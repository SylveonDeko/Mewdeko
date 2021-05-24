using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Mewdeko.Common;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Core.Services;
using Mewdeko.Core.Services.Database;
using Mewdeko.Core.Services.Database.Models;
using Mewdeko.Extensions;
using Mewdeko.Modules.CustomReactions.Extensions;
using Mewdeko.Modules.Permissions.Common;
using Mewdeko.Modules.Permissions.Services;
using Newtonsoft.Json;
using NLog;
using StackExchange.Redis;

namespace Mewdeko.Modules.CustomReactions.Services
{
    public class CustomReactionsService : IEarlyBehavior, INService
    {
        public enum CrField
        {
            AutoDelete,
            DmResponse,
            ContainsAnywhere,
            Message
        }

        private readonly IBotConfigProvider _bc;
        private readonly IDataCache _cache;

        private readonly DiscordSocketClient _client;
        private readonly CommandHandler _cmd;
        private readonly DbService _db;
        private readonly GlobalPermissionService _gperm;

        private readonly Logger _log;
        private readonly PermissionService _perms;
        private readonly IBotStrings _strings;

        private ConcurrentDictionary<int, CustomReaction> _globalReactions;
        private ConcurrentDictionary<ulong, ConcurrentDictionary<int, CustomReaction>> _guildReactions;

        public CustomReactionsService(PermissionService perms, DbService db, IBotStrings strings,
            DiscordSocketClient client, CommandHandler cmd, IBotConfigProvider bc,
            IDataCache cache, GlobalPermissionService gperm, Mewdeko bot)
        {
            _log = LogManager.GetCurrentClassLogger();
            _db = db;
            _client = client;
            _perms = perms;
            _cmd = cmd;
            _bc = bc;
            _strings = strings;
            _cache = cache;
            _gperm = gperm;

            var sub = _cache.Redis.GetSubscriber();
            sub.Subscribe(_client.CurrentUser.Id + "_crs.reload",
                (ch, msg) => { ReloadInternal(bot.GetCurrentGuildConfigs()); }, CommandFlags.FireAndForget);
            sub.Subscribe(_client.CurrentUser.Id + "_gcr.added", (ch, msg) =>
            {
                var cr = JsonConvert.DeserializeObject<CustomReaction>(msg);
                _globalReactions.TryAdd(cr.Id, cr);
            }, CommandFlags.FireAndForget);
            sub.Subscribe(_client.CurrentUser.Id + "_gcr.deleted", (ch, msg) =>
            {
                var id = int.Parse(msg);
                _globalReactions.TryRemove(id, out _);
            }, CommandFlags.FireAndForget);
            sub.Subscribe(_client.CurrentUser.Id + "_gcr.edited", (ch, msg) =>
            {
                var obj = new {Id = 0, Res = "", Ad = false, Dm = false, Ca = false, Re = ""};
                obj = JsonConvert.DeserializeAnonymousType(msg, obj);
                if (_globalReactions.TryGetValue(obj.Id, out var gcr))
                {
                    gcr.Response = obj.Res;
                    gcr.AutoDeleteTrigger = obj.Ad;
                    gcr.DmResponse = obj.Dm;
                    gcr.ContainsAnywhere = obj.Ca;
                    gcr.Reactions = obj.Re;
                }
            }, CommandFlags.FireAndForget);

            ReloadInternal(bot.AllGuildConfigs);

            bot.JoinedGuild += Bot_JoinedGuild;
            _client.LeftGuild += _client_LeftGuild;
        }

        public int Priority => -1;
        public ModuleBehaviorType BehaviorType => ModuleBehaviorType.Executor;

        public async Task<bool> RunBehavior(DiscordSocketClient client, IGuild guild, IUserMessage msg)
        {
            // maybe this message is a custom reaction
            var cr = await Task.Run(() => TryGetCustomReaction(msg)).ConfigureAwait(false);
            if (cr != null)
                try
                {
                    if (_gperm.BlockedModules.Contains("ActualCustomReactions")) return true;

                    if (guild is SocketGuild sg)
                    {
                        var pc = _perms.GetCacheFor(guild.Id);
                        if (!pc.Permissions.CheckPermissions(msg, cr.Trigger, "ActualCustomReactions",
                            out var index))
                        {
                            if (pc.Verbose)
                            {
                                var returnMsg = _strings.GetText("trigger", guild.Id,
                                    "Permissions".ToLowerInvariant(),
                                    index + 1,
                                    Format.Bold(pc.Permissions[index].GetCommand(_cmd.GetPrefix(guild),
                                        (SocketGuild) guild)));
                                try
                                {
                                    await msg.Channel.SendErrorAsync(returnMsg).ConfigureAwait(false);
                                }
                                catch
                                {
                                }

                                _log.Info(returnMsg);
                            }

                            return true;
                        }
                    }

                    var sentMsg = await cr.Send(msg, _client, this).ConfigureAwait(false);


                    var reactions = cr.GetReactions();
                    foreach (var reaction in reactions)
                        try
                        {
                            await sentMsg.AddReactionAsync(reaction.ToIEmote());
                        }
                        catch
                        {
                            _log.Warn($"Unable to add reactions to message {0} in server {1}");
                        }
                        finally
                        {
                            await Task.Delay(0);
                        }

                    if (cr.AutoDeleteTrigger)
                    {
                        await Task.Delay(10000);
                        try
                        {
                            await msg.DeleteAsync().ConfigureAwait(false);
                        }
                        catch
                        {
                        }
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    _log.Warn(ex.Message);
                }

            return false;
        }

        private void ReloadInternal(IEnumerable<GuildConfig> allGuildConfigs)
        {
            using (var uow = _db.GetDbContext())
            {
                ReloadInternal(allGuildConfigs, uow);
            }
        }

        private void ReloadInternal(IEnumerable<GuildConfig> allGuildConfigs, IUnitOfWork uow)
        {
            var guildItems = uow.CustomReactions.GetFor(allGuildConfigs.Select(x => x.GuildId)).ToList();
            _guildReactions = new ConcurrentDictionary<ulong, ConcurrentDictionary<int, CustomReaction>>(guildItems
                .GroupBy(k => k.GuildId.Value)
                .ToDictionary(g => g.Key, g => g.ToDictionary(x => x.Id, x => x).ToConcurrent()));

            var globalItems = uow.CustomReactions.GetGlobal();
            _globalReactions = globalItems
                .ToDictionary(x => x.Id, x => x)
                .ToConcurrent();
        }

        private Task _client_LeftGuild(SocketGuild arg)
        {
            _guildReactions.TryRemove(arg.Id, out _);
            return Task.CompletedTask;
        }

        private Task Bot_JoinedGuild(GuildConfig gc)
        {
            var _ = Task.Run(() =>
            {
                using (var uow = _db.GetDbContext())
                {
                    var crs = uow.CustomReactions.ForId(gc.GuildId)
                        .ToDictionary(x => x.Id, x => x)
                        .ToConcurrent();
                    _guildReactions.AddOrUpdate(gc.GuildId, crs, (key, old) => crs);
                }
            });
            return Task.CompletedTask;
        }

        public Task AddGcr(CustomReaction cr)
        {
            var sub = _cache.Redis.GetSubscriber();
            return sub.PublishAsync(_client.CurrentUser.Id + "_gcr.added", JsonConvert.SerializeObject(cr));
        }

        public Task DelGcr(int id)
        {
            var sub = _cache.Redis.GetSubscriber();
            return sub.PublishAsync(_client.CurrentUser.Id + "_gcr.deleted", id);
        }

        public CustomReaction TryGetCustomReaction(IUserMessage umsg)
        {
            var channel = umsg.Channel as SocketTextChannel;
            if (channel == null)
                return null;

            var content = umsg.Content.Trim().ToLowerInvariant();

            if (_guildReactions.TryGetValue(channel.Guild.Id, out var reactions))
                if (reactions != null && reactions.Any())
                {
                    var rs = reactions.Values.Where(cr =>
                    {
                        if (cr == null)
                            return false;

                        var hasTarget = cr.Response.ToLowerInvariant().Contains("%target%");
                        var trigger = cr.TriggerWithContext(umsg, _client).Trim().ToLowerInvariant();
                        return cr.ContainsAnywhere &&
                               content.GetWordPosition(trigger) != WordPosition.None
                               || hasTarget && content.StartsWith(trigger + " ", StringComparison.InvariantCulture)
                               || _bc.BotConfig.CustomReactionsStartWith &&
                               content.StartsWith(trigger + " ", StringComparison.InvariantCulture)
                               || content == trigger;
                    }).ToArray();

                    if (rs.Length != 0)
                    {
                        var reaction = rs[new MewdekoRandom().Next(0, rs.Length)];
                        if (reaction != null)
                        {
                            if (reaction.Response == "-")
                                return null;
                            //using (var uow = _db.UnitOfWork)
                            //{
                            //    var rObj = uow.CustomReactions.GetById(reaction.Id);
                            //    if (rObj != null)
                            //    {
                            //        rObj.UseCount += 1;
                            //        uow.Complete();
                            //    }
                            //}
                            return reaction;
                        }
                    }
                }

            var grs = _globalReactions.Values.Where(cr =>
            {
                if (cr == null)
                    return false;
                var hasTarget = cr.Response.ToLowerInvariant().Contains("%target%");
                var trigger = cr.TriggerWithContext(umsg, _client).Trim().ToLowerInvariant();
                return cr.ContainsAnywhere &&
                       content.GetWordPosition(trigger) != WordPosition.None
                       || hasTarget && content.StartsWith(trigger + " ", StringComparison.InvariantCulture)
                       || _bc.BotConfig.CustomReactionsStartWith &&
                       content.StartsWith(trigger + " ", StringComparison.InvariantCulture)
                       || content == trigger;
            }).ToArray();
            if (grs.Length == 0)
                return null;
            var greaction = grs[new MewdekoRandom().Next(0, grs.Length)];

            return greaction;
        }

        public async Task ResetCRReactions(ulong? guildId, int id)
        {
            CustomReaction cr;
            using (var uow = _db.GetDbContext())
            {
                cr = uow.CustomReactions.GetById(id);
                if (cr is null)
                    return;

                cr.Reactions = string.Empty;

                await uow.SaveChangesAsync();
            }

            if (guildId is null)
            {
                await PublishEditedGcr(cr).ConfigureAwait(false);
            }
            else
            {
                if (_guildReactions.TryGetValue(guildId.Value, out var crs)
                    && crs.TryGetValue(cr.Id, out var oldCr))
                    oldCr.Reactions = cr.Reactions;
            }
        }

        public async Task SetCrReactions(ulong? guildId, int id, IEnumerable<string> emojis)
        {
            CustomReaction cr;
            using (var uow = _db.GetDbContext())
            {
                cr = uow.CustomReactions.GetById(id);
                if (cr is null)
                    return;

                cr.Reactions = string.Join("@@@", emojis);

                await uow.SaveChangesAsync();
            }

            if (guildId is null)
            {
                await PublishEditedGcr(cr).ConfigureAwait(false);
            }
            else
            {
                if (_guildReactions.TryGetValue(guildId.Value, out var crs)
                    && crs.TryGetValue(cr.Id, out var oldCr))
                    oldCr.Reactions = cr.Reactions;
            }
        }

        public void TriggerReloadCustomReactions()
        {
            var sub = _cache.Redis.GetSubscriber();
            sub.Publish(_client.CurrentUser.Id + "_crs.reload", "");
        }

        public async Task<(bool Sucess, bool NewValue)> ToggleCrOptionAsync(int id, CrField field)
        {
            var newVal = false;
            CustomReaction cr;
            using (var uow = _db.GetDbContext())
            {
                cr = uow.CustomReactions.GetById(id);
                if (cr == null)
                    return (false, false);
                if (field == CrField.AutoDelete)
                    newVal = cr.AutoDeleteTrigger = !cr.AutoDeleteTrigger;
                else if (field == CrField.ContainsAnywhere)
                    newVal = cr.ContainsAnywhere = !cr.ContainsAnywhere;
                else if (field == CrField.DmResponse)
                    newVal = cr.DmResponse = !cr.DmResponse;

                await uow.SaveChangesAsync();
            }

            if (cr.GuildId == null)
            {
                await PublishEditedGcr(cr).ConfigureAwait(false);
            }
            else
            {
                if (_guildReactions.TryGetValue(cr.GuildId.Value, out var crs)
                    && crs.TryGetValue(id, out var oldCr))
                    if (oldCr != null)
                    {
                        oldCr.DmResponse = cr.DmResponse;
                        oldCr.ContainsAnywhere = cr.ContainsAnywhere;
                        oldCr.AutoDeleteTrigger = cr.AutoDeleteTrigger;
                    }
            }

            return (true, newVal);
        }

        public Task PublishEditedGcr(CustomReaction cr)
        {
            // don't publish changes of server-specific crs
            // as other shards no longer have them, nor need them
            if (cr.GuildId != 0 && cr.GuildId != null)
                return Task.CompletedTask;

            var sub = _cache.Redis.GetSubscriber();
            var data = new
            {
                cr.Id,
                Res = cr.Response,
                Ad = cr.AutoDeleteTrigger,
                Dm = cr.DmResponse,
                Ca = cr.ContainsAnywhere,
                Re = cr.Reactions
            };
            return sub.PublishAsync(_client.CurrentUser.Id + "_gcr.edited", JsonConvert.SerializeObject(data));
        }

        public int ClearCustomReactions(ulong id)
        {
            using (var uow = _db.GetDbContext())
            {
                var count = uow.CustomReactions.ClearFromGuild(id);
                _guildReactions.TryRemove(id, out _);
                uow.SaveChanges();
                return count;
            }
        }

        public async Task<CustomReaction> AddCustomReaction(ulong? guildId, string key, string message)
        {
            key = key.ToLowerInvariant();
            var cr = new CustomReaction
            {
                GuildId = guildId,
                IsRegex = false,
                Trigger = key,
                Response = message
            };

            using (var uow = _db.GetDbContext())
            {
                uow.CustomReactions.Add(cr);

                await uow.SaveChangesAsync();
            }

            if (guildId == null)
            {
                await AddGcr(cr).ConfigureAwait(false);
            }
            else
            {
                var crs = _guildReactions.GetOrAdd(guildId.Value, new ConcurrentDictionary<int, CustomReaction>());
                crs.AddOrUpdate(cr.Id, cr, delegate { return cr; });
            }

            return cr;
        }

        public async Task<CustomReaction> EditCustomReaction(ulong? guildId, int id, string message)
        {
            CustomReaction cr;
            using (var uow = _db.GetDbContext())
            {
                cr = uow.CustomReactions.GetById(id);

                if (cr.GuildId != guildId)
                    return null;

                if (cr != null)
                {
                    cr.Response = message;
                    await uow.SaveChangesAsync();
                }
            }

            if (cr != null)
            {
                if (guildId == null)
                {
                    await PublishEditedGcr(cr).ConfigureAwait(false);
                }
                else
                {
                    if (_guildReactions.TryGetValue(guildId.Value, out var crs)
                        && crs.TryGetValue(cr.Id, out var oldCr))
                        oldCr.Response = message;
                }
            }

            return cr;
        }

        public IEnumerable<CustomReaction> GetCustomReactions(ulong? guildId)
        {
            if (guildId == null)
                return _globalReactions.Values;
            return _guildReactions.GetOrAdd(guildId.Value, new ConcurrentDictionary<int, CustomReaction>()).Values;
        }

        public CustomReaction GetCustomReaction(ulong? guildId, int id)
        {
            using (var uow = _db.GetDbContext())
            {
                var cr = uow.CustomReactions.GetById(id);
                if (cr == null || cr.GuildId != guildId)
                    return null;
                return cr;
            }
        }

        public async Task<CustomReaction> DeleteCustomReactionAsync(ulong? guildId, int id)
        {
            var success = false;
            using (var uow = _db.GetDbContext())
            {
                var toDelete = uow.CustomReactions.GetById(id);
                if (toDelete == null) //not found
                {
                    success = false;
                }
                else
                {
                    if ((toDelete.GuildId == null || toDelete.GuildId == 0) && guildId == null)
                    {
                        uow.CustomReactions.Remove(toDelete);
                        await DelGcr(toDelete.Id);
                        success = true;
                    }
                    else if (toDelete.GuildId != null && toDelete.GuildId != 0 && guildId == toDelete.GuildId)
                    {
                        uow.CustomReactions.Remove(toDelete);
                        var grs = _guildReactions.GetOrAdd(guildId.Value,
                            new ConcurrentDictionary<int, CustomReaction>());
                        success = grs.TryRemove(toDelete.Id, out _);
                    }

                    if (success)
                        await uow.SaveChangesAsync();
                }

                return success
                    ? toDelete
                    : null;
            }
        }

        public bool ReactionExists(ulong? guildId, string input)
        {
            using (var uow = _db.GetDbContext())
            {
                var cr = uow.CustomReactions.GetByGuildIdAndInput(guildId, input);
                return cr != null;
            }
        }
    }
}