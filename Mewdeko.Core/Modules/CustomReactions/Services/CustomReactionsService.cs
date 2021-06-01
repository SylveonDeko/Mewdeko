using Discord;
using Discord.WebSocket;
using Mewdeko.Common;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Core.Services;
using Mewdeko.Core.Services.Database.Models;
using Mewdeko.Extensions;
using Mewdeko.Modules.CustomReactions.Extensions;
using Mewdeko.Modules.Permissions.Common;
using Mewdeko.Modules.Permissions.Services;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Mewdeko.Core.Common;
using Serilog;

namespace Mewdeko.Modules.CustomReactions.Services
{
    public sealed class CustomReactionsService : IEarlyBehavior, INService, IReadyExecutor
    {
        public enum CrField
        {
            AutoDelete,
            DmResponse,
            AllowTarget,
            ContainsAnywhere,
            Message,
        }

        private readonly object _gcrWriteLock = new object();

        private readonly TypedKey<CustomReaction> _gcrAddedKey = new TypedKey<CustomReaction>("gcr.added");
        private readonly TypedKey<int> _gcrDeletedkey = new TypedKey<int>("gcr.deleted");
        private readonly TypedKey<CustomReaction> _gcrEditedKey = new TypedKey<CustomReaction>("gcr.edited");
        private readonly TypedKey<bool> _crsReloadedKey = new TypedKey<bool>("crs.reloaded");
        private const string MentionPh = "%bot.mention%";

        // it is perfectly fine to have global customreactions as an array
        // 1. custom reactions are almost never added (compared to how many times they are being looped through)
        // 2. only need write locks for this as we'll rebuild+replace the array on every edit
        // 3. there's never many of them (at most a thousand, usually < 100)
        private CustomReaction[] _globalReactions;
        private ConcurrentDictionary<ulong, CustomReaction[]> _newGuildReactions;

        public int Priority => -1;
        public ModuleBehaviorType BehaviorType => ModuleBehaviorType.Executor;

        private readonly DbService _db;
        private readonly DiscordSocketClient _client;
        private readonly PermissionService _perms;
        private readonly CommandHandler _cmd;
        private readonly IBotStrings _strings;
        private readonly Mewdeko _bot;
        private readonly GlobalPermissionService _gperm;
        private readonly IPubSub _pubSub;
        private readonly Random _rng;

        public CustomReactionsService(PermissionService perms, DbService db, IBotStrings strings, Mewdeko bot,
            DiscordSocketClient client, CommandHandler cmd, GlobalPermissionService gperm,
            IPubSub pubSub)
        {
            _db = db;
            _client = client;
            _perms = perms;
            _cmd = cmd;
            _strings = strings;
            _bot = bot;
            _gperm = gperm;
            _pubSub = pubSub;
            _rng = new MewdekoRandom();

            _pubSub.Sub(_crsReloadedKey, OnCrsShouldReload);
            pubSub.Sub(_gcrAddedKey, OnGcrAdded);
            pubSub.Sub(_gcrDeletedkey, OnGcrDeleted);
            pubSub.Sub(_gcrEditedKey, OnGcrEdited);

            bot.JoinedGuild += OnJoinedGuild;
            _client.LeftGuild += OnLeftGuild;
        }

        private async Task ReloadInternal(IReadOnlyList<ulong> allGuildIds)
        {
            using var uow = _db.GetDbContext();
            var guildItems = await uow._context.CustomReactions
                .AsNoTracking()
                .Where(x => allGuildIds.Contains(x.GuildId.Value))
                .ToListAsync();

            _newGuildReactions = guildItems
                .GroupBy(k => k.GuildId!.Value)
                .ToDictionary(g => g.Key,
                    g => g.Select(x =>
                    {
                        x.Trigger = x.Trigger.Replace(MentionPh, _bot.Mention);
                        return x;
                    }).ToArray())
                .ToConcurrent();

            lock (_gcrWriteLock)
            {
                var globalItems = uow._context
                    .CustomReactions
                    .AsNoTracking()
                    .Where(x => x.GuildId == null || x.GuildId == 0)
                    .AsEnumerable()
                    .Select(x =>
                    {
                        x.Trigger = x.Trigger.Replace(MentionPh, _bot.Mention);
                        return x;
                    })
                    .ToArray();

                _globalReactions = globalItems;
            }

            ready = true;
        }

        #region Event Handlers

        public Task OnReadyAsync() 
            => ReloadInternal(_bot.GetCurrentGuildIds());

        private ValueTask OnCrsShouldReload(bool _)
            => new ValueTask(ReloadInternal(_bot.GetCurrentGuildIds()));
        
        private ValueTask OnGcrAdded(CustomReaction c)
        {
            lock (_gcrWriteLock)
            {
                var newGlobalReactions = new CustomReaction[_globalReactions.Length + 1];
                Array.Copy(_globalReactions, newGlobalReactions, _globalReactions.Length);
                newGlobalReactions[_globalReactions.Length] = c;
                _globalReactions = newGlobalReactions;
            }

            return default;
        }

        private ValueTask OnGcrEdited(CustomReaction c)
        {
            lock (_gcrWriteLock)
            {
                for (var i = 0; i < _globalReactions.Length; i++)
                {
                    if (_globalReactions[i].Id == c.Id)
                    {
                        _globalReactions[i] = c;
                        return default;
                    }
                }

                // if edited cr is not found?!
                // add it
                OnGcrAdded(c);
            }

            return default;
        }

        private ValueTask OnGcrDeleted(int id)
        {
            lock (_gcrWriteLock)
            {
                var newGlobalReactions = DeleteInternal(_globalReactions, id, out _);
                _globalReactions = newGlobalReactions;
            }

            return default;
        }

        public Task TriggerReloadCustomReactions()
            => _pubSub.Pub(_crsReloadedKey, true);
        
        #endregion

        #region Client Event Handlers

        private Task OnLeftGuild(SocketGuild arg)
        {
            _newGuildReactions.TryRemove(arg.Id, out _);
            
            return Task.CompletedTask;
        }

        private async Task OnJoinedGuild(GuildConfig gc)
        {
            using var uow = _db.GetDbContext();
            var crs = await uow._context
                .CustomReactions
                .AsNoTracking()
                .Where(x => x.GuildId == gc.GuildId)
                .ToArrayAsync();

            _newGuildReactions[gc.GuildId] = crs;
        }

        #endregion
        
        #region Basic Operations

        public async Task<CustomReaction> AddAsync(ulong? guildId, string key, string message)
        {
            key = key.ToLowerInvariant();
            var cr = new CustomReaction()
            {
                GuildId = guildId,
                Trigger = key,
                Response = message,
            };

            if (cr.Response.Contains("%target%", StringComparison.OrdinalIgnoreCase))
                cr.AllowTarget = true;

            using (var uow = _db.GetDbContext())
            {
                uow.CustomReactions.Add(cr);
                await uow.SaveChangesAsync();
            }

            await AddInternalAsync(guildId, cr);
            
            return cr;
        }

        public async Task<CustomReaction> EditAsync(ulong? guildId, int id, string message)
        {
            using var uow = _db.GetDbContext();
            var cr = uow.CustomReactions.GetById(id);

            if (cr == null || cr.GuildId != guildId)
                return null;

            // disable allowtarget if message had target, but it was removed from it
            if (!message.Contains("%target%", StringComparison.OrdinalIgnoreCase)
                && cr.Response.Contains("%target%", StringComparison.OrdinalIgnoreCase))
            {
                cr.AllowTarget = false;
            }

            cr.Response = message;
            
            // enable allow target if message is edited to contain target
            if (cr.Response.Contains("%target%", StringComparison.OrdinalIgnoreCase))
                cr.AllowTarget = true;
            
            await uow.SaveChangesAsync();
            await UpdateInternalAsync(guildId, cr);
            
            return cr;
        }
        

        public async Task<CustomReaction> DeleteAsync(ulong? guildId, int id)
        {
            using var uow = _db.GetDbContext();
            var toDelete = uow.CustomReactions.GetById(id);
            
            if (toDelete is null)
                return null;
            
            if ((toDelete.IsGlobal() && guildId == null) || (guildId == toDelete.GuildId))
            {
                uow.CustomReactions.Remove(toDelete);
                await uow.SaveChangesAsync();
                await DeleteInternalAsync(guildId, id);
                return toDelete;
            }

            return null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CustomReaction[] GetCustomReactionsFor(ulong? maybeGuildId)
        {
            if (maybeGuildId is ulong guildId)
            {
                return _newGuildReactions.TryGetValue(guildId, out var crs)
                    ? crs
                    : Array.Empty<CustomReaction>();
            }
            
            return _globalReactions;
        }

        #endregion

        private bool ready;

        private CustomReaction TryGetCustomReaction(IUserMessage umsg)
        {
            if (!ready)
                return null;

            if (!(umsg.Channel is SocketTextChannel channel))
                return null;

            var content = umsg.Content.Trim().ToLowerInvariant();
            
            if (_newGuildReactions.TryGetValue(channel.Guild.Id, out var reactions) && reactions.Length > 0)
            {
                var cr = MatchCustomReactions(content, reactions);
                if (!(cr is null))
                    return cr;
            }

            var localGrs = _globalReactions;

            return MatchCustomReactions(content, localGrs);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private CustomReaction MatchCustomReactions(in ReadOnlySpan<char> content, CustomReaction[] crs)
        {
            var result = new List<CustomReaction>(1);
            for (var i = 0; i < crs.Length; i++)
            {
                var cr = crs[i];
                var trigger = cr.Trigger;
                if (content.Length > trigger.Length)
                {
                    // if input is greater than the trigger, it can only work if:
                    // it has CA enabled
                    if (cr.ContainsAnywhere)
                    {
                        // if ca is enabled, we have to check if it is a word within the content
                        var wp = content.GetWordPosition(trigger);

                        // if it is, then that's valid
                        if (wp != WordPosition.None)
                        {
                            result.Add(cr);
                        }

                        // if it's not, then it cant' work under any circumstance,
                        // because content is greater than the trigger length
                        // so it can't be equal, and it's not contained as a word
                        continue;
                    }

                    // if CA is disabled, and CR has AllowTarget, then the
                    // content has to start with the trigger followed by a space
                    if (cr.AllowTarget && content.StartsWith(trigger, StringComparison.OrdinalIgnoreCase)
                                       && content[trigger.Length] == ' ')
                    {
                        result.Add(cr);
                    }
                }
                else if (content.Length < cr.Trigger.Length)
                {
                    // if input length is less than trigger length, it means
                    // that the reaction can never be triggered
                }
                else
                {
                    // if input length is the same as trigger length
                    // reaction can only trigger if the strings are equal
                    if (content.SequenceEqual(cr.Trigger))
                    {
                        result.Add(cr);
                    }
                }
            }

            if (result.Count == 0)
                return null;

            return result[_rng.Next(0, result.Count)];
        }

        public async Task<bool> RunBehavior(DiscordSocketClient client, IGuild guild, IUserMessage msg)
        {
            // maybe this message is a custom reaction
            var cr = TryGetCustomReaction(msg);

            if (cr is null)
                return false;
            
            try
            {
                if (_gperm.BlockedModules.Contains("ActualCustomReactions"))
                {
                    return true;
                }

                if (guild is SocketGuild sg)
                {
                    var pc = _perms.GetCacheFor(guild.Id);
                    if (!pc.Permissions.CheckPermissions(msg, cr.Trigger, "ActualCustomReactions",
                        out int index))
                    {
                        if (pc.Verbose)
                        {
                            var returnMsg = _strings.GetText("trigger", sg.Id,
                                index + 1,
                                Format.Bold(pc.Permissions[index].GetCommand(_cmd.GetPrefix(guild), sg)));
                            try
                            {
                                await msg.Channel.SendErrorAsync(returnMsg).ConfigureAwait(false);
                            }
                            catch
                            {
                            }

                            Log.Information(returnMsg);
                        }

                        return true;
                    }
                }

                var sentMsg = await cr.Send(msg, _client, false).ConfigureAwait(false);

                var reactions = cr.GetReactions();
                foreach (var reaction in reactions)
                {
                    try
                    {
                        await sentMsg.AddReactionAsync(reaction.ToIEmote());
                    }
                    catch
                    {
                        Log.Warning("Unable to add reactions to message {Message} in server {GuildId}", sentMsg.Id,
                            cr.GuildId);
                        break;
                    }

                    await Task.Delay(1000);
                }

                if (cr.AutoDeleteTrigger)
                {
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
                Log.Warning(ex.Message);
            }

            return false;
        }

        public async Task ResetCrReactions(ulong? maybeGuildId, int id)
        {
            CustomReaction cr;
            using var uow = _db.GetDbContext();
            cr = uow.CustomReactions.GetById(id);
            if (cr is null)
                return;

            cr.Reactions = string.Empty;

            await uow.SaveChangesAsync();
        }

        private Task UpdateInternalAsync(ulong? maybeGuildId, CustomReaction cr)
        {
            if (maybeGuildId is ulong guildId)
                UpdateInternal(guildId, cr);
            else
                return _pubSub.Pub(_gcrEditedKey, cr);

            return Task.CompletedTask;
        }

        private void UpdateInternal(ulong? maybeGuildId, CustomReaction cr)
        {
            if (maybeGuildId is ulong guildId)
            {
                _newGuildReactions.AddOrUpdate(guildId, new[] {cr},
                    (key, old) =>
                    {
                        var newArray = old.ToArray();
                        for (var i = 0; i < newArray.Length; i++)
                        {
                            if (newArray[i].Id == cr.Id)
                                newArray[i] = cr;
                        }
                        return newArray;
                    });
            }
            else
            {
                lock (_gcrWriteLock)
                {
                    var crs = _globalReactions;
                    for (var i = 0; i < crs.Length; i++)
                    {
                        if (crs[i].Id == cr.Id)
                            crs[i] = cr;
                    }
                }
            }
        }

        private Task AddInternalAsync(ulong? maybeGuildId, CustomReaction cr)
        {
            // only do this for perf purposes
            cr.Trigger = cr.Trigger.Replace(MentionPh, _client.CurrentUser.Mention);

            if (maybeGuildId is ulong guildId)
            {
                _newGuildReactions.AddOrUpdate(guildId,
                    new[] {cr},
                    (key, old) => old.With(cr));
            }
            else
            {
                return _pubSub.Pub(_gcrAddedKey, cr);
            }

            return Task.CompletedTask;
        }
        
        private Task DeleteInternalAsync(ulong? maybeGuildId, int id)
        {
            if (maybeGuildId is ulong guildId)
            {
                _newGuildReactions.AddOrUpdate(guildId,
                    Array.Empty<CustomReaction>(),
                    (key, old) => DeleteInternal(old, id, out _));
                
                return Task.CompletedTask;
            }

            lock (_gcrWriteLock)
            {
                var cr = Array.Find(_globalReactions, item => item.Id == id);
                if (!(cr is null))
                {
                    return _pubSub.Pub(_gcrDeletedkey, cr.Id);
                }
            }

            return Task.CompletedTask;
        }

        private CustomReaction[] DeleteInternal(IReadOnlyList<CustomReaction> crs, int id, out CustomReaction deleted)
        {
            deleted = null;
            if (crs is null || crs.Count == 0)
                return crs as CustomReaction[] ?? crs?.ToArray();
            
            var newCrs = new CustomReaction[crs.Count - 1];
            for (int i = 0, k = 0; i < crs.Count; i++, k++)
            {
                if (crs[i].Id == id)
                {
                    deleted = crs[i];
                    k--;
                    continue;
                }

                newCrs[k] = crs[i];
            }

            return newCrs;
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

            await UpdateInternalAsync(guildId, cr);
        }

        public async Task<(bool Sucess, bool NewValue)> ToggleCrOptionAsync(int id, CrField field)
        {
            var newVal = false;
            CustomReaction cr;
            using (var uow = _db.GetDbContext())
            {
                cr = uow.CustomReactions.GetById(id);
                if (cr is null)
                    return (false, false);
                if (field == CrField.AutoDelete)
                    newVal = cr.AutoDeleteTrigger = !cr.AutoDeleteTrigger;
                else if (field == CrField.ContainsAnywhere)
                    newVal = cr.ContainsAnywhere = !cr.ContainsAnywhere;
                else if (field == CrField.DmResponse)
                    newVal = cr.DmResponse = !cr.DmResponse;
                else if (field == CrField.AllowTarget)
                    newVal = cr.AllowTarget = !cr.AllowTarget;

                await uow.SaveChangesAsync();
            }

            await UpdateInternalAsync(cr.GuildId, cr);

            return (true, newVal);
        }

        public CustomReaction GetCustomReaction(ulong? guildId, int id)
        {
            using var uow = _db.GetDbContext();
            var cr = uow.CustomReactions.GetById(id);
            if (cr == null || cr.GuildId != guildId)
                return null;

            return cr;
        }
        
        public int DeleteAllCustomReactions(ulong guildId)
        {
            using var uow = _db.GetDbContext();
            var count = uow.CustomReactions.ClearFromGuild(guildId);
            uow.SaveChanges();
            
            _newGuildReactions.TryRemove(guildId, out _);

            return count;
        }

        public bool ReactionExists(ulong? guildId, string input)
        {
            using var uow = _db.GetDbContext();
            var cr = uow.CustomReactions.GetByGuildIdAndInput(guildId, input);
            return cr != null;
        }
    }
}