using System.Net;
using System.Threading;
using DataModel;
using Discord.Net;
using Humanizer;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Services.Settings;
using Mewdeko.Services.Strings;
using ZiggyCreatures.Caching.Fusion;
using DiscordShardedClient = Discord.WebSocket.DiscordShardedClient;

namespace Mewdeko.Modules.Afk.Services;

/// <summary>
///     Service for managing user AFK (Away From Keyboard) status across Discord guilds.
/// </summary>
public class AfkService : INService, IReadyExecutor, IDisposable
{
    private readonly ConcurrentDictionary<(ulong GuildId, ulong UserId), Timer> afkTimers = new();
    private readonly IFusionCache cache;
    private readonly Timer cleanupTimer;
    private readonly DiscordShardedClient client;
    private readonly BotConfigService config;
    private readonly IDataConnectionFactory dbFactory;
    private readonly EventHandler eventHandler;
    private readonly ConcurrentDictionary<ulong, (GuildConfig Config, DateTime Expiry)> guildConfigCache = new();
    private readonly ConcurrentDictionary<ulong, bool> guildDataLoaded = new();
    private readonly GuildSettingsService guildSettings;
    private readonly ILogger<AfkService> logger;
    private readonly SemaphoreSlim messageUpdateSemaphore = new(5, 5); // Rate limit protection
    private readonly ConcurrentDictionary<ulong, DateTime> recentlyProcessedMessages = new();
    private readonly GeneratedBotStrings strings;
    private bool isDisposed;
    private bool isInitialized;


    /// <summary>
    ///     Initializes a new instance of the <see cref="AfkService" /> class.
    /// </summary>
    /// <param name="dbFactory">Provider for database contexts.</param>
    /// <param name="client">The Discord sharded client.</param>
    /// <param name="cache">The fusion cache for storing AFK data.</param>
    /// <param name="guildSettings">Service for accessing guild settings.</param>
    /// <param name="eventHandler">Handler for Discord events.</param>
    /// <param name="config">The bot's configuration service.</param>
    /// <param name="strings">The localization service.</param>
    /// <param name="logger">The logger instance for structured logging.</param>
    public AfkService(
        IDataConnectionFactory dbFactory,
        DiscordShardedClient client,
        IFusionCache cache,
        GuildSettingsService guildSettings,
        EventHandler eventHandler,
        BotConfigService config,
        GeneratedBotStrings strings, ILogger<AfkService> logger)
    {
        this.cache = cache;
        this.guildSettings = guildSettings;
        this.config = config;
        this.dbFactory = dbFactory;
        this.client = client;
        this.eventHandler = eventHandler;
        this.strings = strings;
        this.logger = logger;

        this.eventHandler.Subscribe("MessageReceived", "AFKService", MessageReceived);
        this.eventHandler.Subscribe("MessageUpdated", "AFKService", MessageUpdated);
        this.eventHandler.Subscribe("UserIsTyping", "AFKService", UserTyping);

        cleanupTimer = new Timer(async _ => await CleanupTimersAsync().ConfigureAwait(false), null,
            TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(30));

        _ = InitializeTimedAfksAsync();
    }

    /// <summary>
    ///     Handles initialization when the bot is ready.
    ///     Prepares the AFK service and sets up initial state.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task OnReadyAsync()
    {
        await Task.CompletedTask;
        if (isInitialized)
            return;

        logger.LogInformation("Starting {Type} Cache", GetType());
        Environment.SetEnvironmentVariable("AFK_CACHED", "1");

        // Background cleanup of old entries
        _ = Task.Run(async () =>
        {
            try
            {
                var oneMonthAgo = DateTime.UtcNow.AddMonths(-1);
                await using var db = await dbFactory.CreateConnectionAsync();

                var deletedCount = await db.Afks
                    .Where(a => a.DateAdded < oneMonthAgo)
                    .DeleteAsync().ConfigureAwait(false);

                if (deletedCount > 0)
                {
                    logger.LogInformation("Cleaned up {Count} old AFK entries during startup", deletedCount);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during startup AFK cleanup");
            }
        });

        // Only initialize timed AFKs (they need active timers)
        _ = InitializeTimedAfksAsync();

        isInitialized = true;
        logger.LogInformation("AFK Service Ready - Lazy loading enabled");
    }

    #region Public AFK Management Methods

    /// <summary>
    ///     Gets a list of AFK users in the specified guild.
    /// </summary>
    /// <param name="guild">The guild to get AFK users from.</param>
    /// <returns>A list of guild users who are currently AFK.</returns>
    public async Task<List<IGuildUser>> GetAfkUsers(IGuild guild)
    {
        await EnsureGuildAfksLoaded(guild.Id);

        var users = await guild.GetUsersAsync();
        var afkUserTasks = users.Select(async user => await IsAfk(guild.Id, user.Id) ? user : null);
        var results = await Task.WhenAll(afkUserTasks);
        return results.Where(user => user != null).ToList();
    }

    /// <summary>
    ///     Sets the custom AFK message for the guild.
    /// </summary>
    /// <param name="guild">The guild to set the custom AFK message for.</param>
    /// <param name="afkMessage">The custom AFK message to set.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task SetCustomAfkMessage(IGuild guild, string afkMessage)
    {
        var guildConfig = await guildSettings.GetGuildConfig(guild.Id);
        if (guildConfig == null) return;
        guildConfig.AfkMessage = afkMessage;
        await guildSettings.UpdateGuildConfig(guild.Id, guildConfig);
        InvalidateGuildConfigCache(guild.Id);
    }

    /// <summary>
    ///     Checks if the specified user is AFK in the guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="userId">The ID of the user.</param>
    /// <returns>True if the user is AFK in the guild; otherwise, false.</returns>
    public async Task<bool> IsAfk(ulong guildId, ulong userId)
    {
        var afk = await GetAfk(guildId, userId);
        return afk is not null;
    }

    /// <summary>
    ///     Retrieves the AFK entry for the specified user in the guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="userId">The ID of the user.</param>
    /// <returns>The AFK entry for the user if found; otherwise, null.</returns>
    public async Task<DataModel.Afk?> GetAfk(ulong guildId, ulong userId)
    {
        var cacheKey = $"{guildId}:{userId}";
        var result = await cache.GetOrDefaultAsync<DataModel.Afk>(cacheKey);

        if (result != null || guildDataLoaded.ContainsKey(guildId)) return result;
        await EnsureGuildAfksLoaded(guildId);
        result = await cache.GetOrDefaultAsync<DataModel.Afk>(cacheKey);
        return result;
    }

    /// <summary>
    ///     Sets or removes the AFK status for the specified user in the specified guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="message">The AFK message. If empty, removes all AFK statuses for the user.</param>
    /// <param name="timed">Whether the AFK is timed.</param>
    /// <param name="when">The time when the AFK was set.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task AfkSet(ulong guildId, ulong userId, string message, bool timed = false, DateTime when = default)
    {
        if (afkTimers.TryRemove((guildId, userId), out var existingTimer))
        {
            await existingTimer.DisposeAsync();
        }

        await using var db = await dbFactory.CreateConnectionAsync();

        await db.Afks
            .Where(a => a.GuildId == guildId && a.UserId == userId)
            .DeleteAsync().ConfigureAwait(false);

        if (string.IsNullOrEmpty(message))
        {
            await cache.RemoveAsync($"{guildId}:{userId}");
        }
        else
        {
            var newAfk = new DataModel.Afk
            {
                GuildId = guildId,
                UserId = userId,
                Message = message,
                WasTimed = timed,
                When = when == default ? DateTime.UtcNow : when,
                DateAdded = DateTime.UtcNow
            };

            await db.InsertAsync(newAfk).ConfigureAwait(false);

            var cacheOptions = new FusionCacheEntryOptions
            {
                Duration = timed && newAfk.When.HasValue
                    ? TimeSpan.FromMinutes(Math.Max(1, (newAfk.When.Value - DateTime.UtcNow).TotalMinutes + 5))
                    : TimeSpan.FromHours(12)
            };
            await cache.SetAsync($"{guildId}:{userId}", newAfk, cacheOptions);

            if (timed && newAfk.When.HasValue && newAfk.When.Value > DateTime.UtcNow)
            {
                ScheduleTimedAfk(newAfk);
            }
        }
    }

    /// <summary>
    ///     Sets the AFK type for the guild.
    /// </summary>
    /// <param name="guild">The guild to set the AFK type for.</param>
    /// <param name="num">The AFK type to set.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task AfkTypeSet(IGuild guild, int num)
    {
        var guildConfig = await guildSettings.GetGuildConfig(guild.Id);
        if (guildConfig == null) return;
        guildConfig.AfkType = num;
        await guildSettings.UpdateGuildConfig(guild.Id, guildConfig);
        InvalidateGuildConfigCache(guild.Id);
    }

    /// <summary>
    ///     Sets the AFK deletion time for the guild.
    /// </summary>
    /// <param name="guild">The guild to set the AFK deletion for.</param>
    /// <param name="inputNum">The input number representing AFK deletion time in seconds.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task AfkDelSet(IGuild guild, int inputNum)
    {
        var num = inputNum.ToString();
        var guildConfig = await guildSettings.GetGuildConfig(guild.Id);
        if (guildConfig == null) return;
        guildConfig.AfkDel = num;
        await guildSettings.UpdateGuildConfig(guild.Id, guildConfig);
        InvalidateGuildConfigCache(guild.Id);
    }

    /// <summary>
    ///     Sets the AFK message length limit for the guild.
    /// </summary>
    /// <param name="guild">The guild to set the AFK length for.</param>
    /// <param name="num">The AFK length to set.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task AfkLengthSet(IGuild guild, int num)
    {
        var guildConfig = await guildSettings.GetGuildConfig(guild.Id);
        if (guildConfig == null) return;
        guildConfig.AfkLength = num;
        await guildSettings.UpdateGuildConfig(guild.Id, guildConfig);
        InvalidateGuildConfigCache(guild.Id);
    }

    /// <summary>
    ///     Sets the AFK timeout for the guild.
    /// </summary>
    /// <param name="guild">The guild to set the AFK timeout for.</param>
    /// <param name="num">The AFK timeout to set in seconds.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task AfkTimeoutSet(IGuild guild, int num)
    {
        var guildConfig = await guildSettings.GetGuildConfig(guild.Id);
        if (guildConfig == null) return;
        guildConfig.AfkTimeout = num;
        await guildSettings.UpdateGuildConfig(guild.Id, guildConfig);
        InvalidateGuildConfigCache(guild.Id);
    }

    /// <summary>
    ///     Sets the AFK disabled channels for the guild.
    /// </summary>
    /// <param name="guild">The guild to set the AFK disabled channels for.</param>
    /// <param name="channels">Comma-separated list of channel IDs where AFK is disabled.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task AfkDisabledSet(IGuild guild, string channels)
    {
        var guildConfig = await guildSettings.GetGuildConfig(guild.Id);
        if (guildConfig == null) return;
        guildConfig.AfkDisabledChannels = channels;
        await guildSettings.UpdateGuildConfig(guild.Id, guildConfig);
        InvalidateGuildConfigCache(guild.Id);
    }

    /// <summary>
    ///     Retrieves the custom AFK message for the specified guild.
    /// </summary>
    /// <param name="id">The ID of the guild.</param>
    /// <returns>The custom AFK message.</returns>
    public async Task<string?> GetCustomAfkMessage(ulong id) // Return nullable string
    {
        return (await GetGuildConfigCached(id))?.AfkMessage;
    }

    /// <summary>
    ///     Retrieves the AFK deletion setting for the specified guild.
    /// </summary>
    /// <param name="id">The ID of the guild.</param>
    /// <returns>The AFK message deletion timeout in seconds.</returns>
    public async Task<int> GetAfkDel(ulong id)
    {
        var config = await GetGuildConfigCached(id);
        return config != null && int.TryParse(config.AfkDel, out var num) ? num : 0;
    }

    /// <summary>
    ///     Retrieves the AFK type for the specified guild.
    /// </summary>
    /// <param name="id">The ID of the guild.</param>
    /// <returns>The AFK type (0-4).</returns>
    public async Task<int> GetAfkType(ulong id)
    {
        return (await GetGuildConfigCached(id))?.AfkType ?? 0; // Return default if config null
    }

    /// <summary>
    ///     Retrieves the AFK message length limit for the specified guild.
    /// </summary>
    /// <param name="id">The ID of the guild.</param>
    /// <returns>The maximum allowed length for AFK messages.</returns>
    public async Task<int> GetAfkLength(ulong id)
    {
        // Provide a default length if config is null or AfkLength is 0/not set
        return (await GetGuildConfigCached(id))?.AfkLength ?? 128;
    }

    /// <summary>
    ///     Retrieves the disabled AFK channels for the specified guild.
    /// </summary>
    /// <param name="id">The ID of the guild.</param>
    /// <returns>A comma-separated list of channel IDs where AFK is disabled.</returns>
    public async Task<string?> GetDisabledAfkChannels(ulong id)
    {
        return (await GetGuildConfigCached(id))?.AfkDisabledChannels;
    }

    /// <summary>
    ///     Retrieves the AFK timeout for the specified guild.
    /// </summary>
    /// <param name="id">The ID of the guild.</param>
    /// <returns>The timeout in seconds after which AFK status is automatically removed.</returns>
    public async Task<int> GetAfkTimeout(ulong id)
    {
        // Provide a default timeout if config is null or AfkTimeout is 0/not set
        return (await GetGuildConfigCached(id))?.AfkTimeout ?? 10;
    }

    #endregion

    #region Private Implementation

    private async Task<GuildConfig?> GetGuildConfigCached(ulong guildId)
    {
        if (guildConfigCache.TryGetValue(guildId, out var cached) && cached.Expiry > DateTime.UtcNow)
        {
            return cached.Config;
        }

        var config = await guildSettings.GetGuildConfig(guildId);
        if (config != null)
        {
            guildConfigCache[guildId] = (config, DateTime.UtcNow.AddMinutes(15));
        }

        return config;
    }

    private void InvalidateGuildConfigCache(ulong guildId)
    {
        guildConfigCache.TryRemove(guildId, out _);
    }

    private async Task EnsureGuildAfksLoaded(ulong guildId)
    {
        if (guildDataLoaded.ContainsKey(guildId))
            return;

        await using var db = await dbFactory.CreateConnectionAsync();
        var guildAfks = await db.Afks
            .Where(a => a.GuildId == guildId)
            .OrderByDescending(afk => afk.DateAdded) // Fetch ordered
            .ToListAsync().ConfigureAwait(false); // Use ToListAsync

        // Get latest AFK per user efficiently
        var latestAfksPerUser = guildAfks
            .GroupBy(a => a.UserId)
            .Select(g => g.First()) // First is latest due to OrderByDescending
            .ToDictionary(a => a.UserId);

        foreach (var afk in latestAfksPerUser.Values)
        {
            var cacheOptions = new FusionCacheEntryOptions
            {
                Duration = afk.WasTimed && afk.When.HasValue
                    ? TimeSpan.FromMinutes(Math.Max(1, (afk.When.Value - DateTime.UtcNow).TotalMinutes + 5))
                    : TimeSpan.FromHours(12)
            };
            await cache.SetAsync($"{guildId}:{afk.UserId}", afk, cacheOptions);

            if (afk.WasTimed && afk.When.HasValue && afk.When.Value > DateTime.UtcNow)
            {
                ScheduleTimedAfk(afk);
            }
        }

        guildDataLoaded[guildId] = true;
    }

    private void ScheduleTimedAfk(DataModel.Afk afk)
    {
        if (!afk.When.HasValue || afk.When.Value <= DateTime.UtcNow) return; // Don't schedule if already past

        var timeToGo = afk.When.Value - DateTime.UtcNow;

        var state = new AfkTimerState
        {
            GuildId = afk.GuildId, UserId = afk.UserId
        };
        var timer = new Timer(async _ => await TimedAfkCallback(state), state, timeToGo, Timeout.InfiniteTimeSpan);

        if (afkTimers.TryRemove((afk.GuildId, afk.UserId), out var existingTimer))
        {
            existingTimer.Dispose();
        }

        afkTimers[(afk.GuildId, afk.UserId)] = timer;
    }

    private async Task TimedAfkCallback(object? timerState) // Timer callback state is object?
    {
        if (timerState is not AfkTimerState state) return;
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();
            var afk = await db.Afks // Fetch latest state
                .FirstOrDefaultAsync(a => a.GuildId == state.GuildId && a.UserId == state.UserId).ConfigureAwait(false);

            // Only proceed if AFK exists and was timed and is now due
            if (afk is { WasTimed: true, When: not null } && afk.When.Value <= DateTime.UtcNow)
            {
                await TimedAfkFinished(afk);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in timed AFK callback for {GuildId}:{UserId}", state.GuildId, state.UserId);
        }
        finally
        {
            if (afkTimers.TryRemove((state.GuildId, state.UserId), out var timer))
            {
                await timer.DisposeAsync();
            }
        }
    }

    private class AfkTimerState
    {
        public ulong GuildId { get; set; }
        public ulong UserId { get; set; }
    }

    private async Task CleanupTimersAsync()
    {
        try
        {
            logger.LogDebug("Running AFK timer cleanup, current count: {TimerCount}", afkTimers.Count);
            var now = DateTime.UtcNow;

            var expiredConfigs = guildConfigCache
                .Where(kv => kv.Value.Expiry < now)
                .Select(kv => kv.Key)
                .ToList();
            foreach (var guildId in expiredConfigs) guildConfigCache.TryRemove(guildId, out _);

            var timersToCheck = afkTimers.ToList();
            foreach (var timerEntry in timersToCheck)
            {
                var (guildId, userId) = timerEntry.Key;
                var afk = await cache.GetOrDefaultAsync<DataModel.Afk>($"{guildId}:{userId}")
                    .ConfigureAwait(false);

                if (afk == null || afk.When.HasValue && afk.When.Value < now)
                {
                    if (afkTimers.TryRemove(timerEntry.Key, out var timer)) timer.Dispose();
                }
            }

            _ = Task.Run(async () => // Run DB cleanup in background
            {
                try
                {
                    var oneMonthAgo = now.AddMonths(-1);
                    await using var db = await dbFactory.CreateConnectionAsync();

                    var oldAfkIdsToDelete = await db.Afks
                        .Where(a => a.DateAdded < oneMonthAgo)
                        .Select(a => new
                        {
                            a.GuildId, a.UserId, a.Id
                        }) // Select info needed for cache/timer removal
                        .ToListAsync().ConfigureAwait(false);

                    if (oldAfkIdsToDelete.Any())
                    {
                        logger.LogInformation("Deleting {Count} AFK entries older than one month",
                            oldAfkIdsToDelete.Count);
                        var ids = oldAfkIdsToDelete.Select(a => a.Id).ToList();
                        await db.Afks.Where(a => ids.Contains(a.Id)).DeleteAsync().ConfigureAwait(false);

                        foreach (var afkInfo in oldAfkIdsToDelete)
                        {
                            await cache.RemoveAsync($"{afkInfo.GuildId}:{afkInfo.UserId}");
                            if (afkTimers.TryRemove((afkInfo.GuildId, afkInfo.UserId), out var timer))
                            {
                                await timer.DisposeAsync();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error during old AFK entries cleanup task");
                }
            });
            logger.LogDebug("AFK timer cleanup complete, new count: {TimerCount}", afkTimers.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during AFK timer cleanup");
        }
    }

    private async Task InitializeTimedAfksAsync()
    {
        try
        {
            var now = DateTime.UtcNow;
            await using var db = await dbFactory.CreateConnectionAsync();

            var timedAfks = await db.Afks
                .Where(x => x.WasTimed && x.When > now) // Ensure When is not null and in future
                .ToListAsync().ConfigureAwait(false); // Use ToListAsync

            logger.LogInformation("Initializing {Count} timed AFKs", timedAfks.Count);

            foreach (var afk in timedAfks)
            {
                // Ensure When has value before calculating duration
                if (!afk.When.HasValue) continue;

                var cacheOptions = new FusionCacheEntryOptions
                {
                    Duration = TimeSpan.FromMinutes(Math.Max(1, (afk.When.Value - now).TotalMinutes + 5))
                };
                await cache.SetAsync($"{afk.GuildId}:{afk.UserId}", afk, cacheOptions);
                ScheduleTimedAfk(afk);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error initializing timed AFKs");
        }
    }

    private async Task TimedAfkFinished(DataModel.Afk afk)
    {
        try
        {
            await AfkSet(afk.GuildId, afk.UserId, ""); // Use AfkSet to remove from DB and cache

            var guild = client.GetGuild(afk.GuildId);
            if (guild == null) return;
            var user = guild.GetUser(afk.UserId);
            if (user == null) return;

            try
            {
                if (user.Nickname != null && user.Nickname.Contains("[AFK]"))
                {
                    await user.ModifyAsync(x => x.Nickname = user.Nickname.Replace("[AFK]", ""));
                }
            }
            catch
            {
                /* Ignore nickname errors */
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in TimedAfkFinished for {GuildId}:{UserId}", afk.GuildId, afk.UserId);
        }
    }

    private async Task MessageReceived(SocketMessage msg)
    {
        if (msg.Author.IsBot || msg.Author is not IGuildUser user)
            return;

        // Deduplicate rapid message events for the same message
        var messageKey = msg.Id;
        var now = DateTime.UtcNow;
        if (recentlyProcessedMessages.TryGetValue(messageKey, out var lastProcessed))
        {
            if (now - lastProcessed < TimeSpan.FromSeconds(2))
                return; // Skip if we just processed this message
        }

        recentlyProcessedMessages[messageKey] = now;

        // Clean up old entries periodically
        if (recentlyProcessedMessages.Count > 1000)
        {
            var cutoff = now.AddMinutes(-1);
            var toRemove = recentlyProcessedMessages.Where(x => x.Value < cutoff).Select(x => x.Key).ToList();
            foreach (var key in toRemove)
                recentlyProcessedMessages.TryRemove(key, out _);
        }

        try
        {
            var guildConfig = await GetGuildConfigCached(user.GuildId);
            if (guildConfig == null) return;

            // Handle author's AFK removal
            if (guildConfig.AfkType is 2 or 4)
            {
                var afkEntry = await GetAfk(user.GuildId, user.Id); // Check cache/DB
                if (afkEntry != null) // User is AFK
                {
                    switch (afkEntry.WasTimed)
                    {
                        // Don't auto-remove if timed AFK is still active
                        case true when afkEntry.When.HasValue && afkEntry.When.Value > DateTime.UtcNow:
                            // It's a timed AFK that hasn't expired yet, don't remove it just because they talked
                            break;
                        // Check timeout for non-timed AFKs
                        case false when afkEntry.DateAdded.HasValue && afkEntry.DateAdded.Value <
                            DateTime.UtcNow.AddSeconds(-(guildConfig.AfkTimeout == 0 ? guildConfig.AfkTimeout : 10)):
                        {
                            await AfkSet(user.GuildId, user.Id, ""); // Clear AFK
                            var notifyMsg = await msg.Channel
                                .SendMessageAsync(strings.WelcomeBackAfk(user.Guild.Id, user.Mention))
                                .ConfigureAwait(false);
                            notifyMsg.DeleteAfter(5);
                            try
                            {
                                if (user.Nickname?.Contains("[AFK]") == true)
                                    await user.ModifyAsync(x => x.Nickname = user.Nickname.Replace("[AFK]", ""))
                                        .ConfigureAwait(false);
                            }
                            catch
                            {
                                /* Ignore nickname errors */
                            }

                            return; // Return after clearing AFK
                        }
                    }
                }
            }

            // Handle mentioned AFK users
            if (msg.MentionedUsers.Count > 0)
            {
                var prefix = await guildSettings.GetPrefix(user.Guild); // Fetch prefix using GuildSettingsService
                if (msg.Content.StartsWith(prefix)) return; // Ignore commands

                var disabledChannels = guildConfig.AfkDisabledChannels?.Split(',') ?? [];
                if (disabledChannels.Contains(msg.Channel.Id.ToString())) return;

                foreach (var mentionedUser in msg.MentionedUsers.Take(5)) // Limit checks per message
                {
                    if (mentionedUser.IsBot || mentionedUser.Id == user.Id) continue;
                    if (mentionedUser is not IGuildUser mentionedGuildUser) continue;

                    var mentionedAfk = await GetAfk(user.GuildId, mentionedUser.Id);
                    if (mentionedAfk == null) continue;

                    var customAfkMessage = guildConfig.AfkMessage;
                    var afkDeleteTime = int.TryParse(guildConfig.AfkDel, out var delTime) ? delTime : 0;
                    var length = guildConfig.AfkLength > 0 ? guildConfig.AfkLength : 128;

                    if (string.IsNullOrWhiteSpace(customAfkMessage) || customAfkMessage == "-")
                    {
                        var embed = new EmbedBuilder()
                            .WithAuthor(eab =>
                                eab.WithName(strings.UserCurrentlyAway(user.Guild.Id, mentionedGuildUser))
                                    .WithIconUrl(mentionedGuildUser.GetAvatarUrl()))
                            .WithDescription(mentionedAfk.Message.Truncate(length))
                            .WithFooter(
                                strings.AfkFor(user.Guild.Id,
                                    (DateTime.UtcNow - (mentionedAfk.DateAdded ?? DateTime.UtcNow)).Humanize()))
                            .WithOkColor()
                            .Build();

                        var components = config.Data.ShowInviteButton
                            ? new ComponentBuilder().WithButton(style: ButtonStyle.Link, url: config.Data.SupportServer,
                                label: "Invite Me!", emote: config.Data.SuccessEmote.ToIEmote()).Build()
                            : null;
                        var sentMsg = await msg.Channel.SendMessageAsync(embed: embed, components: components);
                        if (afkDeleteTime > 0) sentMsg.DeleteAfter(afkDeleteTime);
                    }
                    else
                    {
                        var replacer = new ReplacementBuilder()
                            .WithOverride("%afk.message%",
                                () => mentionedAfk.Message.SanitizeMentions(true).Truncate(length))
                            .WithOverride("%afk.user%", () => mentionedGuildUser.ToString())
                            .WithOverride("%afk.user.mention%", () => mentionedGuildUser.Mention)
                            .WithOverride("%afk.user.avatar%", () => mentionedGuildUser.GetAvatarUrl(size: 2048))
                            .WithOverride("%afk.user.id%", () => mentionedGuildUser.Id.ToString())
                            .WithOverride("%afk.triggeruser%", () => msg.Author.ToString().EscapeWeirdStuff())
                            .WithOverride("%afk.triggeruser.avatar%", () => msg.Author.RealAvatarUrl().ToString())
                            .WithOverride("%afk.triggeruser.id%", () => msg.Author.Id.ToString())
                            .WithOverride("%afk.triggeruser.mention%", () => msg.Author.Mention)
                            .WithOverride("%afk.time%",
                                () => $"{(DateTime.UtcNow - (mentionedAfk.DateAdded ?? DateTime.UtcNow)).Humanize()}")
                            .Build();
                        var parsedMessage = replacer.Replace(customAfkMessage);

                        if (SmartEmbed.TryParse(parsedMessage, user.GuildId, out var embed, out var plainText,
                                out var components))
                        {
                            var sentMsg = await msg.Channel.SendMessageAsync(plainText, embeds: embed,
                                components: components?.Build());
                            if (afkDeleteTime > 0) sentMsg.DeleteAfter(afkDeleteTime);
                        }
                        else
                        {
                            var sentMsg = await msg.Channel.SendMessageAsync(parsedMessage.SanitizeMentions(true));
                            if (afkDeleteTime > 0) sentMsg.DeleteAfter(afkDeleteTime);
                        }
                    }

                    break; // Only show for first mentioned AFK user
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in AfkHandler MessageReceived");
        }
    }

    private async Task MessageUpdated(Cacheable<IMessage, ulong> msg, SocketMessage updatedMsg,
        ISocketMessageChannel channel)
    {
        try
        {
            // Skip processing if we already have the updated message
            if (updatedMsg == null || updatedMsg.Author.IsBot || updatedMsg.EditedTimestamp == null)
                return;

            // Skip if message is too old or not edited recently enough
            if (DateTimeOffset.UtcNow - updatedMsg.EditedTimestamp.Value > TimeSpan.FromMinutes(5)) return;

            // Check if we have the message cached first
            IMessage? message = null;
            if (msg.HasValue)
            {
                message = msg.Value;
            }
            else
            {
                // Try to download with timeout and rate limit protection
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                    var options = new RequestOptions
                    {
                        CancelToken = cts.Token, RetryMode = RetryMode.AlwaysRetry, Timeout = 3000
                    };

                    // Use updatedMsg directly if download fails or times out
                    message = await msg.GetOrDownloadAsync();
                }
                catch (TimeoutException)
                {
                    // Log at debug level since this is expected under rate limits
                    logger.LogDebug("Timeout downloading message {MessageId} in MessageUpdated, using cached data",
                        msg.Id);
                    // Continue processing with updatedMsg which we already have
                }
                catch (HttpException httpEx) when (httpEx.HttpCode == HttpStatusCode.TooManyRequests)
                {
                    logger.LogDebug("Rate limited when downloading message {MessageId} in MessageUpdated", msg.Id);
                    // Continue processing with updatedMsg
                }
            }

            // Process the updated message - we already have updatedMsg so we can proceed
            await MessageReceived(updatedMsg);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in AfkHandler MessageUpdated");
        }
    }

    private async Task UserTyping(Cacheable<IUser, ulong> userCache, Cacheable<IMessageChannel, ulong> chanCache)
    {
        try
        {
            var user = await userCache.GetOrDownloadAsync();
            if (user is not IGuildUser guildUser) return;

            var guildConfig = await GetGuildConfigCached(guildUser.GuildId);
            if (guildConfig == null) return;

            if (guildConfig.AfkType is 3 or 4)
            {
                var afkEntry = await GetAfk(guildUser.GuildId, user.Id);
                // Check if user is AFK, it's not a timed one (or timed one expired), and timeout passed
                if (afkEntry != null &&
                    (!afkEntry.WasTimed || afkEntry.When.HasValue && afkEntry.When.Value < DateTime.UtcNow) &&
                    afkEntry.DateAdded.HasValue && afkEntry.DateAdded.Value <
                    DateTime.UtcNow.AddSeconds(-(guildConfig.AfkTimeout == 0 ? guildConfig.AfkTimeout : 10)))
                {
                    await AfkSet(guildUser.GuildId, guildUser.Id, ""); // Clear AFK

                    var chan = await chanCache.GetOrDownloadAsync();
                    if (chan != null)
                    {
                        var notifyMsg =
                            await chan.SendMessageAsync(
                                strings.WelcomeBackAfk(guildUser.GuildId, user.Mention));
                        notifyMsg.DeleteAfter(5);
                    }

                    try
                    {
                        if (guildUser.Nickname?.Contains("[AFK]") == true)
                            await guildUser.ModifyAsync(x => x.Nickname = guildUser.Nickname.Replace("[AFK]", ""))
                                .ConfigureAwait(false);
                    }
                    catch
                    {
                        /* Ignore nickname errors */
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in AfkHandler UserTyping");
        }
    }

    /// <summary>
    ///     Releases all resources used by the <see cref="AfkService" />.
    /// </summary>
    public void Dispose()
    {
        if (isDisposed) return;
        isDisposed = true;

        try
        {
            eventHandler.Unsubscribe("MessageReceived", "AFKService", MessageReceived);
            eventHandler.Unsubscribe("MessageUpdated", "AFKService", MessageUpdated);
            eventHandler.Unsubscribe("UserIsTyping", "AFKService", UserTyping);
            // Unsubscribe from Bot Joined/Left Guild events if needed

            cleanupTimer?.Dispose();
            messageUpdateSemaphore?.Dispose();

            var timers = afkTimers.Values.ToList(); // Snapshot before clearing
            afkTimers.Clear();
            foreach (var timer in timers)
            {
                try { timer.Dispose(); }
                catch
                {
                    /* Ignore disposal errors */
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during AfkService Dispose");
        }

        logger.LogInformation("AfkService disposed");
        GC.SuppressFinalize(this); // Prevent finalizer run
    }

    #endregion
}