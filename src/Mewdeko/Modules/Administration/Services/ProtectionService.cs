using System.Threading.Tasks;
using Mewdeko.Modules.Administration.Common;
using Mewdeko.Modules.Moderation.Services;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Mewdeko.Modules.Administration.Services;

public class ProtectionService : INService
{
    private readonly ConcurrentDictionary<ulong, AntiAltStats> antiAltGuilds
        = new();

    private readonly ConcurrentDictionary<ulong, AntiRaidStats> antiRaidGuilds
        = new();

    private readonly ConcurrentDictionary<ulong, AntiSpamStats> antiSpamGuilds
        = new();

    private readonly DiscordSocketClient client;
    private readonly DbService db;
    private readonly MuteService mute;
    private readonly UserPunishService punishService;

    private readonly Channel<PunishQueueItem> punishUserQueue =
        Channel.CreateBounded<PunishQueueItem>(new BoundedChannelOptions(200)
        {
            SingleReader = true, SingleWriter = false, AllowSynchronousContinuations = false, FullMode = BoundedChannelFullMode.DropOldest
        });

    public ProtectionService(DiscordSocketClient client, Mewdeko bot,
        MuteService mute, DbService db, UserPunishService punishService, EventHandler eventHandler)
    {
        this.client = client;
        this.mute = mute;
        this.db = db;
        this.punishService = punishService;

        var ids = client.GetGuildIds();
        using (var uow = db.GetDbContext())
        {
            var configs = uow.GuildConfigs
                .AsQueryable()
                .Include(x => x.AntiRaidSetting)
                .Include(x => x.AntiSpamSetting)
                .ThenInclude(x => x.IgnoredChannels)
                .Include(x => x.AntiAltSetting)
                .Where(x => ids.Contains(x.GuildId))
                .ToList();

            foreach (var gc in configs) Initialize(gc);
        }

        eventHandler.MessageReceived += HandleAntiSpam;
        eventHandler.UserJoined += HandleUserJoined;

        bot.JoinedGuild += _bot_JoinedGuild;
        this.client.LeftGuild += _client_LeftGuild;

        _ = Task.Run(RunQueue);
    }


    public event Func<PunishmentAction, ProtectionType, IGuildUser[], Task> OnAntiProtectionTriggered
        = delegate { return Task.CompletedTask; };

    private async Task RunQueue()
    {
        while (true)
        {
            var item = await punishUserQueue.Reader.ReadAsync().ConfigureAwait(false);
            var muteTime = item.MuteTime;
            var gu = item.User;
            try
            {
                await punishService.ApplyPunishment(gu.Guild, gu, client.CurrentUser, item.Action, muteTime, item.RoleId, $"{item.Type} Protection").ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error in punish queue: {Message}", ex.Message);
            }
            finally
            {
                await Task.Delay(1000);
            }
        }
    }

    private Task _client_LeftGuild(SocketGuild guild)
    {
        _ = Task.Run(async () =>
        {
            await TryStopAntiRaid(guild.Id).ConfigureAwait(false);
            await TryStopAntiSpam(guild.Id).ConfigureAwait(false);
            await TryStopAntiAlt(guild.Id).ConfigureAwait(false);
        });
        return Task.CompletedTask;
    }

    private async Task _bot_JoinedGuild(GuildConfig gc)
    {
        await using var uow = db.GetDbContext();
        var gcWithData = await uow.ForGuildId(gc.GuildId,
            set => set
                .Include(x => x.AntiRaidSetting)
                .Include(x => x.AntiAltSetting)
                .Include(x => x.AntiSpamSetting)
                .ThenInclude(x => x.IgnoredChannels));

        Initialize(gcWithData);
    }

    private void Initialize(GuildConfig gc)
    {
        var raid = gc.AntiRaidSetting;
        var spam = gc.AntiSpamSetting;

        if (raid != null)
        {
            antiRaidGuilds[gc.GuildId] = new AntiRaidStats
            {
                AntiRaidSettings = raid
            };
        }

        if (spam != null)
            antiSpamGuilds[gc.GuildId] = new AntiSpamStats
            {
                AntiSpamSettings = spam
            };

        var alt = gc.AntiAltSetting;
        if (alt is not null)
            antiAltGuilds[gc.GuildId] = new AntiAltStats(alt);
    }

    private Task HandleUserJoined(IGuildUser user)
    {
        if (user.IsBot)
            return Task.CompletedTask;

        antiRaidGuilds.TryGetValue(user.Guild.Id, out var maybeStats);
        antiAltGuilds.TryGetValue(user.Guild.Id, out var maybeAlts);

        if (maybeStats is null && maybeAlts is null)
            return Task.CompletedTask;

        _ = Task.Run(async () =>
        {
            if (maybeAlts is { } alts)
            {
                if (user.CreatedAt != default)
                {
                    var diff = DateTime.UtcNow - user.CreatedAt.UtcDateTime;
                    if (diff < alts.MinAge)
                    {
                        alts.Increment();

                        await PunishUsers(
                            alts.Action,
                            ProtectionType.Alting,
                            alts.ActionDurationMinutes,
                            alts.RoleId,
                            user).ConfigureAwait(false);

                        return;
                    }
                }
            }

            try
            {
                if (maybeStats is not { } stats || !stats.RaidUsers.Add(user))
                    return;

                ++stats.UsersCount;

                if (stats.UsersCount >= stats.AntiRaidSettings.UserThreshold)
                {
                    var users = stats.RaidUsers.ToArray();
                    stats.RaidUsers.Clear();
                    var settings = stats.AntiRaidSettings;

                    await PunishUsers(settings.Action, ProtectionType.Raiding,
                        settings.PunishDuration, null, users).ConfigureAwait(false);
                }

                await Task.Delay(1000 * stats.AntiRaidSettings.Seconds).ConfigureAwait(false);

                stats.RaidUsers.TryRemove(user);
                --stats.UsersCount;
            }
            catch
            {
                // ignored
            }
        });
        return Task.CompletedTask;
    }

    private Task HandleAntiSpam(IMessage arg)
    {
        if (arg is not SocketUserMessage msg
            || msg.Author.IsBot
            || msg.Author is IGuildUser { GuildPermissions.Administrator: true })
            return Task.CompletedTask;

        if (msg.Channel is not ITextChannel channel)
            return Task.CompletedTask;
        _ = Task.Run(async () =>
        {
            try
            {
                if (!antiSpamGuilds.TryGetValue(channel.Guild.Id, out var spamSettings) ||
                    spamSettings.AntiSpamSettings.IgnoredChannels.Contains(new AntiSpamIgnore
                    {
                        ChannelId = channel.Id
                    }))
                {
                    return;
                }

                var stats = spamSettings.UserStats.AddOrUpdate(msg.Author.Id, _ => new UserSpamStats(msg),
                    (_, old) =>
                    {
                        old.ApplyNextMessage(msg);
                        return old;
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
        Log.Information(
            "[{PunishType}] - Punishing [{Count}] users with [{PunishAction}] in {GuildName} guild",
            pt,
            gus.Length,
            action,
            gus[0].Guild.Name);

        foreach (var gu in gus)
        {
            await punishUserQueue.Writer.WriteAsync(new PunishQueueItem
            {
                Action = action,
                Type = pt,
                User = gu,
                MuteTime = muteTime,
                RoleId = roleId
            }).ConfigureAwait(false);
        }

        _ = OnAntiProtectionTriggered(action, pt, gus);
    }

    public async Task<AntiRaidStats?> StartAntiRaidAsync(ulong guildId, int userThreshold, int seconds,
        PunishmentAction action, int minutesDuration)
    {
        var g = client.GetGuild(guildId);
        await mute.GetMuteRole(g).ConfigureAwait(false);

        if (action == PunishmentAction.AddRole)
            return null;

        if (!IsDurationAllowed(action))
            minutesDuration = 0;

        var stats = new AntiRaidStats
        {
            AntiRaidSettings = new AntiRaidSetting
            {
                Action = action, Seconds = seconds, UserThreshold = userThreshold, PunishDuration = minutesDuration
            }
        };

        antiRaidGuilds.AddOrUpdate(guildId, stats, (_, _) => stats);

        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guildId, set => set.Include(x => x.AntiRaidSetting));

        gc.AntiRaidSetting = stats.AntiRaidSettings;
        await uow.SaveChangesAsync().ConfigureAwait(false);

        return stats;
    }

    public async Task<bool> TryStopAntiRaid(ulong guildId)
    {
        if (antiRaidGuilds.TryRemove(guildId, out _))
        {
            await using var uow = db.GetDbContext();
            var gc = await uow.ForGuildId(guildId, set => set.Include(x => x.AntiRaidSetting));

            gc.AntiRaidSetting = null;
            await uow.SaveChangesAsync().ConfigureAwait(false);

            return true;
        }

        return false;
    }

    public async Task<bool> TryStopAntiSpam(ulong guildId)
    {
        if (antiSpamGuilds.TryRemove(guildId, out var removed))
        {
            removed.UserStats.ForEach(x => x.Value.Dispose());
            await using var uow = db.GetDbContext();
            var gc = await uow.ForGuildId(guildId, set => set.Include(x => x.AntiSpamSetting)
                .ThenInclude(x => x.IgnoredChannels));

            gc.AntiSpamSetting = null;
            await uow.SaveChangesAsync().ConfigureAwait(false);

            return true;
        }

        return false;
    }

    public async Task<AntiSpamStats> StartAntiSpamAsync(ulong guildId, int messageCount, PunishmentAction action,
        int punishDurationMinutes, ulong? roleId)
    {
        var g = client.GetGuild(guildId);
        await mute.GetMuteRole(g).ConfigureAwait(false);

        if (!IsDurationAllowed(action))
            punishDurationMinutes = 0;

        var stats = new AntiSpamStats
        {
            AntiSpamSettings = new AntiSpamSetting
            {
                Action = action, MessageThreshold = messageCount, MuteTime = punishDurationMinutes, RoleId = roleId
            }
        };

        var stats1 = stats;
        stats = antiSpamGuilds.AddOrUpdate(guildId, stats, (_, old) =>
        {
            stats1.AntiSpamSettings.IgnoredChannels = old.AntiSpamSettings.IgnoredChannels;
            return stats1;
        });

        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guildId, set => set.Include(x => x.AntiSpamSetting));

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

        await uow.SaveChangesAsync().ConfigureAwait(false);

        return stats;
    }

    public async Task<bool?> AntiSpamIgnoreAsync(ulong guildId, ulong channelId)
    {
        var obj = new AntiSpamIgnore
        {
            ChannelId = channelId
        };
        bool added;
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guildId, set => set.Include(x => x.AntiSpamSetting).ThenInclude(x => x.IgnoredChannels));
        var spam = gc.AntiSpamSetting;
        if (spam is null)
        {
            return null;
        }

        if (spam.IgnoredChannels.Add(obj)) // if adding to db is successful
        {
            if (antiSpamGuilds.TryGetValue(guildId, out var temp))
                temp.AntiSpamSettings.IgnoredChannels.Add(obj); // add to local cache
            added = true;
        }
        else
        {
            var toRemove = spam.IgnoredChannels.First(x => x.ChannelId == channelId);
            uow.Set<AntiSpamIgnore>().Remove(toRemove); // remove from db
            if (antiSpamGuilds.TryGetValue(guildId, out var temp))
            {
                temp.AntiSpamSettings.IgnoredChannels.Remove(toRemove); // remove from local cache
            }

            added = false;
        }

        await uow.SaveChangesAsync().ConfigureAwait(false);
        return added;
    }

    public (AntiSpamStats?, AntiRaidStats?, AntiAltStats?) GetAntiStats(ulong guildId)
    {
        antiRaidGuilds.TryGetValue(guildId, out var antiRaidStats);
        antiSpamGuilds.TryGetValue(guildId, out var antiSpamStats);
        antiAltGuilds.TryGetValue(guildId, out var antiAltStats);

        return (antiSpamStats, antiRaidStats, antiAltStats);
    }

    public static bool IsDurationAllowed(PunishmentAction action) =>
        action switch
        {
            PunishmentAction.Ban => true,
            PunishmentAction.Mute => true,
            PunishmentAction.ChatMute => true,
            PunishmentAction.VoiceMute => true,
            PunishmentAction.AddRole => true,
            PunishmentAction.Timeout => true,
            _ => false
        };

    public async Task StartAntiAltAsync(ulong guildId, int minAgeMinutes, PunishmentAction action,
        int actionDurationMinutes = 0, ulong? roleId = null)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guildId, set => set.Include(x => x.AntiAltSetting));
        gc.AntiAltSetting = new AntiAltSetting
        {
            Action = action, ActionDurationMinutes = actionDurationMinutes, MinAge = TimeSpan.FromMinutes(minAgeMinutes), RoleId = roleId
        };

        await uow.SaveChangesAsync().ConfigureAwait(false);
        antiAltGuilds[guildId] = new AntiAltStats(gc.AntiAltSetting);
    }

    public async Task<bool> TryStopAntiAlt(ulong guildId)
    {
        if (!antiAltGuilds.TryRemove(guildId, out _))
            return false;

        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guildId, set => set.Include(x => x.AntiAltSetting));
        gc.AntiAltSetting = null;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        return true;
    }
}