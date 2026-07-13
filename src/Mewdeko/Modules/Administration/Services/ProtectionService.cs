using System.Text.RegularExpressions;
using System.Threading;
using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Modules.Administration.Common;
using Mewdeko.Modules.Moderation.Services;
using Mewdeko.Services.Strings;

namespace Mewdeko.Modules.Administration.Services;

/// <summary>
///     Provides anti-alt, anti-raid, and antispam protection services.
/// </summary>
public class ProtectionService : INService, IReadyExecutor, IUnloadableService
{
    /// <summary>
    ///     The most images from a single message that will be downloaded and hashed.
    /// </summary>
    private const int MaxImagesPerMessage = 4;

    private readonly ConcurrentDictionary<ulong, AntiAltStats> antiAltGuilds = new();
    private readonly ConcurrentDictionary<ulong, AntiImageHashStats> antiImageHashGuilds = new();
    private readonly ConcurrentDictionary<ulong, AntiMassMentionStats> antiMassMentionGuilds = new();
    private readonly ConcurrentDictionary<ulong, AntiMassPostStats> antiMassPostGuilds = new();
    private readonly ConcurrentDictionary<ulong, AntiPatternStats> antiPatternGuilds = new();
    private readonly ConcurrentDictionary<ulong, AntiPostChannelStats> antiPostChannelGuilds = new();
    private readonly ConcurrentDictionary<ulong, AntiRaidStats> antiRaidGuilds = new();
    private readonly ConcurrentDictionary<ulong, AntiSpamStats> antiSpamGuilds = new();

    private readonly DiscordShardedClient client;
    private readonly IDataConnectionFactory dbFactory;
    private readonly EventHandler eventHandler;
    private readonly ImageHashingService imageHashing;
    private readonly ILogger<ProtectionService> logger;
    private readonly MuteService mute;
    private readonly UserPunishService punishService;

    private readonly Channel<PunishQueueItem> punishUserQueue =
        Channel.CreateBounded<PunishQueueItem>(new BoundedChannelOptions(200)
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false,
            FullMode = BoundedChannelFullMode.DropOldest
        });

    private readonly ScamImagePresetService scamPresets;

    private readonly GeneratedBotStrings strings;

    /// <summary>
    ///     Constructs a new instance of the ProtectionService.
    /// </summary>
    /// <param name="client">The Discord client.</param>
    /// <param name="mute">The mute service.</param>
    /// <param name="dbFactory">The database service.</param>
    /// <param name="punishService">The user punish service.</param>
    /// <param name="eventHandler">The event handler.</param>
    /// <param name="logger">The logger instance for structured logging.</param>
    /// <param name="strings">The localization strings service.</param>
    /// <param name="imageHashing">The perceptual image hashing service.</param>
    /// <param name="scamPresets">The known scam image hashes that ship with the bot.</param>
    public ProtectionService(DiscordShardedClient client,
        MuteService mute, IDataConnectionFactory dbFactory, UserPunishService punishService, EventHandler eventHandler,
        ILogger<ProtectionService> logger, GeneratedBotStrings strings, ImageHashingService imageHashing,
        ScamImagePresetService scamPresets)
    {
        this.client = client;
        this.mute = mute;
        this.dbFactory = dbFactory;
        this.punishService = punishService;
        this.logger = logger;
        this.eventHandler = eventHandler;
        this.strings = strings;
        this.imageHashing = imageHashing;
        this.scamPresets = scamPresets;

        eventHandler.Subscribe("MessageReceived", "ProtectionService", HandleAntiSpam);
        eventHandler.Subscribe("UserJoined", "ProtectionService", HandleUserJoined);
        eventHandler.Subscribe("MessageReceived", "ProtectionService", HandleAntiMassMention);
        eventHandler.Subscribe("MessageReceived", "ProtectionService", HandleAntiMassPost);
        eventHandler.Subscribe("MessageReceived", "ProtectionService", HandleAntiPostChannel);
        eventHandler.Subscribe("MessageReceived", "ProtectionService", HandleAntiImageHash);

        eventHandler.Subscribe("JoinedGuild", "ProtectionService", _bot_JoinedGuild);
        eventHandler.Subscribe("LeftGuild", "ProtectionService", _client_LeftGuild);

        _ = Task.Run(RunQueue);
    }

    /// <summary>
    ///     Gets the number of known scam images that ship with the bot.
    /// </summary>
    public int PresetScamImageCount
    {
        get
        {
            return scamPresets.Images.Count;
        }
    }

    /// <inheritdoc />
    public async Task OnReadyAsync()
    {
        foreach (var guild in client.Guilds)
        {
            try
            {
                await Initialize(guild.Id);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error initializing protections for Guild {GuildId}", guild.Id);
            }
        }
    }

    /// <summary>
    ///     Unloads the service and unsubscribes from events.
    /// </summary>
    public Task Unload()
    {
        eventHandler.Unsubscribe("MessageReceived", "ProtectionService", HandleAntiSpam);
        eventHandler.Unsubscribe("UserJoined", "ProtectionService", HandleUserJoined);
        eventHandler.Unsubscribe("MessageReceived", "ProtectionService", HandleAntiMassMention);
        eventHandler.Unsubscribe("MessageReceived", "ProtectionService", HandleAntiMassPost);
        eventHandler.Unsubscribe("MessageReceived", "ProtectionService", HandleAntiPostChannel);
        eventHandler.Unsubscribe("MessageReceived", "ProtectionService", HandleAntiImageHash);
        eventHandler.Unsubscribe("JoinedGuild", "ProtectionService", _bot_JoinedGuild);
        eventHandler.Unsubscribe("LeftGuild", "ProtectionService", _client_LeftGuild);
        return Task.CompletedTask;
    }

    /// <summary>
    ///     An event that is triggered when the anti-protection is triggered.
    /// </summary>
    public event Func<PunishmentAction, ProtectionType, IGuildUser[], Task> OnAntiProtectionTriggered = delegate
    {
        return Task.CompletedTask;
    };

    /// <summary>
    ///     The task that runs the punish queue.
    /// </summary>
    private async Task RunQueue()
    {
        while (true)
        {
            try
            {
                var item = await punishUserQueue.Reader.ReadAsync().ConfigureAwait(false);
                var muteTime = item.MuteTime;
                var gu = item.User;

                var currentUser = client.CurrentUser;
                if (currentUser == null)
                {
                    logger.LogWarning("Cannot apply punishment; CurrentUser is null.");
                    continue;
                }

                await punishService.ApplyPunishment(gu.Guild, gu, currentUser, (PunishmentAction)item.Action, muteTime,
                    item.RoleId, $"{item.Type} Protection").ConfigureAwait(false);

                await Task.Delay(1000);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error in punish queue: {Message}", ex.Message);
                await Task.Delay(5000);
            }
        }
    }


    /// <summary>
    ///     Handles the event when the bot leaves a guild.
    /// </summary>
    /// <param name="guild">The guild that the bot has left.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private Task _client_LeftGuild(SocketGuild guild)
    {
        antiRaidGuilds.TryRemove(guild.Id, out _);
        antiSpamGuilds.TryRemove(guild.Id, out _);
        antiAltGuilds.TryRemove(guild.Id, out _);
        antiMassMentionGuilds.TryRemove(guild.Id, out _);
        antiPatternGuilds.TryRemove(guild.Id, out _);
        antiMassPostGuilds.TryRemove(guild.Id, out _);
        antiPostChannelGuilds.TryRemove(guild.Id, out _);
        antiImageHashGuilds.TryRemove(guild.Id, out _);
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Handles the event when the bot joins a guild.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task _bot_JoinedGuild(SocketGuild guild)
    {
        await Initialize(guild.Id);
    }

    /// <summary>
    ///     Initializes the anti-raid, anti-spam, and anti-alt settings for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to initialize the settings for.</param>
    private async Task Initialize(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var raid = await db.GetTable<AntiRaidSetting>().FirstOrDefaultAsync(x => x.GuildId == guildId)
            .ConfigureAwait(false);
        var spam = await db.GetTable<AntiSpamSetting>().FirstOrDefaultAsync(x => x.GuildId == guildId)
            .ConfigureAwait(false);

        if (spam != null)
        {
            spam.AntiSpamIgnores = (await db.GetTable<AntiSpamIgnore>()
                .Where(i => i.AntiSpamSettingId == spam.Id)
                .ToListAsync().ConfigureAwait(false)).ToHashSet();
        }

        var alt = await db.GetTable<AntiAltSetting>().FirstOrDefaultAsync(x => x.GuildId == guildId)
            .ConfigureAwait(false);
        var mention = await db.GetTable<AntiMassMentionSetting>().FirstOrDefaultAsync(x => x.GuildId == guildId)
            .ConfigureAwait(false);
        var pattern = await db.GetTable<AntiPatternSetting>().FirstOrDefaultAsync(x => x.GuildId == guildId)
            .ConfigureAwait(false);

        if (raid != null)
            antiRaidGuilds[guildId] = new AntiRaidStats
            {
                AntiRaidSettings = raid
            };
        else antiRaidGuilds.TryRemove(guildId, out _);

        if (spam != null)
            antiSpamGuilds[guildId] = new AntiSpamStats
            {
                AntiSpamSettings = spam
            };
        else antiSpamGuilds.TryRemove(guildId, out _);

        if (alt != null) antiAltGuilds[guildId] = new AntiAltStats(alt);
        else antiAltGuilds.TryRemove(guildId, out _);

        if (mention != null)
            antiMassMentionGuilds[guildId] = new AntiMassMentionStats
            {
                AntiMassMentionSettings = mention
            };
        else antiMassMentionGuilds.TryRemove(guildId, out _);

        if (pattern != null)
        {
            pattern.AntiPatternPatterns = (await db.GetTable<AntiPatternPattern>()
                .Where(p => p.AntiPatternSettingId == pattern.Id)
                .ToListAsync().ConfigureAwait(false)).ToHashSet();
            antiPatternGuilds[guildId] = new AntiPatternStats(pattern);
        }
        else antiPatternGuilds.TryRemove(guildId, out _);

        // Load anti-mass-post settings
        var massPost = await db.GetTable<AntiMassPostSetting>().FirstOrDefaultAsync(x => x.GuildId == guildId)
            .ConfigureAwait(false);

        if (massPost != null)
        {
            massPost.AntiMassPostIgnoredRoles = (await db.GetTable<AntiMassPostIgnoredRole>()
                .Where(r => r.AntiMassPostSettingId == massPost.Id)
                .ToListAsync().ConfigureAwait(false)).ToHashSet();
            massPost.AntiMassPostIgnoredUsers = (await db.GetTable<AntiMassPostIgnoredUser>()
                .Where(u => u.AntiMassPostSettingId == massPost.Id)
                .ToListAsync().ConfigureAwait(false)).ToHashSet();
            massPost.AntiMassPostIgnoredChannels = (await db.GetTable<AntiMassPostIgnoredChannel>()
                .Where(c => c.AntiMassPostSettingId == massPost.Id)
                .ToListAsync().ConfigureAwait(false)).ToHashSet();
            massPost.AntiMassPostLinkWhitelists = (await db.GetTable<AntiMassPostLinkWhitelist>()
                .Where(w => w.AntiMassPostSettingId == massPost.Id)
                .ToListAsync().ConfigureAwait(false)).ToHashSet();
            massPost.AntiMassPostLinkBlacklists = (await db.GetTable<AntiMassPostLinkBlacklist>()
                .Where(b => b.AntiMassPostSettingId == massPost.Id)
                .ToListAsync().ConfigureAwait(false)).ToHashSet();
            antiMassPostGuilds[guildId] = new AntiMassPostStats(massPost);
        }
        else antiMassPostGuilds.TryRemove(guildId, out _);

        // Load anti-post-channel settings
        var postChannel = await db.GetTable<AntiPostChannelSetting>().FirstOrDefaultAsync(x => x.GuildId == guildId)
            .ConfigureAwait(false);

        if (postChannel != null)
        {
            postChannel.AntiPostChannelChannels = (await db.GetTable<AntiPostChannelChannel>()
                .Where(c => c.AntiPostChannelSettingId == postChannel.Id)
                .ToListAsync().ConfigureAwait(false)).ToHashSet();
            postChannel.AntiPostChannelIgnoredRoles = (await db.GetTable<AntiPostChannelIgnoredRole>()
                .Where(r => r.AntiPostChannelSettingId == postChannel.Id)
                .ToListAsync().ConfigureAwait(false)).ToHashSet();
            postChannel.AntiPostChannelIgnoredUsers = (await db.GetTable<AntiPostChannelIgnoredUser>()
                .Where(u => u.AntiPostChannelSettingId == postChannel.Id)
                .ToListAsync().ConfigureAwait(false)).ToHashSet();
            antiPostChannelGuilds[guildId] = new AntiPostChannelStats(postChannel);
        }
        else antiPostChannelGuilds.TryRemove(guildId, out _);

        // Load anti-image-hash settings
        var imageHash = await db.GetTable<AntiImageHashSetting>().FirstOrDefaultAsync(x => x.GuildId == guildId)
            .ConfigureAwait(false);

        if (imageHash != null)
        {
            var hashes = await db.GetTable<BannedImageHash>()
                .Where(h => h.GuildId == guildId)
                .ToListAsync().ConfigureAwait(false);

            var ignoredRoles = await db.GetTable<AntiImageHashIgnoredRole>()
                .Where(r => r.GuildId == guildId)
                .Select(r => r.RoleId)
                .ToListAsync().ConfigureAwait(false);

            var ignoredChannels = await db.GetTable<AntiImageHashIgnoredChannel>()
                .Where(c => c.GuildId == guildId)
                .Select(c => c.ChannelId)
                .ToListAsync().ConfigureAwait(false);

            antiImageHashGuilds[guildId] = new AntiImageHashStats(imageHash)
            {
                Hashes = hashes.Select(ToBlockedImageHash).Where(h => h.Hashes.Count > 0).ToList(),
                IgnoredRoles = ignoredRoles.ToHashSet(),
                IgnoredChannels = ignoredChannels.ToHashSet()
            };
        }
        else antiImageHashGuilds.TryRemove(guildId, out _);
    }

    /// <summary>
    ///     Handles the event when a user joins a guild.
    /// </summary>
    /// <param name="user">The user that has joined the guild.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task HandleUserJoined(IGuildUser user)
    {
        if (user.IsBot) return;

        antiRaidGuilds.TryGetValue(user.Guild.Id, out var raidStats);
        antiAltGuilds.TryGetValue(user.Guild.Id, out var altStats);
        antiPatternGuilds.TryGetValue(user.Guild.Id, out var patternStats);

        if (raidStats is null && altStats is null && patternStats is null) return;

        if (altStats is { } alts && altStats.Action != (int)PunishmentAction.Warn)
        {
            if (user.CreatedAt != default)
            {
                var diff = DateTime.UtcNow - user.CreatedAt.UtcDateTime;
                if (double.TryParse(alts.MinAge, out var minAgeMinutes))
                {
                    var minAgeSpan = TimeSpan.FromMinutes(minAgeMinutes);
                    if (diff < minAgeSpan)
                    {
                        await PunishUsers(alts.Action, ProtectionType.Alting, alts.ActionDurationMinutes, alts.RoleId,
                            user).ConfigureAwait(false);
                        return;
                    }
                }
            }
        }

        if (patternStats is { } patterns && patterns.Action != (int)PunishmentAction.Warn)
        {
            try
            {
                var username = user.Username?.ToLower() ?? "";
                var displayName = user.DisplayName?.ToLower() ?? "";
                var settings = patterns.AntiPatternSettings;
                var score = 0;
                var reasons = new List<string>();
                var now = DateTimeOffset.UtcNow;

                // Account age check
                if (settings.CheckAccountAge)
                {
                    var accountAge = now - user.CreatedAt;
                    if (accountAge.TotalDays <= settings.MaxAccountAgeMonths * 30)
                    {
                        score += 5;
                        reasons.Add($"AccountAge({accountAge.TotalDays:F1}d)");
                    }
                }

                // Join timing check
                if (settings.CheckJoinTiming && user.JoinedAt.HasValue)
                {
                    var timeBetween = (user.JoinedAt.Value - user.CreatedAt).TotalHours;
                    if (timeBetween <= settings.MaxJoinHours)
                    {
                        score += timeBetween < 1 ? 10 : timeBetween < 6 ? 7 : 3;
                        reasons.Add($"QuickJoin({timeBetween:F1}h)");
                    }
                }

                // Batch creation check
                if (settings.CheckBatchCreation)
                {
                    var guild = user.Guild;
                    var creationHour = user.CreatedAt.ToString("yyyy-MM-dd HH");
                    var recentUsers = await guild.GetUsersAsync();
                    var batchCount = recentUsers.Count(u => !u.IsBot &&
                                                            u.CreatedAt.ToString("yyyy-MM-dd HH") == creationHour);
                    if (batchCount > 1)
                    {
                        score += Math.Min(batchCount, 10);
                        reasons.Add($"Batch({batchCount})");
                    }
                }

                // Offline status check
                if (settings.CheckOfflineStatus && user.Status == UserStatus.Offline)
                {
                    score += 2;
                    reasons.Add("Offline");
                }

                // New account check
                if (settings.CheckNewAccounts)
                {
                    var accountAge = (now - user.CreatedAt).TotalDays;
                    if (accountAge < settings.NewAccountDays)
                    {
                        score += 3;
                        reasons.Add($"NewAccount({accountAge:F1}d)");
                    }
                }

                // Pattern matching
                foreach (var pattern in patterns.AntiPatternSettings.AntiPatternPatterns)
                {
                    var regex = new Regex(pattern.Pattern, RegexOptions.IgnoreCase);

                    var isMatch = false;
                    if (pattern.CheckUsername && regex.IsMatch(username))
                    {
                        isMatch = true;
                        score += 15;
                        reasons.Add($"UsernamePattern({pattern.Name ?? "Unnamed"})");
                    }

                    if (pattern.CheckDisplayName && regex.IsMatch(displayName))
                    {
                        isMatch = true;
                        score += 12;
                        reasons.Add($"DisplayNamePattern({pattern.Name ?? "Unnamed"})");
                    }

                    if (isMatch && score >= settings.MinimumScore)
                    {
                        patterns.Increment();
                        await PunishUsers(patterns.Action, ProtectionType.PatternMatching, patterns.PunishDuration,
                            patterns.RoleId, user).ConfigureAwait(false);
                        logger.LogInformation(
                            "Anti-pattern triggered for user {UserId} ({Username}) - Score: {Score}, Reasons: {Reasons}",
                            user.Id, user.Username, score, string.Join("|", reasons));
                        return;
                    }
                }

                // Check if overall score meets threshold without pattern match
                if (score >= settings.MinimumScore && reasons.Any())
                {
                    patterns.Increment();
                    await PunishUsers(patterns.Action, ProtectionType.PatternMatching, patterns.PunishDuration,
                        patterns.RoleId, user).ConfigureAwait(false);
                    logger.LogInformation(
                        "Anti-pattern triggered for user {UserId} ({Username}) - Score: {Score}, Reasons: {Reasons}",
                        user.Id, user.Username, score, string.Join("|", reasons));
                    return;
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error processing anti-pattern for user {UserId}", user.Id);
            }
        }

        if (raidStats is { } stats && stats.AntiRaidSettings.Action != (int)PunishmentAction.Warn)
        {
            try
            {
                stats.RaidUsers.Add(user); // Add user to the collection

                var statsUsersCount = stats.UsersCount;
                var currentCount = Interlocked.Increment(ref statsUsersCount);

                if (currentCount >= stats.AntiRaidSettings.UserThreshold)
                {
                    var usersToPunish = stats.RaidUsers.ToList();
                    stats.RaidUsers.Clear();
                    Interlocked.Add(ref statsUsersCount, -usersToPunish.Count);

                    if (usersToPunish.Any())
                    {
                        var settings = stats.AntiRaidSettings;
                        await PunishUsers(settings.Action, ProtectionType.Raiding, settings.PunishDuration, null,
                            usersToPunish.Where(u => u != null).ToArray()).ConfigureAwait(false);
                    }
                }
                else
                {
                    // Schedule count decrement after delay
                    _ = Task.Delay(TimeSpan.FromSeconds(stats.AntiRaidSettings.Seconds)).ContinueWith(_ =>
                    {
                        // Just decrement the count after the delay
                        Interlocked.Decrement(ref statsUsersCount);
                    });
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error processing anti-raid for user {UserId}", user.Id);
            }
        }
    }

    /// <summary>
    ///     Handles the event when a message is received in a guild for anti-spam protection.
    /// </summary>
    /// <param name="arg">The message that was received.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private Task HandleAntiSpam(IMessage arg)
    {
        if (arg is not SocketUserMessage msg || msg.Author.IsBot || msg.Author is IGuildUser
            {
                GuildPermissions.Administrator: true
            })
            return Task.CompletedTask;

        if (msg.Channel is not ITextChannel channel)
            return Task.CompletedTask;

        if (!antiSpamGuilds.TryGetValue(channel.Guild.Id, out var spamStats))
            return Task.CompletedTask;

        if (spamStats.AntiSpamSettings.AntiSpamIgnores.Any(i => i.ChannelId == channel.Id))
            return Task.CompletedTask;

        _ = Task.Run(async () =>
        {
            try
            {
                var stats = spamStats.UserStats.AddOrUpdate(msg.Author.Id, _ => new UserSpamStats(msg), (_, old) =>
                {
                    old.ApplyNextMessage(msg);
                    return old;
                });

                if (stats.Count >= spamStats.AntiSpamSettings.MessageThreshold)
                {
                    if (spamStats.UserStats.TryRemove(msg.Author.Id, out var removedStats))
                    {
                        removedStats.Dispose();
                        var settings = spamStats.AntiSpamSettings;
                        await PunishUsers(settings.Action, ProtectionType.Spamming, settings.MuteTime, settings.RoleId,
                            (IGuildUser)msg.Author).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error processing anti-spam for user {UserId}", msg.Author.Id);
            }
        });
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Punishes a set of users based on the provided punishment action and protection type.
    /// </summary>
    /// <param name="action">The punishment action to be applied.</param>
    /// <param name="pt">The type of protection triggering the punishment.</param>
    /// <param name="muteTime">The duration of the mute punishment, if applicable.</param>
    /// <param name="roleId">The ID of the role to be added, if applicable.</param>
    /// <param name="gus">The users to be punished.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task PunishUsers(int action, ProtectionType pt, int muteTime, ulong? roleId, params IGuildUser[]? gus)
    {
        if (gus == null || gus.Length == 0) return;

        logger.LogInformation("[{PunishType}] - Punishing [{Count}] users with [{PunishAction}] in {GuildName} guild",
            pt,
            gus.Length, action, gus[0].Guild.Name);

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

        _ = OnAntiProtectionTriggered((PunishmentAction)action, pt, gus);
    }

    /// <summary>
    ///     Starts the anti-raid protection for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to start the protection for.</param>
    /// <param name="userThreshold">The number of users that triggers the anti-raid protection.</param>
    /// <param name="seconds">The time period in seconds in which the user threshold must be reached to trigger the protection.</param>
    /// <param name="action">The punishment action to be applied when the protection is triggered.</param>
    /// <param name="minutesDuration">The duration of the punishment, if applicable.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation and contains the anti-raid stats if the protection was
    ///     successfully started.
    /// </returns>
    public async Task<AntiRaidStats?> StartAntiRaidAsync(ulong guildId, int userThreshold, int seconds,
        PunishmentAction action, int minutesDuration)
    {
        var g = client.GetGuild(guildId);
        if (g == null) return null;
        await mute.GetMuteRole(g).ConfigureAwait(false);

        if (action == PunishmentAction.AddRole) return null;

        if (!IsDurationAllowed(action)) minutesDuration = 0;

        await using var db = await dbFactory.CreateConnectionAsync();
        var settings = await db.GetTable<AntiRaidSetting>().FirstOrDefaultAsync(x => x.GuildId == guildId)
            .ConfigureAwait(false);
        var isNew = settings == null;
        settings ??= new AntiRaidSetting
        {
            GuildId = guildId
        };

        settings.Action = (int)action;
        settings.Seconds = seconds;
        settings.UserThreshold = userThreshold;
        settings.PunishDuration = minutesDuration;

        if (isNew)
            await db.InsertAsync(settings).ConfigureAwait(false);
        else
            await db.UpdateAsync(settings).ConfigureAwait(false);

        var stats = new AntiRaidStats
        {
            AntiRaidSettings = settings
        };
        antiRaidGuilds.AddOrUpdate(guildId, stats, (_, _) => stats);

        return stats;
    }

    /// <summary>
    ///     Handles the event when a message is received for anti-mass mention protection.
    /// </summary>
    /// <param name="arg">The message that was received.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private Task HandleAntiMassMention(IMessage arg)
    {
        if (arg is not SocketUserMessage msg || msg.Author is IGuildUser { GuildPermissions.Administrator: true })
            return Task.CompletedTask;

        if (msg.Channel is not ITextChannel channel)
            return Task.CompletedTask;

        if (!antiMassMentionGuilds.TryGetValue(channel.Guild.Id, out var massMentionStats))
            return Task.CompletedTask;

        var settings = massMentionStats.AntiMassMentionSettings;
        if (settings.IgnoreBots && msg.Author.IsBot)
            return Task.CompletedTask;

        var mentionCount = msg.MentionedUsers.Count + msg.MentionedRoles.Count;
        if (mentionCount == 0) return Task.CompletedTask;


        _ = Task.Run(async () =>
        {
            try
            {
                if (mentionCount >= settings.MentionThreshold)
                {
                    await PunishUsers(settings.Action, ProtectionType.MassMention, settings.MuteTime, settings.RoleId,
                        (IGuildUser)msg.Author).ConfigureAwait(false);
                    if (massMentionStats.UserStats.TryRemove(msg.Author.Id, out var removedStats))
                        removedStats.Dispose();
                    return;
                }

                var userStats = massMentionStats.UserStats.AddOrUpdate(msg.Author.Id,
                    _ => new UserMentionStats(settings.TimeWindowSeconds), (_, old) => old);

                if (userStats.AddMentions(mentionCount, settings.MaxMentionsInTimeWindow))
                {
                    await PunishUsers(settings.Action, ProtectionType.MassMention, settings.MuteTime, settings.RoleId,
                        (IGuildUser)msg.Author).ConfigureAwait(false);
                    if (massMentionStats.UserStats.TryRemove(msg.Author.Id, out var removedStats))
                        removedStats.Dispose();
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error processing anti-mass-mention for user {UserId}", msg.Author.Id);
            }
        });
        return Task.CompletedTask;
    }


    /// <summary>
    ///     Attempts to stop the anti-raid protection for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to stop the protection for.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation and contains a boolean indicating whether the operation was
    ///     successful.
    /// </returns>
    public async Task<bool> TryStopAntiRaid(ulong guildId)
    {
        var removed = antiRaidGuilds.TryRemove(guildId, out _);
        await using var db = await dbFactory.CreateConnectionAsync();
        var deletedCount = await db.GetTable<AntiRaidSetting>()
            .Where(x => x.GuildId == guildId)
            .DeleteAsync().ConfigureAwait(false);
        return removed || deletedCount > 0;
    }

    /// <summary>
    ///     Attempts to stop the anti-spam protection for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to stop the protection for.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation and contains a boolean indicating whether the operation was
    ///     successful.
    /// </returns>
    public async Task<bool> TryStopAntiSpam(ulong guildId)
    {
        var removed = antiSpamGuilds.TryRemove(guildId, out var removedStats);
        if (removed) removedStats.UserStats.ForEach(x => x.Value.Dispose());

        await using var db = await dbFactory.CreateConnectionAsync();
        var setting = await db.GetTable<AntiSpamSetting>().FirstOrDefaultAsync(x => x.GuildId == guildId)
            .ConfigureAwait(false);
        var deletedCount = 0;
        if (setting != null)
        {
            await db.GetTable<AntiSpamIgnore>().Where(i => i.AntiSpamSettingId == setting.Id).DeleteAsync()
                .ConfigureAwait(false);
            // Use DeleteAsync with the fetched entity for single deletion
            deletedCount = await db.DeleteAsync(setting).ConfigureAwait(false);
        }

        return removed || deletedCount > 0;
    }

    /// <summary>
    ///     Starts the anti-spam protection for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to start the protection for.</param>
    /// <param name="messageCount">The number of messages that triggers the anti-spam protection.</param>
    /// <param name="action">The punishment action to be applied when the protection is triggered.</param>
    /// <param name="punishDurationMinutes">The duration of the punishment, if applicable.</param>
    /// <param name="roleId">The ID of the role to be added, if applicable.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation and contains the anti-spam stats if the protection was
    ///     successfully started.
    /// </returns>
    public async Task<AntiSpamStats?> StartAntiSpamAsync(ulong guildId, int messageCount, PunishmentAction action,
        int punishDurationMinutes, ulong? roleId)
    {
        var g = client.GetGuild(guildId);
        if (g == null) return null;
        await mute.GetMuteRole(g).ConfigureAwait(false);

        if (!IsDurationAllowed(action)) punishDurationMinutes = 0;

        await using var db = await dbFactory.CreateConnectionAsync();
        var settings = await db.GetTable<AntiSpamSetting>().FirstOrDefaultAsync(x => x.GuildId == guildId)
            .ConfigureAwait(false);
        var isNew = settings == null;
        settings ??= new AntiSpamSetting
        {
            GuildId = guildId
        };

        settings.Action = (int)action;
        settings.MessageThreshold = messageCount;
        settings.MuteTime = punishDurationMinutes;
        settings.RoleId = roleId;

        if (isNew)
            await db.InsertAsync(settings).ConfigureAwait(false);
        else
            await db.UpdateAsync(settings).ConfigureAwait(false);

        // Reload ignored channels after insert/update for consistency
        settings.AntiSpamIgnores = (await db.GetTable<AntiSpamIgnore>()
            .Where(i => i.AntiSpamSettingId == settings.Id)
            .ToListAsync().ConfigureAwait(false)).ToHashSet();

        var stats = new AntiSpamStats
        {
            AntiSpamSettings = settings
        };
        antiSpamGuilds.AddOrUpdate(guildId, stats, (_, _) => stats);

        return stats;
    }

    /// <summary>
    ///     Starts the anti-mass mention protection for a guild with the specified settings.
    /// </summary>
    /// <param name="guildId">The ID of the guild to start the protection for.</param>
    /// <param name="mentionThreshold">The number of mentions allowed in a single message before triggering protection.</param>
    /// <param name="timeWindowSeconds">The time window in seconds during which mentions are tracked.</param>
    /// <param name="maxMentionsInTimeWindow">
    ///     The maximum number of mentions allowed within the specified time window before
    ///     triggering protection.
    /// </param>
    /// <param name="ignoreBots">Whether to ignore bots.</param>
    /// <param name="action">The punishment action to be applied when the protection is triggered.</param>
    /// <param name="muteTime">The duration of the mute punishment in minutes, if applicable.</param>
    /// <param name="roleId">The ID of the role to be assigned as punishment, if applicable.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task StartAntiMassMentionAsync(ulong guildId, int mentionThreshold, int timeWindowSeconds,
        int maxMentionsInTimeWindow, bool ignoreBots, PunishmentAction action, int muteTime, ulong? roleId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var settings = await db.GetTable<AntiMassMentionSetting>().FirstOrDefaultAsync(x => x.GuildId == guildId)
            .ConfigureAwait(false);
        var isNew = settings == null;
        settings ??= new AntiMassMentionSetting
        {
            GuildId = guildId
        };

        settings.MentionThreshold = mentionThreshold;
        settings.TimeWindowSeconds = timeWindowSeconds;
        settings.MaxMentionsInTimeWindow = maxMentionsInTimeWindow;
        settings.IgnoreBots = ignoreBots;
        settings.Action = (int)action;
        settings.MuteTime = muteTime;
        settings.RoleId = roleId;

        if (isNew)
            await db.InsertAsync(settings).ConfigureAwait(false);
        else
            await db.UpdateAsync(settings).ConfigureAwait(false);

        var stats = new AntiMassMentionStats
        {
            AntiMassMentionSettings = settings
        };
        antiMassMentionGuilds.AddOrUpdate(guildId, stats, (_, _) => stats);
    }

    /// <summary>
    ///     Attempts to stop the anti-mass mention protection for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to stop the protection for.</param>
    /// <returns>
    ///     A task representing the asynchronous operation. Returns true if the protection was successfully stopped;
    ///     otherwise, false.
    /// </returns>
    public async Task<bool> TryStopAntiMassMention(ulong guildId)
    {
        var removed = antiMassMentionGuilds.TryRemove(guildId, out var removedStats);
        if (removed) removedStats.UserStats.ForEach(x => x.Value.Dispose());

        await using var db = await dbFactory.CreateConnectionAsync();
        var deletedCount = await db.GetTable<AntiMassMentionSetting>()
            .Where(x => x.GuildId == guildId)
            .DeleteAsync().ConfigureAwait(false);

        return removed || deletedCount > 0;
    }

    /// <summary>
    ///     Ignores a channel for the anti-spam protection in a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to ignore the channel for.</param>
    /// <param name="channelId">The ID of the channel to ignore.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation and contains a boolean indicating whether the operation was
    ///     successful (true if added, false if removed). Returns null if spam settings don't exist.
    /// </returns>
    public async Task<bool?> AntiSpamIgnoreAsync(ulong guildId, ulong channelId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var spamSettingId = await db.GetTable<AntiSpamSetting>()
            .Where(x => x.GuildId == guildId)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync().ConfigureAwait(false);

        if (spamSettingId is null)
        {
            logger.LogWarning("Attempted to modify AntiSpamIgnore for non-existent AntiSpamSetting GuildId: {GuildId}",
                guildId);
            return null;
        }

        var deletedCount = await db.GetTable<AntiSpamIgnore>()
            .Where(i => i.AntiSpamSettingId == spamSettingId.Value && i.ChannelId == channelId)
            .DeleteAsync().ConfigureAwait(false);

        bool added;
        if (deletedCount > 0)
        {
            added = false;
        }
        else
        {
            var newIgnore = new AntiSpamIgnore
            {
                AntiSpamSettingId = spamSettingId.Value, ChannelId = channelId
            };
            await db.InsertAsync(newIgnore).ConfigureAwait(false);
            added = true;
        }

        var updatedSpamSetting = await db.GetTable<AntiSpamSetting>()
            .FirstOrDefaultAsync(x => x.Id == spamSettingId.Value)
            .ConfigureAwait(false);

        if (updatedSpamSetting != null)
        {
            updatedSpamSetting.AntiSpamIgnores = (await db.GetTable<AntiSpamIgnore>()
                .Where(i => i.AntiSpamSettingId == updatedSpamSetting.Id)
                .ToListAsync().ConfigureAwait(false)).ToHashSet();

            var newStats = new AntiSpamStats
            {
                AntiSpamSettings = updatedSpamSetting
            };
            antiSpamGuilds.AddOrUpdate(guildId, newStats, (_, _) => newStats);
        }
        else
        {
            // Setting was somehow deleted between steps, remove from cache
            antiSpamGuilds.TryRemove(guildId, out _);
        }

        return added;
    }

    /// <summary>
    ///     Retrieves the anti-spam, anti-raid, anti-alt, anti-mass-mention, anti-pattern, anti-mass-post, and anti-post-channel statistics for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to retrieve the statistics for.</param>
    /// <returns>A tuple containing all protection statistics for the guild.</returns>
    public (AntiSpamStats?, AntiRaidStats?, AntiAltStats?, AntiMassMentionStats?, AntiPatternStats?, AntiMassPostStats?,
        AntiPostChannelStats?)
        GetAntiStats(ulong guildId)
    {
        antiSpamGuilds.TryGetValue(guildId, out var antiSpamStats);
        antiRaidGuilds.TryGetValue(guildId, out var antiRaidStats);
        antiAltGuilds.TryGetValue(guildId, out var antiAltStats);
        antiMassMentionGuilds.TryGetValue(guildId, out var antiMassMentionStats);
        antiPatternGuilds.TryGetValue(guildId, out var antiPatternStats);
        antiMassPostGuilds.TryGetValue(guildId, out var antiMassPostStats);
        antiPostChannelGuilds.TryGetValue(guildId, out var antiPostChannelStats);
        return (antiSpamStats, antiRaidStats, antiAltStats, antiMassMentionStats, antiPatternStats, antiMassPostStats,
            antiPostChannelStats);
    }

    /// <summary>
    ///     Checks if a duration is allowed for a specific punishment action.
    /// </summary>
    /// <param name="action">The punishment action to check.</param>
    /// <returns>A boolean indicating whether a duration is allowed for the punishment action.</returns>
    public static bool IsDurationAllowed(PunishmentAction action)
    {
        return action switch
        {
            PunishmentAction.Ban => true, PunishmentAction.Mute => true, PunishmentAction.ChatMute => true,
            PunishmentAction.VoiceMute => true, PunishmentAction.AddRole => true, PunishmentAction.Timeout => true,
            _ => false
        };
    }

    /// <summary>
    ///     Starts the anti-alt protection for a guild.
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
        if (!IsDurationAllowed(action)) actionDurationMinutes = 0;

        await using var db = await dbFactory.CreateConnectionAsync();
        var settings = await db.GetTable<AntiAltSetting>().FirstOrDefaultAsync(x => x.GuildId == guildId)
            .ConfigureAwait(false);
        var isNew = settings == null;
        settings ??= new AntiAltSetting
        {
            GuildId = guildId
        };

        settings.Action = (int)action;
        settings.ActionDurationMinutes = actionDurationMinutes;
        settings.MinAge = minAgeMinutes.ToString();
        settings.RoleId = roleId;

        if (isNew)
            await db.InsertAsync(settings).ConfigureAwait(false);
        else
            await db.UpdateAsync(settings).ConfigureAwait(false);

        antiAltGuilds[guildId] = new AntiAltStats(settings);
    }

    /// <summary>
    ///     Attempts to stop the anti-alt protection for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to stop the protection for.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation and contains a boolean indicating whether the operation was
    ///     successful.
    /// </returns>
    public async Task<bool> TryStopAntiAlt(ulong guildId)
    {
        var removed = antiAltGuilds.TryRemove(guildId, out _);
        await using var db = await dbFactory.CreateConnectionAsync();
        var deletedCount = await db.GetTable<AntiAltSetting>()
            .Where(x => x.GuildId == guildId)
            .DeleteAsync().ConfigureAwait(false);
        return removed || deletedCount > 0;
    }

    /// <summary>
    ///     Starts the anti-pattern protection for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to start the protection for.</param>
    /// <param name="action">The punishment action to be applied when the protection is triggered.</param>
    /// <param name="actionDurationMinutes">The duration of the punishment, if applicable.</param>
    /// <param name="roleId">The ID of the role to be added, if applicable.</param>
    /// <param name="checkAccountAge">Whether to check account age.</param>
    /// <param name="maxAccountAgeMonths">Maximum account age in months to flag.</param>
    /// <param name="checkJoinTiming">Whether to check join timing.</param>
    /// <param name="maxJoinHours">Maximum hours between account creation and join.</param>
    /// <param name="checkBatchCreation">Whether to check for batch account creation.</param>
    /// <param name="checkOfflineStatus">Whether to check if user is offline.</param>
    /// <param name="checkNewAccounts">Whether to flag very new accounts.</param>
    /// <param name="newAccountDays">Days to consider an account as new.</param>
    /// <param name="minimumScore">Minimum score to trigger punishment.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<AntiPatternStats?> StartAntiPatternAsync(ulong guildId, PunishmentAction action,
        int actionDurationMinutes = 0, ulong? roleId = null, bool checkAccountAge = false, int maxAccountAgeMonths = 6,
        bool checkJoinTiming = false, double maxJoinHours = 48.0, bool checkBatchCreation = false,
        bool checkOfflineStatus = false, bool checkNewAccounts = false, int newAccountDays = 7, int minimumScore = 15)
    {
        if (!IsDurationAllowed(action)) actionDurationMinutes = 0;

        await using var db = await dbFactory.CreateConnectionAsync();
        var settings = await db.GetTable<AntiPatternSetting>().FirstOrDefaultAsync(x => x.GuildId == guildId)
            .ConfigureAwait(false);
        var isNew = settings == null;
        settings ??= new AntiPatternSetting
        {
            GuildId = guildId,
            CheckAccountAge = checkAccountAge,
            MaxAccountAgeMonths = maxAccountAgeMonths,
            CheckJoinTiming = checkJoinTiming,
            MaxJoinHours = maxJoinHours,
            CheckBatchCreation = checkBatchCreation,
            CheckOfflineStatus = checkOfflineStatus,
            CheckNewAccounts = checkNewAccounts,
            NewAccountDays = newAccountDays,
            MinimumScore = minimumScore,
            DateAdded = DateTime.UtcNow
        };

        settings.Action = (int)action;
        settings.PunishDuration = actionDurationMinutes;
        settings.RoleId = roleId;

        if (isNew)
            await db.InsertAsync(settings).ConfigureAwait(false);
        else
            await db.UpdateAsync(settings).ConfigureAwait(false);

        // Load existing patterns
        settings.AntiPatternPatterns = (await db.GetTable<AntiPatternPattern>()
            .Where(p => p.AntiPatternSettingId == settings.Id)
            .ToListAsync().ConfigureAwait(false)).ToHashSet();

        var stats = new AntiPatternStats(settings);
        antiPatternGuilds[guildId] = stats;

        return stats;
    }

    /// <summary>
    ///     Updates the anti-pattern configuration settings for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to update settings for.</param>
    /// <param name="checkAccountAge">Whether to check account age.</param>
    /// <param name="maxAccountAgeMonths">Maximum account age in months to flag.</param>
    /// <param name="checkJoinTiming">Whether to check join timing.</param>
    /// <param name="maxJoinHours">Maximum hours between account creation and join.</param>
    /// <param name="checkBatchCreation">Whether to check for batch account creation.</param>
    /// <param name="checkOfflineStatus">Whether to check if user is offline.</param>
    /// <param name="checkNewAccounts">Whether to flag very new accounts.</param>
    /// <param name="newAccountDays">Days to consider an account as new.</param>
    /// <param name="minimumScore">Minimum score to trigger punishment.</param>
    /// <returns>A task that represents the asynchronous operation and contains a boolean indicating success.</returns>
    public async Task<bool> UpdateAntiPatternConfigAsync(ulong guildId, bool? checkAccountAge = null,
        int? maxAccountAgeMonths = null, bool? checkJoinTiming = null, double? maxJoinHours = null,
        bool? checkBatchCreation = null, bool? checkOfflineStatus = null, bool? checkNewAccounts = null,
        int? newAccountDays = null, int? minimumScore = null)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var settings = await db.GetTable<AntiPatternSetting>().FirstOrDefaultAsync(x => x.GuildId == guildId)
            .ConfigureAwait(false);

        if (settings == null) return false;

        if (checkAccountAge.HasValue) settings.CheckAccountAge = checkAccountAge.Value;
        if (maxAccountAgeMonths.HasValue) settings.MaxAccountAgeMonths = maxAccountAgeMonths.Value;
        if (checkJoinTiming.HasValue) settings.CheckJoinTiming = checkJoinTiming.Value;
        if (maxJoinHours.HasValue) settings.MaxJoinHours = maxJoinHours.Value;
        if (checkBatchCreation.HasValue) settings.CheckBatchCreation = checkBatchCreation.Value;
        if (checkOfflineStatus.HasValue) settings.CheckOfflineStatus = checkOfflineStatus.Value;
        if (checkNewAccounts.HasValue) settings.CheckNewAccounts = checkNewAccounts.Value;
        if (newAccountDays.HasValue) settings.NewAccountDays = newAccountDays.Value;
        if (minimumScore.HasValue) settings.MinimumScore = minimumScore.Value;

        await db.UpdateAsync(settings).ConfigureAwait(false);

        // Refresh the cache
        await Initialize(guildId);

        return true;
    }

    /// <summary>
    ///     Attempts to stop the anti-pattern protection for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to stop the protection for.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation and contains a boolean indicating whether the operation was
    ///     successful.
    /// </returns>
    public async Task<bool> TryStopAntiPattern(ulong guildId)
    {
        var removed = antiPatternGuilds.TryRemove(guildId, out _);
        await using var db = await dbFactory.CreateConnectionAsync();

        var setting = await db.GetTable<AntiPatternSetting>().FirstOrDefaultAsync(x => x.GuildId == guildId)
            .ConfigureAwait(false);
        var deletedCount = 0;
        if (setting != null)
        {
            // Delete all patterns first
            await db.GetTable<AntiPatternPattern>().Where(p => p.AntiPatternSettingId == setting.Id).DeleteAsync()
                .ConfigureAwait(false);
            // Then delete the setting
            deletedCount = await db.DeleteAsync(setting).ConfigureAwait(false);
        }

        return removed || deletedCount > 0;
    }

    /// <summary>
    ///     Adds a regex pattern to the anti-pattern protection for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to add the pattern for.</param>
    /// <param name="pattern">The regex pattern to match against usernames/display names.</param>
    /// <param name="name">Optional name for the pattern.</param>
    /// <param name="checkUsername">Whether to check usernames against this pattern.</param>
    /// <param name="checkDisplayName">Whether to check display names against this pattern.</param>
    /// <returns>A task that represents the asynchronous operation and contains a boolean indicating success.</returns>
    public async Task<bool> AddPatternAsync(ulong guildId, string pattern, string? name = null,
        bool checkUsername = true, bool checkDisplayName = true)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var setting = await db.GetTable<AntiPatternSetting>().FirstOrDefaultAsync(x => x.GuildId == guildId)
            .ConfigureAwait(false);

        if (setting == null)
        {
            logger.LogWarning("Attempted to add pattern to non-existent AntiPatternSetting for GuildId: {GuildId}",
                guildId);
            return false;
        }

        try
        {
            // Test if the pattern is valid regex
            _ = new Regex(pattern);
        }
        catch (ArgumentException)
        {
            logger.LogWarning("Invalid regex pattern attempted: {Pattern}", pattern);
            return false;
        }

        var newPattern = new AntiPatternPattern
        {
            AntiPatternSettingId = setting.Id,
            Pattern = pattern,
            Name = name,
            CheckUsername = checkUsername,
            CheckDisplayName = checkDisplayName,
            DateAdded = DateTime.UtcNow
        };

        await db.InsertAsync(newPattern).ConfigureAwait(false);

        // Refresh the cache
        await Initialize(guildId);

        return true;
    }

    /// <summary>
    ///     Removes a regex pattern from the anti-pattern protection for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to remove the pattern from.</param>
    /// <param name="patternId">The ID of the pattern to remove.</param>
    /// <returns>A task that represents the asynchronous operation and contains a boolean indicating success.</returns>
    public async Task<bool> RemovePatternAsync(ulong guildId, int patternId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var setting = await db.GetTable<AntiPatternSetting>().FirstOrDefaultAsync(x => x.GuildId == guildId)
            .ConfigureAwait(false);

        if (setting == null) return false;

        var deletedCount = await db.GetTable<AntiPatternPattern>()
            .Where(p => p.Id == patternId && p.AntiPatternSettingId == setting.Id)
            .DeleteAsync().ConfigureAwait(false);

        if (deletedCount > 0)
        {
            // Refresh the cache
            await Initialize(guildId);
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Gets all anti-pattern patterns for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to get patterns for.</param>
    /// <returns>A list of anti-pattern patterns.</returns>
    public async Task<List<AntiPatternPattern>> GetAntiPatternPatternsAsync(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var setting = await db.GetTable<AntiPatternSetting>().FirstOrDefaultAsync(x => x.GuildId == guildId)
            .ConfigureAwait(false);

        if (setting == null) return new List<AntiPatternPattern>();

        return await db.GetTable<AntiPatternPattern>()
            .Where(p => p.AntiPatternSettingId == setting.Id)
            .ToListAsync().ConfigureAwait(false);
    }

    /// <summary>
    ///     Handles the anti-mass-post protection for detecting cross-channel spam.
    /// </summary>
    /// <param name="arg">The message that was received.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private Task HandleAntiMassPost(IMessage arg)
    {
        if (arg is not SocketUserMessage msg || msg.Author.IsBot || msg.Author is not IGuildUser guildUser)
            return Task.CompletedTask;

        if (msg.Channel is not ITextChannel channel)
            return Task.CompletedTask;

        if (!antiMassPostGuilds.TryGetValue(channel.Guild.Id, out var massPostStats))
            return Task.CompletedTask;

        _ = Task.Run(async () =>
        {
            try
            {
                var settings = massPostStats.AntiMassPostSettings;

                // Check if user has administrator permission
                if (guildUser.GuildPermissions.Administrator)
                    return;

                // Check if user is ignored
                if (settings.IgnoreBots && msg.Author.IsBot)
                    return;

                if (settings.AntiMassPostIgnoredUsers.Any(u => u.UserId == msg.Author.Id))
                    return;

                // Check if user has ignored role
                if (settings.AntiMassPostIgnoredRoles.Any(r => guildUser.RoleIds.Contains(r.RoleId)))
                    return;

                // Check if channel is ignored
                if (settings.AntiMassPostIgnoredChannels.Any(c => c.ChannelId == channel.Id))
                    return;

                // Extract content
                var content = msg.Content;
                if (string.IsNullOrWhiteSpace(content) || content.Length < settings.MinContentLength)
                    return;

                // Check links only mode
                if (settings.CheckLinksOnly)
                {
                    if (!content.TryGetUrlPath(out _))
                        return;

                    // Extract domains
                    var domains = ExtractDomains(content);

                    // Check blacklist first
                    if (settings.AntiMassPostLinkBlacklists.Any(b => domains.Contains(b.Domain.ToLower())))
                    {
                        await PunishMassPost(guildUser, massPostStats, msg).ConfigureAwait(false);
                        return;
                    }

                    // Check whitelist
                    if (settings.AntiMassPostLinkWhitelists.Any() &&
                        domains.All(d => settings.AntiMassPostLinkWhitelists.Any(w => w.Domain.ToLower() == d)))
                        return;
                }

                // Track message
                var userStats = massPostStats.UserStats.GetOrAdd(msg.Author.Id,
                    _ => new UserMassPostStats(settings.TimeWindowSeconds, settings.MaxMessagesTracked));

                var triggeredMessageIds = userStats.AddMessage(
                    channel.Id,
                    content,
                    settings.ChannelThreshold,
                    settings.ContentSimilarityThreshold,
                    settings.RequireIdenticalContent,
                    settings.CaseSensitive);

                if (triggeredMessageIds != null)
                {
                    await PunishMassPost(guildUser, massPostStats, msg).ConfigureAwait(false);

                    // Clean up tracked messages
                    if (massPostStats.UserStats.TryRemove(msg.Author.Id, out var removedStats))
                        removedStats.Dispose();
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error processing anti-mass-post for user {UserId}", msg.Author.Id);
            }
        });

        return Task.CompletedTask;
    }

    /// <summary>
    ///     Extracts domains from a message content.
    /// </summary>
    private static HashSet<string> ExtractDomains(string content)
    {
        var domains = new HashSet<string>();
        var urlRegex = new Regex(@"https?://(?:www\.)?([^/\s]+)", RegexOptions.IgnoreCase);
        var matches = urlRegex.Matches(content);

        foreach (Match match in matches)
        {
            if (match.Groups.Count > 1)
            {
                domains.Add(match.Groups[1].Value.ToLower());
            }
        }

        return domains;
    }

    /// <summary>
    ///     Punishes a user for mass posting and optionally deletes messages.
    /// </summary>
    private async Task PunishMassPost(IGuildUser user, AntiMassPostStats stats, IUserMessage triggerMessage)
    {
        stats.Increment();

        if (stats.AntiMassPostSettings.DeleteMessages)
        {
            try
            {
                await triggerMessage.DeleteAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to delete message {MessageId}", triggerMessage.Id);
            }
        }

        if (stats.AntiMassPostSettings.NotifyUser)
        {
            try
            {
                var dmChannel = await user.CreateDMChannelAsync().ConfigureAwait(false);
                await dmChannel.SendMessageAsync(strings.MassPostDetectedDm(user.Guild.Id, user.Guild.Name))
                    .ConfigureAwait(false);
            }
            catch
            {
                // DM failed, continue with punishment
            }
        }

        await PunishUsers(stats.Action, ProtectionType.MassPosting, stats.PunishDuration, stats.RoleId, user)
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Handles the anti-post-channel protection for honeypot channels.
    /// </summary>
    /// <param name="arg">The message that was received.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private Task HandleAntiPostChannel(IMessage arg)
    {
        if (arg is not SocketUserMessage msg || msg.Author.IsBot || msg.Author is not IGuildUser guildUser ||
            msg.Channel is not ITextChannel channel ||
            !antiPostChannelGuilds.TryGetValue(channel.Guild.Id, out var postChannelStats))
            return Task.CompletedTask;

        _ = Task.Run(async () =>
        {
            try
            {
                var settings = postChannelStats.AntiPostChannelSettings;

                // Check if this channel is a honeypot
                if (settings.AntiPostChannelChannels.All(c => c.ChannelId != channel.Id))
                    return;

                // Check if user has administrator permission
                if (guildUser.GuildPermissions.Administrator)
                    return;

                // Check if user is ignored
                if (settings.IgnoreBots && msg.Author.IsBot)
                    return;

                if (settings.AntiPostChannelIgnoredUsers.Any(u => u.UserId == msg.Author.Id))
                    return;

                // Check if user has ignored role
                if (settings.AntiPostChannelIgnoredRoles.Any(r => guildUser.RoleIds.Contains(r.RoleId)))
                    return;

                // User posted in honeypot channel - punish them
                postChannelStats.Increment();

                postChannelStats.RecentViolations.Enqueue((guildUser.Id, guildUser.Username, DateTimeOffset.UtcNow));
                while (postChannelStats.RecentViolations.Count > 10)
                    postChannelStats.RecentViolations.TryDequeue(out _);

                if (settings.StatusChannelId == null)
                {
                    settings.StatusChannelId = channel.Id;
                    await using var statusDb = await dbFactory.CreateConnectionAsync();
                    await statusDb.UpdateAsync(settings).ConfigureAwait(false);
                }

                if (settings.DeleteMessages)
                {
                    try
                    {
                        await msg.DeleteAsync().ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to delete message {MessageId}", msg.Id);
                    }
                }

                if (settings.NotifyUser)
                {
                    try
                    {
                        var dmChannel = await guildUser.CreateDMChannelAsync().ConfigureAwait(false);
                        await dmChannel
                            .SendMessageAsync(strings.PostChannelDetectedDm(guildUser.Guild.Id, guildUser.Guild.Name))
                            .ConfigureAwait(false);
                    }
                    catch
                    {
                        // DM failed, continue with punishment
                    }
                }

                await PunishUsers(settings.Action, ProtectionType.PostChannelBan, settings.PunishDuration,
                        settings.RoleId, guildUser)
                    .ConfigureAwait(false);

                _ = Task.Run(() => UpdateAntiPostChannelStatusEmbedAsync(channel.Guild.Id));
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error processing anti-post-channel for user {UserId}", msg.Author.Id);
            }
        });

        return Task.CompletedTask;
    }

    /// <summary>
    ///     Starts the anti-mass-post protection for a guild.
    /// </summary>
    public async Task<AntiMassPostStats?> StartAntiMassPostAsync(ulong guildId, int channelThreshold,
        int timeWindowSeconds,
        double contentSimilarityThreshold, int minContentLength, bool checkLinksOnly, bool checkDuplicateContent,
        bool requireIdenticalContent, bool caseSensitive, bool deleteMessages, bool notifyUser, PunishmentAction action,
        int punishDuration, ulong? roleId, bool ignoreBots, int maxMessagesTracked)
    {
        if (!IsDurationAllowed(action)) punishDuration = 0;

        await using var db = await dbFactory.CreateConnectionAsync();
        var settings = await db.GetTable<AntiMassPostSetting>().FirstOrDefaultAsync(x => x.GuildId == guildId)
            .ConfigureAwait(false);
        var isNew = settings == null;
        settings ??= new AntiMassPostSetting
        {
            GuildId = guildId, DateAdded = DateTime.UtcNow
        };

        settings.Action = (int)action;
        settings.ChannelThreshold = channelThreshold;
        settings.TimeWindowSeconds = timeWindowSeconds;
        settings.ContentSimilarityThreshold = contentSimilarityThreshold;
        settings.MinContentLength = minContentLength;
        settings.CheckLinksOnly = checkLinksOnly;
        settings.CheckDuplicateContent = checkDuplicateContent;
        settings.RequireIdenticalContent = requireIdenticalContent;
        settings.CaseSensitive = caseSensitive;
        settings.DeleteMessages = deleteMessages;
        settings.NotifyUser = notifyUser;
        settings.PunishDuration = punishDuration;
        settings.RoleId = roleId;
        settings.IgnoreBots = ignoreBots;
        settings.MaxMessagesTracked = maxMessagesTracked;

        if (isNew)
            await db.InsertAsync(settings).ConfigureAwait(false);
        else
            await db.UpdateAsync(settings).ConfigureAwait(false);

        await Initialize(guildId);
        return antiMassPostGuilds.GetValueOrDefault(guildId);
    }

    /// <summary>
    ///     Stops the anti-mass-post protection for a guild.
    /// </summary>
    public async Task<bool> TryStopAntiMassPost(ulong guildId)
    {
        var removed = antiMassPostGuilds.TryRemove(guildId, out var removedStats);
        if (removed) removedStats.UserStats.ForEach(x => x.Value.Dispose());

        await using var db = await dbFactory.CreateConnectionAsync();
        var deletedCount = await db.GetTable<AntiMassPostSetting>()
            .Where(x => x.GuildId == guildId)
            .DeleteAsync().ConfigureAwait(false);

        return removed || deletedCount > 0;
    }

    /// <summary>
    ///     Starts the anti-post-channel protection for a guild.
    /// </summary>
    public async Task<AntiPostChannelStats?> StartAntiPostChannelAsync(ulong guildId, PunishmentAction action,
        int punishDuration, ulong? roleId, bool deleteMessages, bool notifyUser, bool ignoreBots,
        ulong? statusChannelId = null)
    {
        if (!IsDurationAllowed(action)) punishDuration = 0;

        await using var db = await dbFactory.CreateConnectionAsync();
        var settings = await db.GetTable<AntiPostChannelSetting>().FirstOrDefaultAsync(x => x.GuildId == guildId)
            .ConfigureAwait(false);
        var isNew = settings == null;
        settings ??= new AntiPostChannelSetting
        {
            GuildId = guildId, DateAdded = DateTime.UtcNow
        };

        settings.Action = (int)action;
        settings.PunishDuration = punishDuration;
        settings.RoleId = roleId;
        settings.DeleteMessages = deleteMessages;
        settings.NotifyUser = notifyUser;
        settings.IgnoreBots = ignoreBots;

        if (statusChannelId.HasValue)
        {
            settings.StatusChannelId = statusChannelId.Value;
            settings.StatusMessageId = null;
        }

        if (isNew)
            await db.InsertAsync(settings).ConfigureAwait(false);
        else
            await db.UpdateAsync(settings).ConfigureAwait(false);

        await Initialize(guildId);
        return antiPostChannelGuilds.GetValueOrDefault(guildId);
    }

    /// <summary>
    ///     Posts or updates the anti-post-channel status embed.
    /// </summary>
    public async Task UpdateAntiPostChannelStatusEmbedAsync(ulong guildId)
    {
        if (!antiPostChannelGuilds.TryGetValue(guildId, out var stats))
            return;

        var settings = stats.AntiPostChannelSettings;
        if (settings.StatusChannelId is not { } channelId)
            return;

        var guild = client.GetGuild(guildId);
        if (guild?.GetChannel(channelId) is not ITextChannel textChannel)
            return;

        var violations = stats.RecentViolations.ToArray();
        var thumbnailUrl = Emote.TryParse("<:HaneAngry:1026529071825420408>", out var emote) ? emote.Url : null;

        var embed = new EmbedBuilder()
            .WithTitle(strings.AntiPostChannelStatusWarning(guildId,
                ((PunishmentAction)settings.Action).ToString().ToLower()))
            .WithThumbnailUrl(thumbnailUrl)
            .WithErrorColor()
            .AddField(strings.AntiPostChannelStatusAction(guildId), ((PunishmentAction)settings.Action).ToString(),
                true)
            .AddField(strings.AntiPostChannelStatusChannels(guildId),
                settings.AntiPostChannelChannels?.Count().ToString() ?? "0", true)
            .AddField(strings.AntiPostChannelStatusTotal(guildId), stats.Counter.ToString(), true)
            .AddField(strings.AntiPostChannelStatusDelete(guildId),
                settings.DeleteMessages ? strings.Yes(guildId) : strings.No(guildId), true)
            .AddField(strings.AntiPostChannelStatusNotify(guildId),
                settings.NotifyUser ? strings.Yes(guildId) : strings.No(guildId), true)
            .WithTimestamp(DateTimeOffset.UtcNow);

        if (violations.Length > 0)
        {
            var list = string.Join("\n",
                violations.Reverse().Select((v, i) =>
                    $"`{i + 1}.` {v.Username} (`{v.UserId}`) — <t:{v.PunishedAt.ToUnixTimeSeconds()}:R>"));
            embed.AddField(strings.AntiPostChannelStatusViolations(guildId), list);
        }

        try
        {
            if (settings.StatusMessageId is { } msgId)
            {
                var msg = await textChannel.GetMessageAsync(msgId).ConfigureAwait(false);
                if (msg is IUserMessage userMsg)
                {
                    await userMsg.ModifyAsync(m => m.Embed = embed.Build()).ConfigureAwait(false);
                    return;
                }
            }

            var newMsg = await textChannel.SendMessageAsync(embed: embed.Build()).ConfigureAwait(false);
            settings.StatusMessageId = newMsg.Id;
            settings.StatusChannelId = channelId;

            await using var db = await dbFactory.CreateConnectionAsync();
            await db.UpdateAsync(settings).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to update anti-post-channel status embed for guild {GuildId}", guildId);
        }
    }

    /// <summary>
    ///     Stops the anti-post-channel protection for a guild.
    /// </summary>
    public async Task<bool> TryStopAntiPostChannel(ulong guildId)
    {
        var removed = antiPostChannelGuilds.TryRemove(guildId, out _);

        await using var db = await dbFactory.CreateConnectionAsync();
        var deletedCount = await db.GetTable<AntiPostChannelSetting>()
            .Where(x => x.GuildId == guildId)
            .DeleteAsync().ConfigureAwait(false);

        return removed || deletedCount > 0;
    }

    /// <summary>
    ///     Adds a honeypot channel to the anti-post-channel protection.
    /// </summary>
    public async Task<bool> AddAntiPostChannelAsync(ulong guildId, ulong channelId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var setting = await db.GetTable<AntiPostChannelSetting>().FirstOrDefaultAsync(x => x.GuildId == guildId)
            .ConfigureAwait(false);

        if (setting == null) return false;

        var exists = await db.GetTable<AntiPostChannelChannel>()
            .AnyAsync(c => c.AntiPostChannelSettingId == setting.Id && c.ChannelId == channelId)
            .ConfigureAwait(false);

        if (exists) return false;

        await db.InsertAsync(new AntiPostChannelChannel
        {
            AntiPostChannelSettingId = setting.Id, ChannelId = channelId, DateAdded = DateTime.UtcNow
        }).ConfigureAwait(false);

        await Initialize(guildId);
        return true;
    }

    /// <summary>
    ///     Removes a honeypot channel from the anti-post-channel protection.
    /// </summary>
    public async Task<bool> RemoveAntiPostChannelAsync(ulong guildId, ulong channelId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var setting = await db.GetTable<AntiPostChannelSetting>().FirstOrDefaultAsync(x => x.GuildId == guildId)
            .ConfigureAwait(false);

        if (setting == null) return false;

        var deleted = await db.GetTable<AntiPostChannelChannel>()
            .Where(c => c.AntiPostChannelSettingId == setting.Id && c.ChannelId == channelId)
            .DeleteAsync().ConfigureAwait(false);

        if (deleted > 0)
        {
            await Initialize(guildId);
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Toggles an ignored role for anti-post-channel protection.
    /// </summary>
    public async Task<bool> ToggleAntiPostChannelIgnoredRoleAsync(ulong guildId, ulong roleId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var setting = await db.GetTable<AntiPostChannelSetting>().FirstOrDefaultAsync(x => x.GuildId == guildId)
            .ConfigureAwait(false);

        if (setting == null) return false;

        var exists = await db.GetTable<AntiPostChannelIgnoredRole>()
            .AnyAsync(r => r.AntiPostChannelSettingId == setting.Id && r.RoleId == roleId)
            .ConfigureAwait(false);

        if (exists)
        {
            await db.GetTable<AntiPostChannelIgnoredRole>()
                .Where(r => r.AntiPostChannelSettingId == setting.Id && r.RoleId == roleId)
                .DeleteAsync().ConfigureAwait(false);
        }
        else
        {
            await db.InsertAsync(new AntiPostChannelIgnoredRole
            {
                AntiPostChannelSettingId = setting.Id, RoleId = roleId, DateAdded = DateTime.UtcNow
            }).ConfigureAwait(false);
        }

        await Initialize(guildId);
        return !exists; // Return true if added, false if removed
    }

    /// <summary>
    ///     Toggles an ignored user for anti-post-channel protection.
    /// </summary>
    public async Task<bool> ToggleAntiPostChannelIgnoredUserAsync(ulong guildId, ulong userId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var setting = await db.GetTable<AntiPostChannelSetting>().FirstOrDefaultAsync(x => x.GuildId == guildId)
            .ConfigureAwait(false);

        if (setting == null) return false;

        var exists = await db.GetTable<AntiPostChannelIgnoredUser>()
            .AnyAsync(u => u.AntiPostChannelSettingId == setting.Id && u.UserId == userId)
            .ConfigureAwait(false);

        if (exists)
        {
            await db.GetTable<AntiPostChannelIgnoredUser>()
                .Where(u => u.AntiPostChannelSettingId == setting.Id && u.UserId == userId)
                .DeleteAsync().ConfigureAwait(false);
        }
        else
        {
            await db.InsertAsync(new AntiPostChannelIgnoredUser
            {
                AntiPostChannelSettingId = setting.Id, UserId = userId, DateAdded = DateTime.UtcNow
            }).ConfigureAwait(false);
        }

        await Initialize(guildId);
        return !exists; // Return true if added, false if removed
    }

    /// <summary>
    ///     Gets list of honeypot channel IDs.
    /// </summary>
    public async Task<List<ulong>> GetAntiPostChannelChannelsAsync(ulong guildId)
    {
        if (antiPostChannelGuilds.TryGetValue(guildId, out var stats))
        {
            return stats.AntiPostChannelSettings.AntiPostChannelChannels?.Select(c => c.ChannelId).ToList() ??
                   new List<ulong>();
        }

        await using var db = await dbFactory.CreateConnectionAsync();
        var setting = await db.GetTable<AntiPostChannelSetting>().FirstOrDefaultAsync(x => x.GuildId == guildId)
            .ConfigureAwait(false);

        if (setting == null) return new List<ulong>();

        return await db.GetTable<AntiPostChannelChannel>()
            .Where(c => c.AntiPostChannelSettingId == setting.Id)
            .Select(c => c.ChannelId)
            .ToListAsync().ConfigureAwait(false);
    }

    /// <summary>
    ///     Gets list of ignored role IDs for anti-post-channel.
    /// </summary>
    public async Task<List<ulong>> GetAntiPostChannelIgnoredRolesAsync(ulong guildId)
    {
        if (antiPostChannelGuilds.TryGetValue(guildId, out var stats))
        {
            return stats.AntiPostChannelSettings.AntiPostChannelIgnoredRoles?.Select(r => r.RoleId).ToList() ??
                   new List<ulong>();
        }

        await using var db = await dbFactory.CreateConnectionAsync();
        var setting = await db.GetTable<AntiPostChannelSetting>().FirstOrDefaultAsync(x => x.GuildId == guildId)
            .ConfigureAwait(false);

        if (setting == null) return new List<ulong>();

        return await db.GetTable<AntiPostChannelIgnoredRole>()
            .Where(r => r.AntiPostChannelSettingId == setting.Id)
            .Select(r => r.RoleId)
            .ToListAsync().ConfigureAwait(false);
    }

    /// <summary>
    ///     Gets list of ignored user IDs for anti-post-channel.
    /// </summary>
    public async Task<List<ulong>> GetAntiPostChannelIgnoredUsersAsync(ulong guildId)
    {
        if (antiPostChannelGuilds.TryGetValue(guildId, out var stats))
        {
            return stats.AntiPostChannelSettings.AntiPostChannelIgnoredUsers?.Select(u => u.UserId).ToList() ??
                   new List<ulong>();
        }

        await using var db = await dbFactory.CreateConnectionAsync();
        var setting = await db.GetTable<AntiPostChannelSetting>().FirstOrDefaultAsync(x => x.GuildId == guildId)
            .ConfigureAwait(false);

        if (setting == null) return new List<ulong>();

        return await db.GetTable<AntiPostChannelIgnoredUser>()
            .Where(u => u.AntiPostChannelSettingId == setting.Id)
            .Select(u => u.UserId)
            .ToListAsync().ConfigureAwait(false);
    }

    /// <summary>
    ///     Gets the anti-image-hash statistics for a guild, or null if the protection is not enabled there.
    /// </summary>
    /// <param name="guildId">The ID of the guild to retrieve the statistics for.</param>
    /// <returns>The anti-image-hash stats, or null.</returns>
    public AntiImageHashStats? GetAntiImageHashStats(ulong guildId)
    {
        return antiImageHashGuilds.GetValueOrDefault(guildId);
    }

    /// <summary>
    ///     Turns the shipped list of known scam images on or off for a guild, without touching the rest of the settings.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="enabled">Whether the known scam images should be blocked.</param>
    /// <returns>True if the setting was changed; false if the protection is not enabled in the guild.</returns>
    public async Task<bool> SetPresetScamImagesAsync(ulong guildId, bool enabled)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var updated = await db.GetTable<AntiImageHashSetting>()
            .Where(s => s.GuildId == guildId)
            .Set(s => s.UsePresetList, enabled)
            .UpdateAsync().ConfigureAwait(false);

        if (updated == 0)
            return false;

        await Initialize(guildId);
        return true;
    }

    /// <summary>
    ///     Handles the anti-image-hash protection, punishing users who post an image whose perceptual hash matches one on the
    ///     guild blocklist.
    /// </summary>
    /// <param name="arg">The message that was received.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private Task HandleAntiImageHash(IMessage arg)
    {
        if (arg is not SocketUserMessage msg || msg.Author is not IGuildUser guildUser ||
            msg.Channel is not ITextChannel channel ||
            !antiImageHashGuilds.TryGetValue(channel.Guild.Id, out var stats))
            return Task.CompletedTask;

        var settings = stats.AntiImageHashSettings;

        var presetImages = settings.UsePresetList ? scamPresets.Images : [];

        if (stats.Hashes.Count == 0 && presetImages.Count == 0)
            return Task.CompletedTask;

        if (settings.IgnoreBots && msg.Author.IsBot)
            return Task.CompletedTask;

        if (guildUser.GuildPermissions.Administrator)
            return Task.CompletedTask;

        if (stats.IgnoredChannels.Contains(channel.Id))
            return Task.CompletedTask;

        if (guildUser.RoleIds.Any(r => stats.IgnoredRoles.Contains(r)))
            return Task.CompletedTask;

        var urls = CollectImageUrls(msg, settings.CheckEmbeds);
        if (urls.Count == 0)
            return Task.CompletedTask;

        _ = Task.Run(async () =>
        {
            try
            {
                foreach (var url in urls)
                {
                    var posted = await imageHashing
                        .ComputeMatchHashesFromUrlAsync(url, settings.CheckBorders, settings.MaxImageSizeMb)
                        .ConfigureAwait(false);

                    if (posted is null)
                        continue;

                    // A flat, low detail image hashes to something that sits close to everything, so matching it would
                    // punish innocent posters.
                    if (posted.Quality < ImageHashingService.MinReliableQuality)
                        continue;

                    var match = FindImageHashMatch(stats, posted, settings.HashThreshold);
                    if (match is not null)
                    {
                        await PunishImageHash(guildUser, stats, msg, match).ConfigureAwait(false);
                        return;
                    }

                    var preset = FindPresetMatch(presetImages, posted, settings.HashThreshold);
                    if (preset is not null)
                    {
                        await PunishPresetScamImage(guildUser, stats, msg, preset).ConfigureAwait(false);
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error processing anti-image-hash for user {UserId}", msg.Author.Id);
            }
        });

        return Task.CompletedTask;
    }

    /// <summary>
    ///     Finds the closest blocked image within tolerance of any of a posted image's hashes. Every posted variant is
    ///     compared against every stored variant, so a crop of the blocked image and the blocked image with a border added
    ///     around it both match.
    /// </summary>
    private static BlockedImageHash? FindImageHashMatch(AntiImageHashStats stats, ImageMatchHashes posted,
        int threshold)
    {
        var parsed = posted.Hashes
            .Select(h => ImageHashingService.TryParseHash(h, out var value) ? value : null)
            .Where(h => h is not null)
            .ToList();

        if (parsed.Count == 0)
            return null;

        BlockedImageHash? best = null;
        var bestDistance = int.MaxValue;

        foreach (var blocked in stats.Hashes)
        {
            foreach (var stored in blocked.Hashes)
            {
                foreach (var candidate in parsed)
                {
                    var distance = ImageHashingService.Distance(stored, candidate!);

                    if (distance <= threshold && distance < bestDistance)
                    {
                        best = blocked;
                        bestDistance = distance;
                    }
                }
            }
        }

        return best;
    }

    /// <summary>
    ///     Parses a stored blocked image row into its cached form, including the mirrored and cropped variant hashes.
    /// </summary>
    private static BlockedImageHash ToBlockedImageHash(BannedImageHash entry)
    {
        var hashes = new List<ulong[]>();

        if (ImageHashingService.TryParseHash(entry.Hash, out var full))
            hashes.Add(full);

        if (!string.IsNullOrWhiteSpace(entry.Variants))
        {
            foreach (var variant in entry.Variants.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (ImageHashingService.TryParseHash(variant, out var value))
                    hashes.Add(value);
            }
        }

        return new BlockedImageHash(entry, hashes);
    }

    /// <summary>
    ///     Collects the image URLs carried by a message, from its attachments and optionally its embeds.
    /// </summary>
    private static List<string> CollectImageUrls(IUserMessage msg, bool checkEmbeds)
    {
        var urls = new List<string>();

        foreach (var attachment in msg.Attachments)
        {
            if (attachment.ContentType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true ||
                HasImageExtension(attachment.Filename))
                urls.Add(attachment.Url);
        }

        if (checkEmbeds)
        {
            foreach (var embed in msg.Embeds)
            {
                if (embed.Image?.Url is { } imageUrl)
                    urls.Add(imageUrl);
                if (embed.Thumbnail?.Url is { } thumbUrl)
                    urls.Add(thumbUrl);
            }
        }

        return urls.Take(MaxImagesPerMessage).ToList();
    }

    private static bool HasImageExtension(string filename)
    {
        return filename.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
               filename.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
               filename.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
               filename.EndsWith(".webp", StringComparison.OrdinalIgnoreCase) ||
               filename.EndsWith(".gif", StringComparison.OrdinalIgnoreCase) ||
               filename.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Applies the punishment for a blocked image. The matched hash may override the guild default action, duration, and
    ///     role, so one image can simply be deleted while another gets the poster banned.
    /// </summary>
    private async Task PunishImageHash(IGuildUser user, AntiImageHashStats stats, IUserMessage triggerMessage,
        BlockedImageHash match)
    {
        var settings = stats.AntiImageHashSettings;
        var action = (PunishmentAction)(match.Entry.Action ?? settings.Action);
        var duration = match.Entry.PunishDuration ?? settings.PunishDuration;
        var roleId = match.Entry.RoleId ?? settings.RoleId;

        stats.Increment();
        match.Entry.HitCount++;
        match.Entry.LastTriggeredAt = DateTime.UtcNow;

        stats.RecentViolations.Enqueue((user.Id, user.Username, match.Entry.Hash, DateTimeOffset.UtcNow));
        while (stats.RecentViolations.Count > 10)
            stats.RecentViolations.TryDequeue(out _);

        _ = Task.Run(() => RecordImageHashHitAsync(user.Guild.Id, match.Entry.Id));

        await ApplyImageHashPunishment(user, stats, triggerMessage, action, duration, roleId).ConfigureAwait(false);
    }

    /// <summary>
    ///     Finds a known scam image from the shipped list within tolerance of any of a posted image's hashes.
    /// </summary>
    private static PresetScamImage? FindPresetMatch(IReadOnlyList<PresetScamImage> presets, ImageMatchHashes posted,
        int threshold)
    {
        if (presets.Count == 0)
            return null;

        var parsed = posted.Hashes
            .Select(h => ImageHashingService.TryParseHash(h, out var value) ? value : null)
            .Where(h => h is not null)
            .ToList();

        if (parsed.Count == 0)
            return null;

        PresetScamImage? best = null;
        var bestDistance = int.MaxValue;

        foreach (var preset in presets)
        {
            foreach (var stored in preset.Hashes)
            {
                foreach (var candidate in parsed)
                {
                    var distance = ImageHashingService.Distance(stored, candidate!);

                    if (distance <= threshold && distance < bestDistance)
                    {
                        best = preset;
                        bestDistance = distance;
                    }
                }
            }
        }

        return best;
    }

    /// <summary>
    ///     Applies the punishment for a known scam image from the shipped list. These have no per-image overrides, so the
    ///     guild's default action is used.
    /// </summary>
    private async Task PunishPresetScamImage(IGuildUser user, AntiImageHashStats stats, IUserMessage triggerMessage,
        PresetScamImage preset)
    {
        var settings = stats.AntiImageHashSettings;
        var action = (PunishmentAction)settings.Action;

        stats.Increment();
        settings.PresetTriggers++;

        stats.RecentViolations.Enqueue((user.Id, user.Username, preset.Id, DateTimeOffset.UtcNow));
        while (stats.RecentViolations.Count > 10)
            stats.RecentViolations.TryDequeue(out _);

        logger.LogInformation("Known scam image {PresetId} posted by {UserId} in guild {GuildId}", preset.Id, user.Id,
            user.Guild.Id);

        _ = Task.Run(() => RecordPresetHitAsync(user.Guild.Id));

        await ApplyImageHashPunishment(user, stats, triggerMessage, action, settings.PunishDuration, settings.RoleId)
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Deletes the message, DMs the poster, and applies the punishment, shared by the guild blocklist and the shipped
    ///     scam image list.
    /// </summary>
    private async Task ApplyImageHashPunishment(IGuildUser user, AntiImageHashStats stats,
        IUserMessage triggerMessage, PunishmentAction action, int duration, ulong? roleId)
    {
        var settings = stats.AntiImageHashSettings;

        if (settings.DeleteMessages || action == PunishmentAction.Delete)
        {
            try
            {
                await triggerMessage.DeleteAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to delete message {MessageId}", triggerMessage.Id);
            }
        }

        if (settings.NotifyUser)
        {
            try
            {
                var dmChannel = await user.CreateDMChannelAsync().ConfigureAwait(false);
                await dmChannel.SendMessageAsync(strings.ImageHashDetectedDm(user.Guild.Id, user.Guild.Name))
                    .ConfigureAwait(false);
            }
            catch
            {
                // DM failed, continue with punishment
            }
        }

        if (action is PunishmentAction.Delete or PunishmentAction.None)
            return;

        await PunishUsers((int)action, ProtectionType.ImageHash, duration, roleId, user).ConfigureAwait(false);
    }

    /// <summary>
    ///     Persists the counter for catches from the shipped scam image list.
    /// </summary>
    private async Task RecordPresetHitAsync(ulong guildId)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();

            await db.GetTable<AntiImageHashSetting>()
                .Where(s => s.GuildId == guildId)
                .Set(s => s.PresetTriggers, s => s.PresetTriggers + 1)
                .Set(s => s.TotalTriggers, s => s.TotalTriggers + 1)
                .UpdateAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to persist known scam image hit for guild {GuildId}", guildId);
        }
    }

    /// <summary>
    ///     Persists the hit counters for a blocked image hash.
    /// </summary>
    private async Task RecordImageHashHitAsync(ulong guildId, int hashId)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();

            await db.GetTable<BannedImageHash>()
                .Where(h => h.Id == hashId)
                .Set(h => h.HitCount, h => h.HitCount + 1)
                .Set(h => h.LastTriggeredAt, DateTime.UtcNow)
                .UpdateAsync().ConfigureAwait(false);

            await db.GetTable<AntiImageHashSetting>()
                .Where(s => s.GuildId == guildId)
                .Set(s => s.TotalTriggers, s => s.TotalTriggers + 1)
                .UpdateAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to persist image hash hit for guild {GuildId}", guildId);
        }
    }

    /// <summary>
    ///     Starts or reconfigures the anti-image-hash protection for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to start the protection for.</param>
    /// <param name="action">The default punishment action when a blocked image is posted.</param>
    /// <param name="punishDuration">The default punishment duration in minutes, if the action supports one.</param>
    /// <param name="roleId">The ID of the role to be added, if the action is AddRole.</param>
    /// <param name="hashThreshold">
    ///     The maximum hamming distance, out of 256 bits, for an image to count as a match. PDQ's standard "same image"
    ///     threshold is 31.
    /// </param>
    /// <param name="deleteMessages">Whether the offending message is deleted.</param>
    /// <param name="notifyUser">Whether the user is DMed.</param>
    /// <param name="ignoreBots">Whether messages from bots are skipped.</param>
    /// <param name="checkEmbeds">Whether images inside embeds are checked as well as attachments.</param>
    /// <param name="checkBorders">Whether posted images have solid borders stripped before matching, catching bordered copies.</param>
    /// <param name="usePresetList">Whether the known scam images shipped with the bot are blocked as well.</param>
    /// <param name="maxImageSizeMb">The maximum image size to download and hash, in megabytes.</param>
    /// <returns>The resulting stats, or null if the settings could not be saved.</returns>
    public async Task<AntiImageHashStats?> StartAntiImageHashAsync(ulong guildId, PunishmentAction action,
        int punishDuration, ulong? roleId, int hashThreshold, bool deleteMessages, bool notifyUser, bool ignoreBots,
        bool checkEmbeds, bool checkBorders, bool usePresetList, int maxImageSizeMb)
    {
        if (!IsDurationAllowed(action)) punishDuration = 0;

        await using var db = await dbFactory.CreateConnectionAsync();
        var settings = await db.GetTable<AntiImageHashSetting>().FirstOrDefaultAsync(x => x.GuildId == guildId)
            .ConfigureAwait(false);
        var isNew = settings == null;
        settings ??= new AntiImageHashSetting
        {
            GuildId = guildId, DateAdded = DateTime.UtcNow
        };

        settings.Action = (int)action;
        settings.PunishDuration = punishDuration;
        settings.RoleId = roleId;
        settings.HashThreshold = Math.Clamp(hashThreshold, 0, 64);
        settings.DeleteMessages = deleteMessages;
        settings.NotifyUser = notifyUser;
        settings.IgnoreBots = ignoreBots;
        settings.CheckEmbeds = checkEmbeds;
        settings.CheckBorders = checkBorders;
        settings.UsePresetList = usePresetList;
        settings.MaxImageSizeMb = Math.Clamp(maxImageSizeMb, 1, 32);

        if (isNew)
            await db.InsertAsync(settings).ConfigureAwait(false);
        else
            await db.UpdateAsync(settings).ConfigureAwait(false);

        await Initialize(guildId);
        return antiImageHashGuilds.GetValueOrDefault(guildId);
    }

    /// <summary>
    ///     Stops the anti-image-hash protection for a guild. The blocklist itself is kept, so re-enabling the protection
    ///     restores it.
    /// </summary>
    /// <param name="guildId">The ID of the guild to stop the protection for.</param>
    /// <returns>True if the protection was running; otherwise false.</returns>
    public async Task<bool> TryStopAntiImageHash(ulong guildId)
    {
        var removed = antiImageHashGuilds.TryRemove(guildId, out _);

        await using var db = await dbFactory.CreateConnectionAsync();
        var deletedCount = await db.GetTable<AntiImageHashSetting>()
            .Where(x => x.GuildId == guildId)
            .DeleteAsync().ConfigureAwait(false);

        return removed || deletedCount > 0;
    }

    /// <summary>
    ///     Adds an image to the guild blocklist.
    /// </summary>
    /// <param name="guildId">The ID of the guild to add the image for.</param>
    /// <param name="hashSet">
    ///     The hashes produced by <see cref="ImageHashingService.ComputeHashSet" />. Supplying the variant hashes is what
    ///     gives the entry crop and mirror resistance, so prefer hashing the image over pasting a bare hash.
    /// </param>
    /// <param name="name">An optional label for the blocked image.</param>
    /// <param name="sourceUrl">The URL the hash was generated from, kept for previews.</param>
    /// <param name="addedBy">The Discord user ID that added the image.</param>
    /// <param name="action">An optional action that overrides the guild default for this image.</param>
    /// <param name="punishDuration">An optional punishment duration that overrides the guild default for this image.</param>
    /// <param name="roleId">An optional role that overrides the guild default for this image.</param>
    /// <returns>The stored entry, or null if the hash was invalid or the image is already blocked.</returns>
    public async Task<BannedImageHash?> AddBannedImageHashAsync(ulong guildId, ImageHashSet hashSet,
        string? name = null, string? sourceUrl = null, ulong? addedBy = null, PunishmentAction? action = null,
        int? punishDuration = null, ulong? roleId = null)
    {
        if (!ImageHashingService.TryParseHash(hashSet.Hash, out _))
            return null;

        var normalized = hashSet.Hash.Trim().ToLowerInvariant();

        await using var db = await dbFactory.CreateConnectionAsync();

        var exists = await db.GetTable<BannedImageHash>()
            .AnyAsync(h => h.GuildId == guildId && h.Hash == normalized)
            .ConfigureAwait(false);

        if (exists)
            return null;

        var entry = new BannedImageHash
        {
            GuildId = guildId,
            Hash = normalized,
            Variants = hashSet.Variants.Count > 0 ? string.Join(' ', hashSet.Variants) : null,
            Quality = hashSet.Quality,
            Name = name,
            SourceUrl = sourceUrl,
            AddedBy = addedBy,
            Action = action.HasValue ? (int)action.Value : null,
            PunishDuration = punishDuration,
            RoleId = roleId,
            DateAdded = DateTime.UtcNow
        };

        entry.Id = await db.InsertWithInt32IdentityAsync(entry).ConfigureAwait(false);

        await Initialize(guildId);
        return entry;
    }

    /// <summary>
    ///     Removes an image hash from the guild blocklist.
    /// </summary>
    /// <param name="guildId">The ID of the guild to remove the hash from.</param>
    /// <param name="hashId">The ID of the blocked hash.</param>
    /// <returns>True if a hash was removed; otherwise false.</returns>
    public async Task<bool> RemoveBannedImageHashAsync(ulong guildId, int hashId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var deleted = await db.GetTable<BannedImageHash>()
            .Where(h => h.Id == hashId && h.GuildId == guildId)
            .DeleteAsync().ConfigureAwait(false);

        if (deleted == 0)
            return false;

        await Initialize(guildId);
        return true;
    }

    /// <summary>
    ///     Gets the blocked image hashes for a guild, most recently triggered first.
    /// </summary>
    /// <param name="guildId">The ID of the guild to get the blocklist for.</param>
    /// <returns>The blocked hashes, including their hit counters.</returns>
    public async Task<List<BannedImageHash>> GetBannedImageHashesAsync(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        return await db.GetTable<BannedImageHash>()
            .Where(h => h.GuildId == guildId)
            .OrderByDescending(h => h.HitCount)
            .ThenByDescending(h => h.Id)
            .ToListAsync().ConfigureAwait(false);
    }

    /// <summary>
    ///     Toggles a role exempt from anti-image-hash protection.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="roleId">The ID of the role to toggle.</param>
    /// <returns>True if the role was added to the exemptions, false if it was removed.</returns>
    public async Task<bool> ToggleAntiImageHashIgnoredRoleAsync(ulong guildId, ulong roleId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var deleted = await db.GetTable<AntiImageHashIgnoredRole>()
            .Where(r => r.GuildId == guildId && r.RoleId == roleId)
            .DeleteAsync().ConfigureAwait(false);

        if (deleted == 0)
        {
            await db.InsertAsync(new AntiImageHashIgnoredRole
            {
                GuildId = guildId, RoleId = roleId, DateAdded = DateTime.UtcNow
            }).ConfigureAwait(false);
        }

        await Initialize(guildId);
        return deleted == 0;
    }

    /// <summary>
    ///     Toggles a channel exempt from anti-image-hash protection.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="channelId">The ID of the channel to toggle.</param>
    /// <returns>True if the channel was added to the exemptions, false if it was removed.</returns>
    public async Task<bool> ToggleAntiImageHashIgnoredChannelAsync(ulong guildId, ulong channelId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var deleted = await db.GetTable<AntiImageHashIgnoredChannel>()
            .Where(c => c.GuildId == guildId && c.ChannelId == channelId)
            .DeleteAsync().ConfigureAwait(false);

        if (deleted == 0)
        {
            await db.InsertAsync(new AntiImageHashIgnoredChannel
            {
                GuildId = guildId, ChannelId = channelId, DateAdded = DateTime.UtcNow
            }).ConfigureAwait(false);
        }

        await Initialize(guildId);
        return deleted == 0;
    }
}