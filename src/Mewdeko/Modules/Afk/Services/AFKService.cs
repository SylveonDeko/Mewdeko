using System.Threading;
using Humanizer;
using LinqToDB.EntityFrameworkCore;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Database.DbContextStuff;
using Mewdeko.Services.Settings;
using Microsoft.EntityFrameworkCore;
using Serilog;
using ZiggyCreatures.Caching.Fusion;

namespace Mewdeko.Modules.Afk.Services;

/// <summary>
/// Service for managing user AFK (Away From Keyboard) status across Discord guilds.
/// </summary>
public class AfkService : INService, IReadyExecutor, IDisposable
{
    // Dependencies
    private readonly IFusionCache cache;
    private readonly DiscordShardedClient client;
    private readonly BotConfigService config;
    private readonly DbContextProvider dbProvider;
    private readonly GuildSettingsService guildSettings;
    private readonly EventHandler eventHandler;

    // State tracking
    private readonly ConcurrentDictionary<(ulong GuildId, ulong UserId), Timer> afkTimers = new();
    private readonly ConcurrentDictionary<ulong, bool> guildDataLoaded = new();
    private readonly ConcurrentDictionary<ulong, (GuildConfig Config, DateTime Expiry)> guildConfigCache = new();
    private readonly Timer cleanupTimer;
    private bool isDisposed;
    private bool isInitialized;

    /// <summary>
    /// Initializes a new instance of the <see cref="AfkService"/> class.
    /// </summary>
    /// <param name="dbProvider">Provider for database contexts.</param>
    /// <param name="client">The Discord sharded client.</param>
    /// <param name="cache">The fusion cache for storing AFK data.</param>
    /// <param name="guildSettings">Service for accessing guild settings.</param>
    /// <param name="eventHandler">Handler for Discord events.</param>
    /// <param name="config">The bot's configuration service.</param>
    public AfkService(
        DbContextProvider dbProvider,
        DiscordShardedClient client,
        IFusionCache cache,
        GuildSettingsService guildSettings,
        EventHandler eventHandler,
        BotConfigService config)
    {
        this.cache = cache;
        this.guildSettings = guildSettings;
        this.config = config;
        this.dbProvider = dbProvider;
        this.client = client;
        this.eventHandler = eventHandler;

        // Set up event handlers
        this.eventHandler.MessageReceived += MessageReceived;
        this.eventHandler.MessageUpdated += MessageUpdated;
        this.eventHandler.UserIsTyping += UserTyping;

        // Set up periodic cache cleanup (every 30 minutes)
        cleanupTimer = new Timer(_ => CleanupTimers(), null, TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(30));

        // Start initialization
        _ = InitializeTimedAfksAsync();
    }

    /// <summary>
    /// Handles initialization when the bot is ready.
    /// Prepares the AFK service and sets up initial state.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task OnReadyAsync()
    {
        await Task.CompletedTask;
        if (isInitialized)
            return;
        Log.Information("Starting {Type} Cache", GetType());
        Environment.SetEnvironmentVariable("AFK_CACHED", "1");
        await Task.Run(async () =>
        {
            try
            {
                var oneMonthAgo = DateTime.UtcNow.AddMonths(-1);
                await using var dbContext = await dbProvider.GetContextAsync();

                var deletedCount = await dbContext.Afk
                    .Where(a => a.DateAdded < oneMonthAgo)
                    .ExecuteDeleteAsync();

                if (deletedCount > 0)
                {
                    Log.Information("Cleaned up {Count} old AFK entries during startup", deletedCount);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during startup AFK cleanup");
            }
        });
        isInitialized = true;

        Log.Information("AFK Service Ready");
    }

    #region Public AFK Management Methods

    /// <summary>
    /// Gets a list of AFK users in the specified guild.
    /// </summary>
    /// <param name="guild">The guild to get AFK users from.</param>
    /// <returns>A list of guild users who are currently AFK.</returns>
    public async Task<List<IGuildUser>> GetAfkUsers(IGuild guild)
    {
        // Ensure guild data is loaded
        await EnsureGuildAfksLoaded(guild.Id);

        var users = await guild.GetUsersAsync();
        return (await Task.WhenAll(users.Select(async user =>
                await IsAfk(guild.Id, user.Id) ? user : null)))
            .Where(user => user != null)
            .ToList();
    }

    /// <summary>
    /// Sets the custom AFK message for the guild.
    /// </summary>
    /// <param name="guild">The guild to set the custom AFK message for.</param>
    /// <param name="afkMessage">The custom AFK message to set.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task SetCustomAfkMessage(IGuild guild, string afkMessage)
    {
        await using var dbContext = await dbProvider.GetContextAsync();
        var guildConfig = await dbContext.ForGuildId(guild.Id, set => set);
        guildConfig.AfkMessage = afkMessage;
        await guildSettings.UpdateGuildConfig(guild.Id, guildConfig);

        // Update cache if exists
        if (guildConfigCache.TryGetValue(guild.Id, out var cached))
        {
            guildConfigCache[guild.Id] = (guildConfig, DateTime.UtcNow.AddMinutes(15));
        }
    }

    /// <summary>
    /// Checks if the specified user is AFK in the guild.
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
    /// Retrieves the AFK entry for the specified user in the guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="userId">The ID of the user.</param>
    /// <returns>The AFK entry for the user if found; otherwise, null.</returns>
    public async Task<Database.Models.Afk?> GetAfk(ulong guildId, ulong userId)
    {
        // Check cache first
        var cacheKey = $"{guildId}:{userId}";
        var result = await cache.GetOrDefaultAsync<Database.Models.Afk>(cacheKey);

        // If not found and guild data isn't loaded, load it now
        if (result == null && !guildDataLoaded.ContainsKey(guildId))
        {
            await EnsureGuildAfksLoaded(guildId);
            result = await cache.GetOrDefaultAsync<Database.Models.Afk>(cacheKey);
        }

        return result;
    }

    /// <summary>
    /// Sets or removes the AFK status for the specified user in the specified guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="message">The AFK message. If empty, removes all AFK statuses for the user.</param>
    /// <param name="timed">Whether the AFK is timed.</param>
    /// <param name="when">The time when the AFK was set.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task AfkSet(ulong guildId, ulong userId, string message, bool timed = false, DateTime when = default)
    {
        // Remove existing timer if any
        if (afkTimers.TryRemove((guildId, userId), out var existingTimer))
        {
            await existingTimer.DisposeAsync();
        }

        await using var dbContext = await dbProvider.GetContextAsync();

        // Remove existing AFK entries
        await dbContext.Afk
            .Where(a => a.GuildId == guildId && a.UserId == userId)
            .ExecuteDeleteAsync();

        if (string.IsNullOrEmpty(message))
        {
            // Remove from cache if message is empty (not AFK)
            await cache.RemoveAsync($"{guildId}:{userId}");
        }
        else
        {
            // Create new AFK entry
            var newAfk = new Database.Models.Afk
            {
                GuildId = guildId,
                UserId = userId,
                Message = message,
                WasTimed = timed,
                When = when == default ? DateTime.UtcNow : when,
                DateAdded = DateTime.UtcNow
            };

            // Add to database
            dbContext.Afk.Add(newAfk);
            await dbContext.SaveChangesAsync();

            // Add to cache with expiration based on timed status
            var cacheOptions = new FusionCacheEntryOptions
            {
                Duration = timed
                    ? TimeSpan.FromMinutes(Math.Max(1, (newAfk.When.Value - DateTime.UtcNow).TotalMinutes + 5))
                    : TimeSpan.FromHours(12)
            };

            await cache.SetAsync($"{guildId}:{userId}", newAfk, cacheOptions);

            // Set up timer if timed
            if (timed)
            {
                ScheduleTimedAfk(newAfk);
            }
        }
    }

    /// <summary>
    /// Sets the AFK type for the guild.
    /// </summary>
    /// <param name="guild">The guild to set the AFK type for.</param>
    /// <param name="num">The AFK type to set.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task AfkTypeSet(IGuild guild, int num)
    {
        await using var dbContext = await dbProvider.GetContextAsync();
        var guildConfig = await dbContext.ForGuildId(guild.Id, set => set);
        guildConfig.AfkType = num;
        await guildSettings.UpdateGuildConfig(guild.Id, guildConfig);

        // Update cache if exists
        InvalidateGuildConfigCache(guild.Id);
    }

    /// <summary>
    /// Sets the AFK deletion time for the guild.
    /// </summary>
    /// <param name="guild">The guild to set the AFK deletion for.</param>
    /// <param name="inputNum">The input number representing AFK deletion time in seconds.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task AfkDelSet(IGuild guild, int inputNum)
    {
        var num = inputNum.ToString();
        await using var dbContext = await dbProvider.GetContextAsync();
        var guildConfig = await dbContext.ForGuildId(guild.Id, set => set);
        guildConfig.AfkDel = num;
        await guildSettings.UpdateGuildConfig(guild.Id, guildConfig);

        // Update cache if exists
        InvalidateGuildConfigCache(guild.Id);
    }

    /// <summary>
    /// Sets the AFK message length limit for the guild.
    /// </summary>
    /// <param name="guild">The guild to set the AFK length for.</param>
    /// <param name="num">The AFK length to set.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task AfkLengthSet(IGuild guild, int num)
    {
        await using var dbContext = await dbProvider.GetContextAsync();
        var guildConfig = await dbContext.ForGuildId(guild.Id, set => set);
        guildConfig.AfkLength = num;
        await guildSettings.UpdateGuildConfig(guild.Id, guildConfig);

        // Update cache if exists
        InvalidateGuildConfigCache(guild.Id);
    }

    /// <summary>
    /// Sets the AFK timeout for the guild.
    /// </summary>
    /// <param name="guild">The guild to set the AFK timeout for.</param>
    /// <param name="num">The AFK timeout to set in seconds.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task AfkTimeoutSet(IGuild guild, int num)
    {
        await using var dbContext = await dbProvider.GetContextAsync();
        var guildConfig = await dbContext.ForGuildId(guild.Id, set => set);
        guildConfig.AfkTimeout = num;
        await guildSettings.UpdateGuildConfig(guild.Id, guildConfig);

        // Update cache if exists
        InvalidateGuildConfigCache(guild.Id);
    }

    /// <summary>
    /// Sets the AFK disabled channels for the guild.
    /// </summary>
    /// <param name="guild">The guild to set the AFK disabled channels for.</param>
    /// <param name="channels">Comma-separated list of channel IDs where AFK is disabled.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task AfkDisabledSet(IGuild guild, string channels)
    {
        await using var dbContext = await dbProvider.GetContextAsync();
        var guildConfig = await dbContext.ForGuildId(guild.Id, set => set);
        guildConfig.AfkDisabledChannels = channels;
        await guildSettings.UpdateGuildConfig(guild.Id, guildConfig);

        // Update cache if exists
        InvalidateGuildConfigCache(guild.Id);
    }

    /// <summary>
    /// Retrieves the custom AFK message for the specified guild.
    /// </summary>
    /// <param name="id">The ID of the guild.</param>
    /// <returns>The custom AFK message.</returns>
    public async Task<string> GetCustomAfkMessage(ulong id)
    {
        return (await GetGuildConfigCached(id)).AfkMessage;
    }

    /// <summary>
    /// Retrieves the AFK deletion setting for the specified guild.
    /// </summary>
    /// <param name="id">The ID of the guild.</param>
    /// <returns>The AFK message deletion timeout in seconds.</returns>
    public async Task<int> GetAfkDel(ulong id)
    {
        var config = await GetGuildConfigCached(id);
        return int.TryParse(config.AfkDel, out var num) ? num : 0;
    }

    /// <summary>
    /// Retrieves the AFK type for the specified guild.
    /// </summary>
    /// <param name="id">The ID of the guild.</param>
    /// <returns>The AFK type (0-4).</returns>
    public async Task<int> GetAfkType(ulong id)
    {
        return (await GetGuildConfigCached(id)).AfkType;
    }

    /// <summary>
    /// Retrieves the AFK message length limit for the specified guild.
    /// </summary>
    /// <param name="id">The ID of the guild.</param>
    /// <returns>The maximum allowed length for AFK messages.</returns>
    public async Task<int> GetAfkLength(ulong id)
    {
        return (await GetGuildConfigCached(id)).AfkLength;
    }

    /// <summary>
    /// Retrieves the disabled AFK channels for the specified guild.
    /// </summary>
    /// <param name="id">The ID of the guild.</param>
    /// <returns>A comma-separated list of channel IDs where AFK is disabled.</returns>
    public async Task<string?> GetDisabledAfkChannels(ulong id)
    {
        return (await GetGuildConfigCached(id)).AfkDisabledChannels;
    }

    /// <summary>
    /// Retrieves the AFK timeout for the specified guild.
    /// </summary>
    /// <param name="id">The ID of the guild.</param>
    /// <returns>The timeout in seconds after which AFK status is automatically removed.</returns>
    public async Task<int> GetAfkTimeout(ulong id)
    {
        return (await GetGuildConfigCached(id)).AfkTimeout;
    }

    #endregion

    #region Private Implementation

    private async Task<GuildConfig> GetGuildConfigCached(ulong guildId)
    {
        // Check if we have a valid cached config
        if (guildConfigCache.TryGetValue(guildId, out var cached) &&
            cached.Expiry > DateTime.UtcNow)
        {
            return cached.Config;
        }

        // Get config from service
        var config = await guildSettings.GetGuildConfig(guildId, bypassCache: false);

        // Cache for 15 minutes
        guildConfigCache[guildId] = (config, DateTime.UtcNow.AddMinutes(15));

        return config;
    }

    private void InvalidateGuildConfigCache(ulong guildId)
    {
        guildConfigCache.TryRemove(guildId, out _);
    }

    private async Task EnsureGuildAfksLoaded(ulong guildId)
    {
        // Only load if not already loaded
        if (guildDataLoaded.ContainsKey(guildId))
            return;

        await using var dbContext = await dbProvider.GetContextAsync();
        var guildAfks = await dbContext.Afk
            .AsNoTracking()
            .Where(a => a.GuildId == guildId)
            .OrderByDescending(afk => afk.DateAdded)
            .ToListAsyncEF();

        // Group by user ID and take only the most recent AFK for each user
        var latestAfksPerUser = guildAfks
            .GroupBy(a => a.UserId)
            .Select(g => g.MaxBy(a => a.DateAdded))
            .ToDictionary(a => a.UserId);

        // Cache each user's AFK individually with appropriate expiry
        foreach (var afk in latestAfksPerUser.Values)
        {
            var cacheOptions = new FusionCacheEntryOptions
            {
                Duration = afk.WasTimed && afk.When.HasValue
                    ? TimeSpan.FromMinutes(Math.Max(1, (afk.When.Value - DateTime.UtcNow).TotalMinutes + 5))
                    : TimeSpan.FromHours(12)
            };

            await cache.SetAsync($"{guildId}:{afk.UserId}", afk, cacheOptions);

            // Set up timer for timed AFKs
            if (afk.WasTimed && afk.When.HasValue && afk.When.Value > DateTime.UtcNow)
            {
                ScheduleTimedAfk(afk);
            }
        }

        // Mark guild as loaded
        guildDataLoaded[guildId] = true;
    }

    private void ScheduleTimedAfk(Database.Models.Afk afk)
    {
        var timeToGo = afk.When.Value - DateTime.UtcNow;
        if (timeToGo <= TimeSpan.Zero)
        {
            timeToGo = TimeSpan.Zero;
        }

        // Create a separate state object to avoid closure capture
        var state = new AfkTimerState { GuildId = afk.GuildId, UserId = afk.UserId };
        var timer = new Timer(async _ => await TimedAfkCallback(state), null, timeToGo, Timeout.InfiniteTimeSpan);

        // Remove any existing timer
        if (afkTimers.TryRemove((afk.GuildId, afk.UserId), out var existingTimer))
        {
            existingTimer.Dispose();
        }

        afkTimers[(afk.GuildId, afk.UserId)] = timer;
    }

    private async Task TimedAfkCallback(AfkTimerState state)
    {
        try
        {
            // Load the current AFk entry to ensure we have latest data
            await using var dbContext = await dbProvider.GetContextAsync();
            var afk = await dbContext.Afk
                .FirstOrDefaultAsync(a => a.GuildId == state.GuildId && a.UserId == state.UserId);

            if (afk == null)
                return;

            // Process the timed AFk
            await TimedAfkFinished(afk);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in timed AFK callback for {GuildId}:{UserId}", state.GuildId, state.UserId);
        }
        finally
        {
            // Always clean up the timer
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

    private void CleanupTimers()
{
    try
    {
        Log.Debug("Running AFK timer cleanup, current count: {TimerCount}", afkTimers.Count);

        // Clean up expired guild config cache entries
        var expiredConfigs = guildConfigCache
            .Where(kv => kv.Value.Expiry < DateTime.UtcNow)
            .Select(kv => kv.Key)
            .ToList();

        foreach (var guildId in expiredConfigs)
        {
            guildConfigCache.TryRemove(guildId, out _);
        }

        // Check if any timers are for AFK entries that no longer exist or are past due
        var timersToCheck = afkTimers.ToList();
        foreach (var timerEntry in timersToCheck)
        {
            var (guildId, userId) = timerEntry.Key;
            var afk = cache.GetOrDefaultAsync<Database.Models.Afk>($"{guildId}:{userId}").GetAwaiter().GetResult();

            // If AFK no longer exists or is past due, remove the timer
            if (afk == null || (afk.When.HasValue && afk.When.Value < DateTime.UtcNow))
            {
                if (afkTimers.TryRemove(timerEntry.Key, out var timer))
                {
                    timer.Dispose();
                }
            }
        }

        Task.Run(async () =>
        {
            try
            {
                var oneMonthAgo = DateTime.UtcNow.AddMonths(-1);
                await using var dbContext = await dbProvider.GetContextAsync();

                // Find old AFK entries
                var oldAfks = await dbContext.Afk
                    .Where(a => a.DateAdded < oneMonthAgo)
                    .ToListAsyncEF();

                if (oldAfks.Count > 0)
                {
                    Log.Information("Deleting {Count} AFK entries older than one month", oldAfks.Count);

                    // Delete from database
                    await dbContext.Afk
                        .Where(a => a.DateAdded < oneMonthAgo)
                        .ExecuteDeleteAsync();

                    // Remove from cache
                    foreach (var afk in oldAfks)
                    {
                        await cache.RemoveAsync($"{afk.GuildId}:{afk.UserId}");

                        // Also remove any timers
                        if (afkTimers.TryRemove((afk.GuildId, afk.UserId), out var timer))
                        {
                            await timer.DisposeAsync();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during old AFK entries cleanup");
            }
        });

        Log.Debug("AFK timer cleanup complete, new count: {TimerCount}", afkTimers.Count);
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error during AFK timer cleanup");
    }
}

    private async Task InitializeTimedAfksAsync()
    {
        try
        {
            var now = DateTime.UtcNow;
            await using var dbContext = await dbProvider.GetContextAsync();

            var timedAfks = await dbContext.Afk
                .ToLinqToDB()
                .Where(x => x.WasTimed && x.When > now)
                .ToListAsyncEF();

            Log.Information("Initializing {Count} timed AFKs", timedAfks.Count);

            foreach (var afk in timedAfks)
            {
                var cacheOptions = new FusionCacheEntryOptions
                {
                    Duration = TimeSpan.FromMinutes(Math.Max(1, (afk.When.Value - DateTime.UtcNow).TotalMinutes + 5))
                };

                await cache.SetAsync($"{afk.GuildId}:{afk.UserId}", afk, cacheOptions);
                ScheduleTimedAfk(afk);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error initializing timed AFKs");
        }
    }

    private async Task TimedAfkFinished(Database.Models.Afk afk)
    {
        try
        {
            // Check if the user is still AFK
            if (!await IsAfk(afk.GuildId, afk.UserId))
            {
                return;
            }

            // Reset the user's AFK status
            await AfkSet(afk.GuildId, afk.UserId, "");

            // Retrieve the guild and user
            var guild = client.GetGuild(afk.GuildId);
            if (guild == null) return;

            var user = guild.GetUser(afk.UserId);
            if (user == null) return;

            try
            {
                // Attempt to remove "[AFK]" from the user's nickname
                if (user.Nickname != null && user.Nickname.Contains("[AFK]"))
                {
                    await user.ModifyAsync(x => x.Nickname = user.Nickname.Replace("[AFK]", ""));
                }
            }
            catch
            {
                // Ignore any errors with nickname modification
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in TimedAfkFinished for {GuildId}:{UserId}", afk.GuildId, afk.UserId);
        }
    }

    private async Task MessageReceived(SocketMessage msg)
    {
        if (msg.Author.IsBot)
            return;

        if (msg.Author is not IGuildUser user)
            return;

        try
        {
            // Handle author's AFK status (auto-disable when they talk)
            if (await GetAfkType(user.Guild.Id) is 2 or 4)
            {
                if (await IsAfk(user.Guild.Id, user.Id))
                {
                    var afk = await GetAfk(user.Guild.Id, user.Id);
                    if (afk?.DateAdded != null &&
                        afk.DateAdded.Value.ToLocalTime() <
                        DateTime.Now.AddSeconds(-await GetAfkTimeout(user.Guild.Id)) &&
                        !afk.WasTimed)
                    {
                        // Disable the user's AFK status
                        await AfkSet(user.Guild.Id, user.Id, "");

                        // Send notification that AFK was disabled
                        var ms = await msg.Channel
                            .SendMessageAsync($"Welcome back {user.Mention}, I have disabled your AFK for you.")
                            .ConfigureAwait(false);
                        ms.DeleteAfter(5);

                        try
                        {
                            // Remove the AFK tag from user's nickname
                            if (user.Nickname != null && user.Nickname.Contains("[AFK]"))
                            {
                                await user.ModifyAsync(x => x.Nickname = user.Nickname.Replace("[AFK]", ""));
                            }
                        }
                        catch
                        {
                            // Ignore nickname modification errors
                        }

                        return;
                    }
                }
            }

            // Handle mentions of AFK users
            if (msg.MentionedUsers.Count > 0 && !msg.Author.IsBot)
            {
                var prefix = await guildSettings.GetPrefix(user.Guild);

                // Skip AFK command messages
                if (msg.Content.Contains($"{prefix}afkremove") ||
                    msg.Content.Contains($"{prefix}afkrm") ||
                    msg.Content.Contains($"{prefix}afk"))
                {
                    return;
                }

                // Check if the channel is disabled for AFK
                var disabledChannels = await GetDisabledAfkChannels(user.GuildId);
                if (disabledChannels is not "0" and not null)
                {
                    var channelsList = disabledChannels.Split(",");
                    if (channelsList.Contains(msg.Channel.Id.ToString()))
                        return;
                }

                // Look for mentioned users who are AFK
                foreach (var mentionedUser in msg.MentionedUsers)
                {
                    if (mentionedUser is not IGuildUser mentionedGuildUser)
                        continue;

                    if (!await IsAfk(user.Guild.Id, mentionedUser.Id))
                        continue;

                    var mentionedAfk = await GetAfk(user.Guild.Id, mentionedUser.Id);
                    if (mentionedAfk == null)
                        continue;

                    // Get afk message settings
                    var customAfkMessage = await GetCustomAfkMessage(user.Guild.Id);
                    var afkDeleteTime = await GetAfkDel(user.Guild.Id);

                    // Send default or custom AFK notification
                    if (customAfkMessage is null or "-")
                    {
                        // Send default AFK message
                        var embed = new EmbedBuilder()
                            .WithAuthor(eab => eab
                                .WithName($"{mentionedGuildUser} is currently away")
                                .WithIconUrl(mentionedGuildUser.GetAvatarUrl()))
                            .WithDescription(mentionedAfk.Message
                                .Truncate(await GetAfkLength(user.Guild.Id)))
                            .WithFooter(new EmbedFooterBuilder
                            {
                                Text = $"AFK for {(DateTime.UtcNow - mentionedAfk.DateAdded.Value).Humanize()}"
                            })
                            .WithOkColor()
                            .Build();

                        var components = config.Data.ShowInviteButton
                            ? new ComponentBuilder()
                                .WithButton(style: ButtonStyle.Link,
                                    url: "https://discord.com/oauth2/authorize?client_id=752236274261426212&permissions=8&response_type=code&redirect_uri=https%3A%2F%2Fmewdeko.tech&scope=bot%20applications.commands",
                                    label: "Invite Me!",
                                    emote: "<a:HaneMeow:968564817784877066>".ToIEmote())
                                .Build()
                            : null;

                        var message = await msg.Channel.SendMessageAsync(
                            embed: embed,
                            components: components);

                        if (afkDeleteTime > 0)
                            message.DeleteAfter(afkDeleteTime);
                    }
                    else
                    {
                        var length = await GetAfkLength(user.Guild.Id);
                        // Use custom AFK message with replacements
                        var replacer = new ReplacementBuilder()
                            .WithOverride("%afk.message%",
                                () => mentionedAfk.Message.SanitizeMentions(true)
                                    .Truncate(length))
                            .WithOverride("%afk.user%", () => mentionedGuildUser.ToString())
                            .WithOverride("%afk.user.mention%", () => mentionedGuildUser.Mention)
                            .WithOverride("%afk.user.avatar%", () => mentionedGuildUser.GetAvatarUrl(size: 2048))
                            .WithOverride("%afk.user.id%", () => mentionedGuildUser.Id.ToString())
                            .WithOverride("%afk.triggeruser%", () => msg.Author.ToString().EscapeWeirdStuff())
                            .WithOverride("%afk.triggeruser.avatar%", () => msg.Author.RealAvatarUrl().ToString())
                            .WithOverride("%afk.triggeruser.id%", () => msg.Author.Id.ToString())
                            .WithOverride("%afk.triggeruser.mention%", () => msg.Author.Mention)
                            .WithOverride("%afk.time%", () =>
                                $"{(DateTime.UtcNow - mentionedAfk.DateAdded.Value).Humanize()}")
                            .Build();

                        var parsedMessage = replacer.Replace(customAfkMessage);

                        // Try to parse as smart embed
                        if (SmartEmbed.TryParse(parsedMessage,
                                ((ITextChannel)msg.Channel)?.GuildId ?? 0,
                                out var embed,
                                out var plainText,
                                out var components))
                        {
                            var message = await msg.Channel
                                .SendMessageAsync(plainText, embeds: embed, components: components);

                            if (afkDeleteTime > 0)
                                message.DeleteAfter(afkDeleteTime);
                        }
                        else
                        {
                            // Send as plain text
                            var message = await msg.Channel
                                .SendMessageAsync(parsedMessage.SanitizeMentions(true));

                            if (afkDeleteTime > 0)
                                message.DeleteAfter(afkDeleteTime);
                        }
                    }

                    // Only process the first mentioned AFK user to avoid spam
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in AfkHandler MessageReceived");
        }
    }

    private async Task MessageUpdated(Cacheable<IMessage, ulong> msg, SocketMessage updatedMsg, ISocketMessageChannel channel)
    {
        try
        {
            var message = await msg.GetOrDownloadAsync();
            if (message == null)
                return;

            // Skip if message is too old (30+ minutes)
            var originalDate = message.Timestamp.ToUniversalTime();
            if (DateTime.UtcNow > originalDate.Add(TimeSpan.FromMinutes(30)))
                return;

            // Process as a new message
            await MessageReceived(updatedMsg);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in AfkHandler MessageUpdated");
        }
    }

    private async Task UserTyping(Cacheable<IUser, ulong> user, Cacheable<IMessageChannel, ulong> chan)
    {
        try
        {
            if (user.Value is not IGuildUser guildUser)
                return;

            // Check if the guild has typing-based AFK disable and user is AFK
            if (await GetAfkType(guildUser.GuildId) is 3 or 4 && await IsAfk(guildUser.Guild.Id, guildUser.Id))
            {
                var afkEntry = await GetAfk(guildUser.Guild.Id, user.Id);
                if (afkEntry?.DateAdded != null &&
                    afkEntry.DateAdded.Value.ToLocalTime() <
                    DateTime.Now.AddSeconds(-await GetAfkTimeout(guildUser.GuildId)) &&
                    !afkEntry.WasTimed) // Don't auto-disable timed AFKs
                {
                    // Disable the user's AFK status
                    await AfkSet(guildUser.Guild.Id, guildUser.Id, "");

                    // Send notification
                    var msg = await chan.Value
                        .SendMessageAsync($"Welcome back {user.Value.Mention}! I noticed you typing so I disabled your AFK.");

                    try
                    {
                        // Remove AFK tag from nickname
                        if (guildUser.Nickname != null && guildUser.Nickname.Contains("[AFK]"))
                        {
                            await guildUser.ModifyAsync(x => x.Nickname = guildUser.Nickname.Replace("[AFK]", ""));
                        }
                    }
                    catch
                    {
                        // Ignore nickname errors
                    }

                    // Delete notification after 5 seconds
                    msg.DeleteAfter(5);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in AfkHandler UserTyping");
        }
    }

    /// <summary>
    /// Releases all resources used by the <see cref="AfkService"/>.
    /// </summary>
    public void Dispose()
    {
        if (isDisposed)
            return;

        isDisposed = true;

        // Unsubscribe from events
        eventHandler.MessageReceived -= MessageReceived;
        eventHandler.MessageUpdated -= MessageUpdated;
        eventHandler.UserIsTyping -= UserTyping;

        // Dispose cleanup timer
        cleanupTimer?.Dispose();

        // Dispose all timers
        foreach (var timer in afkTimers.Values)
        {
            timer.Dispose();
        }

        afkTimers.Clear();

        Log.Information("AfkService disposed");
    }

    #endregion
}