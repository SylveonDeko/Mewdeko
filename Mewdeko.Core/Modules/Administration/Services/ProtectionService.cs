using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Mewdeko.Modules.Administration.Common;
using Mewdeko.Core.Services;
using Mewdeko.Core.Services.Database.Models;
using NLog;
using Mewdeko.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Modules.Administration.Services
{
    public class ProtectionService : INService
    {
        private readonly ConcurrentDictionary<ulong, AntiRaidStats> _antiRaidGuilds =
                new ConcurrentDictionary<ulong, AntiRaidStats>();

        private readonly ConcurrentDictionary<ulong, AntiSpamStats> _antiSpamGuilds =
                new ConcurrentDictionary<ulong, AntiSpamStats>();

        public event Func<PunishmentAction, ProtectionType, IGuildUser[], Task> OnAntiProtectionTriggered = delegate { return Task.CompletedTask; };

        private readonly Logger _log;
        private readonly DiscordSocketClient _client;
        private readonly MuteService _mute;
        private readonly DbService _db;

        public ProtectionService(DiscordSocketClient client, Mewdeko bot, MuteService mute, DbService db)
        {
            _log = LogManager.GetCurrentClassLogger();
            _client = client;
            _mute = mute;
            _db = db;

            var ids = client.GetGuildIds();
            using (var uow = db.GetDbContext())
            {
                var configs = uow._context.Set<GuildConfig>()
                    .AsQueryable()
                    .Include(x => x.AntiRaidSetting)
                    .Include(x => x.AntiSpamSetting)
                    .ThenInclude(x => x.IgnoredChannels)
                    .Where(x => ids.Contains(x.GuildId))
                    .ToList();
                
                foreach (var gc in configs)
                {
                    Initialize(gc);
                }
            }

            _client.MessageReceived += HandleAntiSpam;
            _client.UserJoined += HandleAntiRaid;

            bot.JoinedGuild += _bot_JoinedGuild;
            _client.LeftGuild += _client_LeftGuild;
        }

        private Task _client_LeftGuild(SocketGuild arg)
        {
            var _ = Task.Run(() =>
            {
                TryStopAntiRaid(arg.Id);
                TryStopAntiSpam(arg.Id);
            });
            return Task.CompletedTask;
        }

        private Task _bot_JoinedGuild(GuildConfig gc)
        {
            using (var uow = _db.GetDbContext())
            {
                var gcWithData = uow.GuildConfigs.ForId(gc.GuildId, 
                    x => x
                        .Include(x => x.AntiRaidSetting)
                        .Include(x => x.AntiSpamSetting)
                        .ThenInclude(x => x.IgnoredChannels));
                
                Initialize(gcWithData);
            }
            return Task.CompletedTask;
        }

        private void Initialize(GuildConfig gc)
        {
            var raid = gc.AntiRaidSetting;
            var spam = gc.AntiSpamSetting;

            if (raid != null)
            {
                var raidStats = new AntiRaidStats() { AntiRaidSettings = raid };
                _antiRaidGuilds.TryAdd(gc.GuildId, raidStats);
            }

            if (spam != null)
                _antiSpamGuilds.TryAdd(gc.GuildId, new AntiSpamStats() { AntiSpamSettings = spam });
        }

        private Task HandleAntiRaid(SocketGuildUser usr)
        {
            if (usr.IsBot)
                return Task.CompletedTask;
            if (!_antiRaidGuilds.TryGetValue(usr.Guild.Id, out var settings))
                return Task.CompletedTask;
            if (!settings.RaidUsers.Add(usr))
                return Task.CompletedTask;

            var _ = Task.Run(async () =>
            {
                try
                {
                    ++settings.UsersCount;

                    if (settings.UsersCount >= settings.AntiRaidSettings.UserThreshold)
                    {
                        var users = settings.RaidUsers.ToArray();
                        settings.RaidUsers.Clear();

                        await PunishUsers(settings.AntiRaidSettings.Action, ProtectionType.Raiding, 0, users).ConfigureAwait(false);
                    }
                    await Task.Delay(1000 * settings.AntiRaidSettings.Seconds).ConfigureAwait(false);

                    settings.RaidUsers.TryRemove(usr);
                    --settings.UsersCount;

                }
                catch
                {
                    // ignored
                }
            });
            return Task.CompletedTask;
        }

        private Task HandleAntiSpam(SocketMessage arg)
        {
            if (!(arg.Author is IGuildUser gu) || gu.GuildPermissions.MuteMembers)
                return Task.CompletedTask;
            if (!(arg is SocketUserMessage msg) || msg.Author.IsBot)
                return Task.CompletedTask;

            if (!(msg.Channel is ITextChannel channel))
                return Task.CompletedTask;
            var _ = Task.Run(async () =>
            {
                try
                {
                    if (!_antiSpamGuilds.TryGetValue(channel.Guild.Id, out var spamSettings) ||
                        spamSettings.AntiSpamSettings.IgnoredChannels.Contains(new AntiSpamIgnore()
                        {
                            ChannelId = channel.Id
                        }))
                        return;

                    var stats = spamSettings.UserStats.AddOrUpdate(msg.Author.Id, (id) => new UserSpamStats(msg),
                        (id, old) =>
                        {
                            old.ApplyNextMessage(msg); return old;
                        });

                    if (stats.Count >= spamSettings.AntiSpamSettings.MessageThreshold)
                    {
                        if (spamSettings.UserStats.TryRemove(msg.Author.Id, out stats))
                        {
                            stats.Dispose();
                            await PunishUsers(spamSettings.AntiSpamSettings.Action, ProtectionType.Spamming, spamSettings.AntiSpamSettings.MuteTime, (IGuildUser)msg.Author)
                                .ConfigureAwait(false);
                        }
                    }
                }
                catch
                {
                    // ignored
                }
            });
            return Task.CompletedTask;
        }

        private async Task PunishUsers(PunishmentAction action, ProtectionType pt, int muteTime, params IGuildUser[] gus)
        {
            _log.Info($"[{pt}] - Punishing [{gus.Length}] users with [{action}] in {gus[0].Guild.Name} guild");
            foreach (var gu in gus)
            {
                switch (action)
                {
                    case PunishmentAction.Mute:
                        try
                        {
                            var muteReason = $"{pt} Protection";
                            if (muteTime <= 0)
                                await _mute.MuteUser(gu, _client.CurrentUser, reason: muteReason).ConfigureAwait(false);
                            else
                                await _mute.TimedMute(gu, _client.CurrentUser, TimeSpan.FromSeconds(muteTime), reason: muteReason).ConfigureAwait(false);
                        }
                        catch (Exception ex) { _log.Warn(ex, "I can't apply punishement"); }
                        break;
                    case PunishmentAction.Kick:
                        try
                        {
                            await gu.KickAsync().ConfigureAwait(false);
                        }
                        catch (Exception ex) { _log.Warn(ex, "I can't apply punishement"); }
                        break;
                    case PunishmentAction.Softban:
                        try
                        {
                            await gu.Guild.AddBanAsync(gu, 7).ConfigureAwait(false);
                            try
                            {
                                await gu.Guild.RemoveBanAsync(gu).ConfigureAwait(false);
                            }
                            catch
                            {
                                await gu.Guild.RemoveBanAsync(gu).ConfigureAwait(false);
                                // try it twice, really don't want to ban user if 
                                // only kick has been specified as the punishement
                            }
                        }
                        catch (Exception ex) { _log.Warn(ex, "I can't apply punishment"); }
                        break;
                    case PunishmentAction.Ban:
                        try
                        {
                            await gu.Guild.AddBanAsync(gu, 7, "Applying Anti-Raid Punishment.").ConfigureAwait(false);
                        }
                        catch (Exception ex) { _log.Warn(ex, "I can't apply punishment"); }
                        break;
                    case PunishmentAction.RemoveRoles:
                        await gu.RemoveRolesAsync(gu.GetRoles().Where(x => x.Id != gu.Guild.EveryoneRole.Id)).ConfigureAwait(false);
                        break;
                }
            }
            await OnAntiProtectionTriggered(action, pt, gus).ConfigureAwait(false);
        }

        public async Task<AntiRaidStats> StartAntiRaidAsync(ulong guildId, int userThreshold, int seconds, PunishmentAction action)
        {

            var g = _client.GetGuild(guildId);
            await _mute.GetMuteRole(g).ConfigureAwait(false);

            var stats = new AntiRaidStats()
            {
                AntiRaidSettings = new AntiRaidSetting()
                {
                    Action = action,
                    Seconds = seconds,
                    UserThreshold = userThreshold,
                }
            };

            _antiRaidGuilds.AddOrUpdate(guildId, stats, (key, old) => stats);

            using (var uow = _db.GetDbContext())
            {
                var gc = uow.GuildConfigs.ForId(guildId, set => set.Include(x => x.AntiRaidSetting));

                gc.AntiRaidSetting = stats.AntiRaidSettings;
                await uow.SaveChangesAsync();
            }

            return stats;
        }

        public bool TryStopAntiRaid(ulong guildId)
        {
            if (_antiRaidGuilds.TryRemove(guildId, out _))
            {
                using (var uow = _db.GetDbContext())
                {
                    var gc = uow.GuildConfigs.ForId(guildId, set => set.Include(x => x.AntiRaidSetting));

                    gc.AntiRaidSetting = null;
                    uow.SaveChanges();
                }
                return true;
            }
            return false;
        }

        public bool TryStopAntiSpam(ulong guildId)
        {
            if (_antiSpamGuilds.TryRemove(guildId, out var removed))
            {
                removed.UserStats.ForEach(x => x.Value.Dispose());
                using (var uow = _db.GetDbContext())
                {
                    var gc = uow.GuildConfigs.ForId(guildId, set => set.Include(x => x.AntiSpamSetting)
                        .ThenInclude(x => x.IgnoredChannels));

                    gc.AntiSpamSetting = null;
                    uow.SaveChanges();
                }
                return true;
            }
            return false;
        }

        public async Task<AntiSpamStats> StartAntiSpamAsync(ulong guildId, int messageCount, int time, PunishmentAction action)
        {
            var g = _client.GetGuild(guildId);
            await _mute.GetMuteRole(g).ConfigureAwait(false);

            var stats = new AntiSpamStats
            {
                AntiSpamSettings = new AntiSpamSetting()
                {
                    Action = action,
                    MessageThreshold = messageCount,
                    MuteTime = time,
                }
            };

            stats = _antiSpamGuilds.AddOrUpdate(guildId, stats, (key, old) =>
            {
                stats.AntiSpamSettings.IgnoredChannels = old.AntiSpamSettings.IgnoredChannels;
                return stats;
            });

            using (var uow = _db.GetDbContext())
            {
                var gc = uow.GuildConfigs.ForId(guildId, set => set.Include(x => x.AntiSpamSetting));

                if (gc.AntiSpamSetting != null)
                {
                    gc.AntiSpamSetting.Action = stats.AntiSpamSettings.Action;
                    gc.AntiSpamSetting.MessageThreshold = stats.AntiSpamSettings.MessageThreshold;
                    gc.AntiSpamSetting.MuteTime = stats.AntiSpamSettings.MuteTime;
                }
                else
                {
                    gc.AntiSpamSetting = stats.AntiSpamSettings;
                }
                await uow.SaveChangesAsync();
            }
            return stats;
        }

        public async Task<bool?> AntiSpamIgnoreAsync(ulong guildId, ulong channelId)
        {
            var obj = new AntiSpamIgnore()
            {
                ChannelId = channelId
            };
            bool added;
            using (var uow = _db.GetDbContext())
            {
                var gc = uow.GuildConfigs.ForId(guildId, set => set.Include(x => x.AntiSpamSetting).ThenInclude(x => x.IgnoredChannels));
                var spam = gc.AntiSpamSetting;
                if (spam is null)
                {
                    return null;
                }

                if (spam.IgnoredChannels.Add(obj)) // if adding to db is successful
                {
                    if (_antiSpamGuilds.TryGetValue(guildId, out var temp))
                        temp.AntiSpamSettings.IgnoredChannels.Add(obj); // add to local cache
                    added = true;
                }
                else
                {
                    var toRemove = spam.IgnoredChannels.First(x => x.ChannelId == channelId);
                    uow._context.Set<AntiSpamIgnore>().Remove(toRemove); // remove from db
                    if (_antiSpamGuilds.TryGetValue(guildId, out var temp))
                    {
                        temp.AntiSpamSettings.IgnoredChannels.Remove(toRemove); // remove from local cache
                    }
                    added = false;
                }

                await uow.SaveChangesAsync();
            }
            return added;
        }

        public (AntiSpamStats, AntiRaidStats) GetAntiStats(ulong guildId)
        {
            _antiRaidGuilds.TryGetValue(guildId, out var antiRaidStats);
            _antiSpamGuilds.TryGetValue(guildId, out var antiSpamStats);

            return (antiSpamStats, antiRaidStats);
        }
    }
}
