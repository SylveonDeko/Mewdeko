using Mewdeko.Modules.Administration.Common;
using Mewdeko.Modules.Moderation.Services;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Mewdeko.Modules.Administration.Services;

/// <summary>
/// Provides anti-alt, anti-raid, and antispam protection services.
/// </summary>
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

    /// <summary>
    /// The punish user queue.
    /// </summary>
    private readonly Channel<PunishQueueItem> punishUserQueue =
        Channel.CreateBounded<PunishQueueItem>(new BoundedChannelOptions(200)
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false,
            FullMode = BoundedChannelFullMode.DropOldest
        });

    /// <summary>
    /// Constructs a new instance of the ProtectionService.
    /// </summary>
    /// <param name="client">The Discord client.</param>
    /// <param name="bot">The Mewdeko bot.</param>
    /// <param name="mute">The mute service.</param>
    /// <param name="db">The database service.</param>
    /// <param name="punishService">The user punish service.</param>
    /// <param name="eventHandler">The event handler.</param>
    public ProtectionService(DiscordSocketClient client, Mewdeko bot,
        MuteService mute, DbService db, UserPunishService punishService, EventHandler eventHandler)
    {
        this.client = client;
        this.mute = mute;
        this.db = db;
        this.punishService = punishService;
        foreach (var gc in bot.AllGuildConfigs) Initialize(gc.Value);

        eventHandler.MessageReceived += HandleAntiSpam;
        eventHandler.UserJoined += HandleUserJoined;

        bot.JoinedGuild += _bot_JoinedGuild;
        this.client.LeftGuild += _client_LeftGuild;

        _ = Task.Run(RunQueue);
    }


    /// <summary>
    /// An event that is triggered when the anti-protection is triggered.
    /// </summary>
    public event Func<PunishmentAction, ProtectionType, IGuildUser[], Task> OnAntiProtectionTriggered
        = delegate { return Task.CompletedTask; };

    /// <summary>
    /// The task that runs the punish queue.
    /// </summary>
    private async Task RunQueue()
    {
        while (true)
        {
            var item = await punishUserQueue.Reader.ReadAsync().ConfigureAwait(false);
            var muteTime = item.MuteTime;
            var gu = item.User;
            try
            {
                await punishService.ApplyPunishment(gu.Guild, gu, client.CurrentUser, item.Action, muteTime,
                    item.RoleId, $"{item.Type} Protection").ConfigureAwait(false);
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


    /// <summary>
    /// Handles the event when the bot leaves a guild.
    /// </summary>
    /// <param name="guild">The guild that the bot has left.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
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

    /// <summary>
    /// Handles the event when the bot joins a guild.
    /// </summary>
    /// <param name="gc">The configuration of the guild that the bot has joined.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
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

    /// <summary>
    /// Initializes the anti-raid, anti-spam, and anti-alt settings for a guild.
    /// </summary>
    /// <param name="gc">The configuration of the guild to initialize.</param>
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

    /// <summary>
    /// Handles the event when a user joins a guild.
    /// </summary>
    /// <param name="user">The user that has joined the guild.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private Task HandleUserJoined(IGuildUser user)
    {
        // If the user is a bot, do nothing
        if (user.IsBot)
            return Task.CompletedTask;

        // Try to get the anti-raid and anti-alt settings for the guild
        antiRaidGuilds.TryGetValue(user.Guild.Id, out var maybeStats);
        antiAltGuilds.TryGetValue(user.Guild.Id, out var maybeAlts);

        // If no settings are found, do nothing
        if (maybeStats is null && maybeAlts is null)
            return Task.CompletedTask;

        // Run the anti-raid and anti-alt checks in a separate task
        _ = Task.Run(async () =>
        {
            // If anti-alt settings are found
            if (maybeAlts is { } alts)
            {
                // If the user's account is not new
                if (user.CreatedAt != default)
                {
                    // Calculate the age of the user's account
                    var diff = DateTime.UtcNow - user.CreatedAt.UtcDateTime;
                    // If the account is younger than the minimum age
                    if (diff < TimeSpan.Parse(alts.MinAge))
                    {
                        // Increment the counter of new accounts
                        alts.Increment();

                        // Punish the user
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
                // If anti-raid settings are found
                if (maybeStats is not { } stats || !stats.RaidUsers.Add(user))
                    return;

                // Increment the counter of users
                ++stats.UsersCount;

                // If the number of users exceeds the threshold
                if (stats.UsersCount >= stats.AntiRaidSettings.UserThreshold)
                {
                    // Get the users that triggered the anti-raid
                    var users = stats.RaidUsers.ToArray();
                    // Clear the users
                    stats.RaidUsers.Clear();
                    var settings = stats.AntiRaidSettings;

                    // Punish the users
                    await PunishUsers(settings.Action, ProtectionType.Raiding,
                        settings.PunishDuration, null, users).ConfigureAwait(false);
                }

                // Wait for a period of time
                await Task.Delay(1000 * stats.AntiRaidSettings.Seconds).ConfigureAwait(false);

                // Remove the user from the list
                stats.RaidUsers.TryRemove(user);
                // Decrement the counter of users
                --stats.UsersCount;
            }
            catch
            {
                // ignored
            }
        });
        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles the event when a message is received in a guild for anti-spam protection.
    /// </summary>
    /// <param name="arg">The message that was received.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private Task HandleAntiSpam(IMessage arg)
    {
        // If the message is not from a user, or the author is a bot, or the author is an administrator, do nothing
        if (arg is not SocketUserMessage msg
            || msg.Author.IsBot
            || msg.Author is IGuildUser { GuildPermissions.Administrator: true })
            return Task.CompletedTask;

        // If the message was not sent in a text channel, do nothing
        if (msg.Channel is not ITextChannel channel)
            return Task.CompletedTask;

        // Run the anti-spam check in a separate task
        _ = Task.Run(async () =>
        {
            try
            {
                // If no anti-spam settings are found for the guild, or the channel is ignored, do nothing
                if (!antiSpamGuilds.TryGetValue(channel.Guild.Id, out var spamSettings) ||
                    spamSettings.AntiSpamSettings.IgnoredChannels.Contains(new AntiSpamIgnore
                    {
                        ChannelId = channel.Id
                    }))
                {
                    return;
                }

                // Update the user's message stats
                var stats = spamSettings.UserStats.AddOrUpdate(msg.Author.Id, _ => new UserSpamStats(msg),
                    (_, old) =>
                    {
                        old.ApplyNextMessage(msg);
                        return old;
                    });

                // If the number of messages sent by the user exceeds the threshold
                if (stats.Count >= spamSettings.AntiSpamSettings.MessageThreshold)
                {
                    // If the user's stats are successfully removed
                    if (spamSettings.UserStats.TryRemove(msg.Author.Id, out stats))
                    {
                        // Dispose the user's stats
                        stats.Dispose();
                        var settings = spamSettings.AntiSpamSettings;

                        // Punish the user
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

    /// <summary>
    /// Punishes a set of users based on the provided punishment action and protection type.
    /// </summary>
    /// <param name="action">The punishment action to be applied.</param>
    /// <param name="pt">The type of protection triggering the punishment.</param>
    /// <param name="muteTime">The duration of the mute punishment, if applicable.</param>
    /// <param name="roleId">The ID of the role to be added, if applicable.</param>
    /// <param name="gus">The users to be punished.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
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

    /// <summary>
    /// Starts the anti-raid protection for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to start the protection for.</param>
    /// <param name="userThreshold">The number of users that triggers the anti-raid protection.</param>
    /// <param name="seconds">The time period in seconds in which the user threshold must be reached to trigger the protection.</param>
    /// <param name="action">The punishment action to be applied when the protection is triggered.</param>
    /// <param name="minutesDuration">The duration of the punishment, if applicable.</param>
    /// <returns>A task that represents the asynchronous operation and contains the anti-raid stats if the protection was successfully started.</returns>
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

    /// <summary>
    /// Attempts to stop the anti-raid protection for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to stop the protection for.</param>
    /// <returns>A task that represents the asynchronous operation and contains a boolean indicating whether the operation was successful.</returns>
    public async Task<bool> TryStopAntiRaid(ulong guildId)
    {
        // If the anti-raid settings for the guild are successfully removed
        if (antiRaidGuilds.TryRemove(guildId, out _))
        {
            // Get the database context
            await using var uow = db.GetDbContext();
            // Get the guild configuration
            var gc = await uow.ForGuildId(guildId, set => set.Include(x => x.AntiRaidSetting));

            // Remove the anti-raid settings
            gc.AntiRaidSetting = null;
            // Save the changes to the database
            await uow.SaveChangesAsync().ConfigureAwait(false);

            // Return true to indicate success
            return true;
        }

        // Return false to indicate failure
        return false;
    }

    /// <summary>
    /// Attempts to stop the anti-spam protection for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to stop the protection for.</param>
    /// <returns>A task that represents the asynchronous operation and contains a boolean indicating whether the operation was successful.</returns>
    public async Task<bool> TryStopAntiSpam(ulong guildId)
    {
        // If the anti-spam settings for the guild are successfully removed
        if (antiSpamGuilds.TryRemove(guildId, out var removed))
        {
            // Dispose the user stats
            removed.UserStats.ForEach(x => x.Value.Dispose());
            // Get the database context
            await using var uow = db.GetDbContext();
            // Get the guild configuration
            var gc = await uow.ForGuildId(guildId, set => set.Include(x => x.AntiSpamSetting)
                .ThenInclude(x => x.IgnoredChannels));

            // Remove the anti-spam settings
            gc.AntiSpamSetting = null;
            // Save the changes to the database
            await uow.SaveChangesAsync().ConfigureAwait(false);

            // Return true to indicate success
            return true;
        }

        // Return false to indicate failure
        return false;
    }

    /// <summary>
    /// Starts the anti-spam protection for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to start the protection for.</param>
    /// <param name="messageCount">The number of messages that triggers the anti-spam protection.</param>
    /// <param name="action">The punishment action to be applied when the protection is triggered.</param>
    /// <param name="punishDurationMinutes">The duration of the punishment, if applicable.</param>
    /// <param name="roleId">The ID of the role to be added, if applicable.</param>
    /// <returns>A task that represents the asynchronous operation and contains the anti-spam stats if the protection was successfully started.</returns>
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

    /// <summary>
    /// Ignores a channel for the anti-spam protection in a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to ignore the channel for.</param>
    /// <param name="channelId">The ID of the channel to ignore.</param>
    /// <returns>A task that represents the asynchronous operation and contains a boolean indicating whether the operation was successful.</returns>
    public async Task<bool?> AntiSpamIgnoreAsync(ulong guildId, ulong channelId)
    {
        var obj = new AntiSpamIgnore
        {
            ChannelId = channelId
        };
        bool added;
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guildId,
            set => set.Include(x => x.AntiSpamSetting).ThenInclude(x => x.IgnoredChannels));
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

    /// <summary>
    /// Retrieves the anti-spam, anti-raid, and anti-alt statistics for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to retrieve the statistics for.</param>
    /// <returns>A tuple containing the anti-spam, anti-raid, and anti-alt statistics for the guild.</returns>
    public (AntiSpamStats?, AntiRaidStats?, AntiAltStats?) GetAntiStats(ulong guildId)
    {
        antiRaidGuilds.TryGetValue(guildId, out var antiRaidStats);
        antiSpamGuilds.TryGetValue(guildId, out var antiSpamStats);
        antiAltGuilds.TryGetValue(guildId, out var antiAltStats);

        return (antiSpamStats, antiRaidStats, antiAltStats);
    }

    /// <summary>
    /// Checks if a duration is allowed for a specific punishment action.
    /// </summary>
    /// <param name="action">The punishment action to check.</param>
    /// <returns>A boolean indicating whether a duration is allowed for the punishment action.</returns>
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

    /// <summary>
    /// Starts the anti-alt protection for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to start the protection for.</param>
    /// <param name="minAgeMinutes">The minimum age of an account to not be considered an alt.</param>
    /// <param name="action">The punishment action to be applied when the protection is triggered.</param>
    /// <param name="actionDurationMinutes">The duration of the punishment, if applicable.</param>
    /// <param name="roleId">The ID of the role to be added, if applicable.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task StartAntiAltAsync(ulong guildId, int minAgeMinutes, PunishmentAction action,
        int actionDurationMinutes = 0, ulong? roleId = null)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guildId, set => set.Include(x => x.AntiAltSetting));
        gc.AntiAltSetting = new AntiAltSetting
        {
            Action = action,
            ActionDurationMinutes = actionDurationMinutes,
            MinAge = minAgeMinutes.ToString(),
            RoleId = roleId
        };

        await uow.SaveChangesAsync().ConfigureAwait(false);
        antiAltGuilds[guildId] = new AntiAltStats(gc.AntiAltSetting);
    }

    /// <summary>
    /// Attempts to stop the anti-alt protection for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to stop the protection for.</param>
    /// <returns>A task that represents the asynchronous operation and contains a boolean indicating whether the operation was successful.</returns>
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