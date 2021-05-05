using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using NadekoBot.Modules.Administration.Common;
using NadekoBot.Core.Services;
using NadekoBot.Core.Services.Database.Models;
using NLog;
using NadekoBot.Extensions;
using Microsoft.EntityFrameworkCore;

namespace NadekoBot.Modules.Administration.Services
{
    public class ProtectionService : INService
    {
        private readonly ConcurrentDictionary<ulong, AntiRaidStats> _antiRaidGuilds
            = new ConcurrentDictionary<ulong, AntiRaidStats>();

        private readonly ConcurrentDictionary<ulong, AntiSpamStats> _antiSpamGuilds
            = new ConcurrentDictionary<ulong, AntiSpamStats>();
        
        public event Func<PunishmentAction, ProtectionType, IGuildUser[], Task> OnAntiProtectionTriggered
            = delegate { return Task.CompletedTask; };

        private readonly Logger _log;
        private readonly DiscordSocketClient _client;
        private readonly MuteService _mute;
        private readonly DbService _db;
        private readonly UserPunishService _punishService;

        public ProtectionService(DiscordSocketClient client, NadekoBot bot,
            MuteService mute, DbService db, UserPunishService punishService)
        {
            _log = LogManager.GetCurrentClassLogger();
            _client = client;
            _mute = mute;
            _db = db;
            _punishService = punishService;

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
                    set => set
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
            if (!_antiRaidGuilds.TryGetValue(usr.Guild.Id, out var stats))
                return Task.CompletedTask;
            if (!stats.RaidUsers.Add(usr))
                return Task.CompletedTask;

            var _ = Task.Run(async () =>
            {
                try
                {
                    ++stats.UsersCount;

                    if (stats.UsersCount >= stats.AntiRaidSettings.UserThreshold)
                    {
                        var users = stats.RaidUsers.ToArray();
                        stats.RaidUsers.Clear();
                        var settings = stats.AntiRaidSettings;

                        await PunishUsers(settings.Action, ProtectionType.Raiding,
                            settings.PunishDuration, null,  users).ConfigureAwait(false);
                    }
                    await Task.Delay(1000 * stats.AntiRaidSettings.Seconds).ConfigureAwait(false);

                    stats.RaidUsers.TryRemove(usr);
                    --stats.UsersCount;

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
                            var settings = spamSettings.AntiSpamSettings;
                            await PunishUsers(settings.Action, ProtectionType.Spamming, settings.MuteTime,
                                    settings.RoleId, (IGuildUser)msg.Author)
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

        private async Task PunishUsers(PunishmentAction action, ProtectionType pt, int muteTime, ulong? roleId,
            params IGuildUser[] gus)
        {
            _log.Info($"[{pt}] - Punishing [{gus.Length}] users with [{action}] in {gus[0].Guild.Name} guild");
            foreach (var gu in gus)
            {
                await _punishService.ApplyPunishment(gu.Guild, gu, _client.CurrentUser,
                    action, muteTime, roleId, $"{pt} Protection");
                await Task.Delay(1000);
            }
            await OnAntiProtectionTriggered(action, pt, gus).ConfigureAwait(false);
        }

        public async Task<AntiRaidStats> StartAntiRaidAsync(ulong guildId, int userThreshold, int seconds,
            PunishmentAction action, int minutesDuration)
        {
            var g = _client.GetGuild(guildId);
            await _mute.GetMuteRole(g).ConfigureAwait(false);

            if (action == PunishmentAction.AddRole)
                return null;
            
            if (!IsDurationAllowed(action))
                minutesDuration = 0;

            var stats = new AntiRaidStats()
            {
                AntiRaidSettings = new AntiRaidSetting()
                {
                    Action = action,
                    Seconds = seconds,
                    UserThreshold = userThreshold,
                    PunishDuration = minutesDuration
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

        public async Task<AntiSpamStats> StartAntiSpamAsync(ulong guildId, int messageCount, PunishmentAction action,
            int punishDurationMinutes, ulong? roleId)
        {
            var g = _client.GetGuild(guildId);
            await _mute.GetMuteRole(g).ConfigureAwait(false);

            if (!IsDurationAllowed(action))
                punishDurationMinutes = 0;

            var stats = new AntiSpamStats
            {
                AntiSpamSettings = new AntiSpamSetting()
                {
                    Action = action,
                    MessageThreshold = messageCount,
                    MuteTime = punishDurationMinutes,
                    RoleId = roleId,
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
                    gc.AntiSpamSetting.RoleId = stats.AntiSpamSettings.RoleId;
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

        public bool IsDurationAllowed(PunishmentAction action)
        {
            switch (action)
            {
                case PunishmentAction.Ban:
                case PunishmentAction.Mute:
                case PunishmentAction.ChatMute:
                case PunishmentAction.VoiceMute:
                case PunishmentAction.AddRole:
                    return true;
                default:
                    return false;
            }
        }
    }
}
