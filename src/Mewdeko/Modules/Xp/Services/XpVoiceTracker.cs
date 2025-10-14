using System.Text.Json;
using System.Threading;
using DataModel;
using LinqToDB.Async;
using Mewdeko.Modules.Xp.Models;
using Microsoft.Extensions.Caching.Memory;

namespace Mewdeko.Modules.Xp.Services;

/// <summary>
///     Manages voice channel XP tracking.
/// </summary>
public class XpVoiceTracker : INService, IDisposable
{
    // Constants
    private const int MinUsersForXp = 2;
    private const int MaxConcurrentSessions = 100000;
    private readonly Timer aggressiveCleanupTimer;
    private readonly XpBackgroundProcessor backgroundProcessor;
    private readonly XpCacheManager cacheManager;

    private readonly MemoryCacheOptions cacheOptions = new()
    {
        SizeLimit = 500, ExpirationScanFrequency = TimeSpan.FromMinutes(2)
    };

    // Small cache for channel eligibility only
    private readonly MemoryCache channelEligibilityCache;
    private readonly DiscordShardedClient client;
    private readonly IDataConnectionFactory dbFactory;
    private readonly Timer diagnosticsTimer;

    // Cache TTLs
    private readonly TimeSpan eligibilityCacheTtl = TimeSpan.FromMinutes(2);
    private readonly ILogger<XpVoiceTracker> logger;

    // Session validation interval
    private readonly TimeSpan validationInterval = TimeSpan.FromMinutes(5);

    // Timers for maintenance operations
    private readonly Timer validationTimer;

    // Track active voice sessions with size limit
    private readonly ConcurrentDictionary<(ulong UserId, ulong ChannelId), VoiceSession> voiceSessions = new();

    // Processing flag to prevent overlapping operations
    private int isProcessing;

    /// <summary>
    ///     Initializes a new instance of the <see cref="XpVoiceTracker" /> class.
    /// </summary>
    /// <param name="client">The Discord client.</param>
    /// <param name="dbFactory">The database context provider.</param>
    /// <param name="cacheManager">The cache manager.</param>
    /// <param name="backgroundProcessor">The background processor.</param>
    /// <param name="logger">The logger instance for structured logging.</param>
    public XpVoiceTracker(
        DiscordShardedClient client,
        IDataConnectionFactory dbFactory,
        XpCacheManager cacheManager,
        XpBackgroundProcessor backgroundProcessor, ILogger<XpVoiceTracker> logger)
    {
        this.client = client;
        this.dbFactory = dbFactory;
        this.cacheManager = cacheManager;
        this.backgroundProcessor = backgroundProcessor;
        this.logger = logger;

        // Initialize memory cache for channel eligibility only
        channelEligibilityCache = new MemoryCache(cacheOptions);

        // Initialize validation timer (5 minutes)
        validationTimer = new Timer(ValidateActiveSessions, null,
            TimeSpan.FromMinutes(1), validationInterval);

        // Initialize aggressive cleanup timer every 3 minutes
        aggressiveCleanupTimer = new Timer(AggressiveCleanup, null,
            TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(3));

        // Initialize diagnostics timer to log stats every hour
        diagnosticsTimer = new Timer(_ => LogDiagnostics(), null,
            TimeSpan.FromMinutes(10), TimeSpan.FromHours(1));
    }

    /// <summary>
    ///     Disposes resources used by the voice tracker.
    /// </summary>
    public void Dispose()
    {
        validationTimer.Dispose();
        aggressiveCleanupTimer.Dispose();
        diagnosticsTimer.Dispose();
        voiceSessions.Clear();
        channelEligibilityCache.Dispose();
    }

    /// <summary>
    ///     Handles voice state updates for voice XP tracking.
    /// </summary>
    /// <param name="user">The user whose voice state changed.</param>
    /// <param name="before">The previous voice state.</param>
    /// <param name="after">The new voice state.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task HandleVoiceStateUpdate(SocketUser user, SocketVoiceState before, SocketVoiceState after)
    {
        if (user is not SocketGuildUser guildUser || guildUser.IsBot)
            return;

        try
        {
            var now = DateTime.UtcNow;
            var guildId = guildUser.Guild.Id;
            var userId = guildUser.Id;

            // Get guild settings
            var settings = await cacheManager.GetGuildXpSettingsAsync(guildId).ConfigureAwait(false);

            // Skip if voice XP is disabled
            if (settings.XpGainDisabled || settings.VoiceXpPerMinute <= 0)
                return;

            // User left voice channel
            if (before.VoiceChannel != null &&
                (after.VoiceChannel == null || after.VoiceChannel.Id != before.VoiceChannel.Id))
            {
                var channelId = before.VoiceChannel.Id;
                var key = (userId, channelId);

                if (voiceSessions.TryRemove(key, out var session))
                {
                    // If session exists, end eligibility period and calculate XP
                    if (session.CurrentlyEligible)
                    {
                        session.EndEligiblePeriod(now);
                    }

                    await CalculateAndAwardXp(guildUser, before.VoiceChannel, session).ConfigureAwait(false);
                }
            }

            // User joined voice channel
            if (after.VoiceChannel != null &&
                (before.VoiceChannel == null || before.VoiceChannel.Id != after.VoiceChannel.Id))
            {
                await StartVoiceSession(guildUser, after.VoiceChannel, now).ConfigureAwait(false);
            }

            // User remained in same channel but state changed (mute/deafen)
            if (after.VoiceChannel != null && before.VoiceChannel != null &&
                after.VoiceChannel.Id == before.VoiceChannel.Id &&
                (IsParticipatingInVoice(before) != IsParticipatingInVoice(after)))
            {
                await UpdateSessionEligibility(guildUser, after.VoiceChannel, now).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling voice state update for {UserId} in {GuildId}",
                user.Id, guildUser.Guild.Id);
        }
    }

    /// <summary>
    ///     Scans a guild's voice channels and sets up voice sessions.
    /// </summary>
    /// <param name="guild">The guild to scan.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task ScanGuildVoiceChannels(SocketGuild guild)
    {
        try
        {
            var now = DateTime.UtcNow;
            var guildId = guild.Id;

            // Get guild settings
            var settings = await cacheManager.GetGuildXpSettingsAsync(guildId).ConfigureAwait(false);

            // Skip if voice XP is disabled
            if (settings.XpGainDisabled || settings.VoiceXpPerMinute <= 0 || settings.VoiceXpTimeout <= 0)
                return;

            // Process all voice channels
            foreach (var channel in guild.VoiceChannels)
            {
                // Check if channel has enough users for XP
                var eligibleUsers = channel.Users.Where(u => !u.IsBot && IsParticipatingInVoice(u)).ToList();
                var isEligible = eligibleUsers.Count >= MinUsersForXp;

                // Update channel eligibility cache
                var cacheKey = $"{guildId}:{channel.Id}";
                var options = new MemoryCacheEntryOptions()
                    .SetSize(1)
                    .SetAbsoluteExpiration(eligibilityCacheTtl);
                channelEligibilityCache.Set(cacheKey, isEligible, options);

                // Skip if channel doesn't meet minimum requirements
                if (!isEligible)
                    continue;

                // Check channel exclusion using Redis
                if (await IsChannelExcludedAsync(guildId, channel.Id).ConfigureAwait(false))
                    continue;

                // Start sessions for eligible users
                foreach (var user in eligibleUsers)
                {
                    if (await IsUserExcludedAsync(guildId, user.Id).ConfigureAwait(false))
                        continue;

                    await StartVoiceSession(user, channel, now).ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error scanning voice channels for guild {GuildId}", guild.Id);
        }
    }

    /// <summary>
    ///     Starts a voice session for a user or updates an existing one.
    /// </summary>
    /// <param name="user">The guild user.</param>
    /// <param name="voiceChannel">The voice channel.</param>
    /// <param name="timestamp">The current timestamp.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task StartVoiceSession(SocketGuildUser user, SocketVoiceChannel voiceChannel, DateTime timestamp)
    {
        var guildId = user.Guild.Id;
        var userId = user.Id;
        var channelId = voiceChannel.Id;

        try
        {
            // Check session limit before adding new sessions
            if (voiceSessions.Count >= MaxConcurrentSessions)
            {
                logger.LogWarning("Voice session limit reached ({Limit}), skipping new session", MaxConcurrentSessions);
                return;
            }

            // Pre-validate: Check if user is excluded using Redis
            if (await IsUserExcludedAsync(guildId, userId).ConfigureAwait(false))
                return;

            // Pre-validate: Check if channel is excluded using Redis
            if (await IsChannelExcludedAsync(guildId, channelId).ConfigureAwait(false))
                return;

            // Pre-validate: Check if user is participating (not muted/deafened)
            if (!IsParticipatingInVoice(user))
            {
                logger.LogDebug(
                    "Skipping voice session creation for non-participating user {UserId} in channel {ChannelId}",
                    userId, channelId);
                return;
            }

            // Pre-validate: Check if channel has minimum users for XP eligibility
            var eligibleUserCount = voiceChannel.Users.Count(u => !u.IsBot && IsParticipatingInVoice(u));
            if (eligibleUserCount < MinUsersForXp)
            {
                logger.LogDebug(
                    "Skipping voice session creation - channel {ChannelId} has only {Count} eligible users (minimum {Min})",
                    channelId, eligibleUserCount, MinUsersForXp);
                return;
            }

            // Get or create session - we already validated eligibility above
            var key = (userId, channelId);
            var session = voiceSessions.GetOrAdd(key, _ => new VoiceSession
            {
                UserId = userId,
                GuildId = guildId,
                ChannelId = channelId,
                JoinTime = timestamp,
                CurrentlyEligible = false,
                LastEligibilityChange = timestamp
            });

            // Since we pre-validated eligibility, start the eligible period immediately
            session.StartEligiblePeriod(timestamp);

            // Update session in dictionary
            voiceSessions[key] = session;

            // Log session creation for monitoring
            if (voiceSessions.Count % 1000 == 0 || voiceSessions.Count > MaxConcurrentSessions * 0.8)
            {
                logger.LogInformation(
                    "Voice session created - Active sessions: {ActiveSessions}/{MaxSessions} ({Percentage:F1}%)",
                    voiceSessions.Count, MaxConcurrentSessions, (voiceSessions.Count * 100.0) / MaxConcurrentSessions);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error starting voice session for user {UserId} in guild {GuildId}",
                userId, guildId);
        }
    }

    /// <summary>
    ///     Updates the eligibility status of an existing session.
    /// </summary>
    /// <param name="user">The guild user.</param>
    /// <param name="voiceChannel">The voice channel.</param>
    /// <param name="timestamp">The current timestamp.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task UpdateSessionEligibility(SocketGuildUser user, SocketVoiceChannel voiceChannel,
        DateTime timestamp)
    {
        var guildId = user.Guild.Id;
        var userId = user.Id;
        var channelId = voiceChannel.Id;
        var key = (userId, channelId);

        // Only proceed if we have an existing session
        if (!voiceSessions.TryGetValue(key, out var session))
            return;

        try
        {
            // Check overall channel eligibility
            var isChannelEligible =
                await IsChannelEligibleAsync(guildId, channelId, voiceChannel).ConfigureAwait(false);

            // Is this user participating
            var isUserParticipating = IsParticipatingInVoice(user);

            // Update session eligibility based on current conditions
            var shouldBeEligible = isChannelEligible && isUserParticipating;

            switch (shouldBeEligible)
            {
                case true when !session.CurrentlyEligible:
                    session.StartEligiblePeriod(timestamp);
                    break;
                case false when session.CurrentlyEligible:
                    session.EndEligiblePeriod(timestamp);
                    break;
            }

            // Update session in dictionary
            voiceSessions[key] = session;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating session eligibility for user {UserId} in guild {GuildId}",
                userId, guildId);
        }
    }

    /// <summary>
    ///     Calculates and awards XP when a user leaves a voice channel.
    /// </summary>
    /// <param name="user">The guild user.</param>
    /// <param name="voiceChannel">The voice channel.</param>
    /// <param name="session">The voice session.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task CalculateAndAwardXp(SocketGuildUser user, SocketVoiceChannel voiceChannel, VoiceSession session)
    {
        var guildId = user.Guild.Id;
        var userId = user.Id;
        var channelId = voiceChannel.Id;
        var xpAmount = 0;
        GuildXpSetting settings = null;

        try
        {
            // Get settings
            settings = await cacheManager.GetGuildXpSettingsAsync(guildId).ConfigureAwait(false);

            // Skip if voice XP is disabled
            if (settings.VoiceXpPerMinute <= 0 || settings.VoiceXpTimeout <= 0)
                return;

            // Calculate total eligible duration
            var totalEligibleDuration = session.GetEligibleDuration(DateTime.UtcNow);

            // Skip if duration is too short
            if (totalEligibleDuration.TotalMinutes < 0.25) // 15 seconds minimum
                return;

            // Cap at maximum timeout to prevent abuse
            var cappedMinutes = Math.Min(totalEligibleDuration.TotalMinutes, settings.VoiceXpTimeout);

            // Calculate XP to award
            var baseXpPerMinute = Math.Min(settings.VoiceXpPerMinute, XpService.MaxVoiceXpPerMinute);
            xpAmount = (int)(baseXpPerMinute * cappedMinutes);

            switch (xpAmount)
            {
                case <= 0:
                    return;
                case >= 5:
                    xpAmount = (int)(xpAmount *
                                     await cacheManager.GetEffectiveMultiplierAsync(userId, guildId, channelId)
                                         .ConfigureAwait(false));
                    break;
            }

            // Skip if still no XP after multipliers
            if (xpAmount <= 0)
                return;

            // Use the injected background processor

            // Queue XP gain
            backgroundProcessor.QueueXpGain(
                guildId,
                userId,
                xpAmount,
                channelId,
                XpSource.Voice
            );

            // Log significant XP awards
            if (xpAmount > 50)
            {
                logger.LogInformation(
                    "Awarded {XpAmount} voice XP to user {UserId} in guild {GuildId} for {Minutes:F1} minutes",
                    xpAmount, userId, guildId, cappedMinutes);
            }
        }
        catch (Exception ex)
        {
            logger.LogInformation("{GuildId}|{UserId}|{XpAmount}|{ChannelId}|{VoiceXpMinutes}", guildId, userId,
                xpAmount,
                channelId, JsonSerializer.Serialize(settings));
            logger.LogError(ex, "Error calculating XP for user {UserId} in guild {GuildId}",
                userId, guildId);
        }
    }

    /// <summary>
    ///     Periodically validates active sessions to ensure channel eligibility is current.
    /// </summary>
    /// <param name="state">The state object.</param>
    private async void ValidateActiveSessions(object state)
    {
        if (Interlocked.Exchange(ref isProcessing, 1) != 0)
            return;

        var startTime = DateTime.UtcNow;
        var sessionsUpdated = 0;

        try
        {
            // Skip if no sessions to validate
            if (voiceSessions.IsEmpty)
                return;

            // Process in batches to avoid memory spikes
            var batchesProcessed = 0;

            var sessionsByChannel = voiceSessions.Values
                .GroupBy(s => (s.GuildId, s.ChannelId))
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var ((guildId, channelId), sessions) in sessionsByChannel)
            {
                try
                {
                    // Skip if guild is excluded using Redis
                    if (await IsGuildExcludedAsync(guildId).ConfigureAwait(false))
                        continue;

                    // Skip if channel is excluded using Redis
                    if (await IsChannelExcludedAsync(guildId, channelId).ConfigureAwait(false))
                        continue;

                    // Get guild and channel
                    var guild = client.GetGuild(guildId);
                    var channel = guild?.GetVoiceChannel(channelId);

                    if (guild == null || channel == null)
                        continue;

                    // Check channel eligibility (need at least 2 participating users)
                    var eligibleUserCount = channel.Users.Count(u => !u.IsBot && IsParticipatingInVoice(u));
                    var isEligible = eligibleUserCount >= MinUsersForXp;

                    // Update channel eligibility cache
                    var cacheKey = $"{guildId}:{channelId}";
                    var options = new MemoryCacheEntryOptions()
                        .SetSize(1)
                        .SetAbsoluteExpiration(eligibilityCacheTtl);
                    channelEligibilityCache.Set(cacheKey, isEligible, options);

                    // Skip if no change in eligibility
                    var wasEligible = sessions.Any(s => s.CurrentlyEligible);
                    if (isEligible == wasEligible)
                        continue;

                    // Update all sessions for this channel
                    foreach (var session in sessions)
                    {
                        try
                        {
                            var user = guild.GetUser(session.UserId);

                            // Skip if user no longer exists or isn't in this channel
                            if (user == null || user.VoiceChannel?.Id != channelId)
                                continue;

                            switch (isEligible)
                            {
                                case true when !session.CurrentlyEligible && IsParticipatingInVoice(user):
                                    session.StartEligiblePeriod(startTime);
                                    voiceSessions[(session.UserId, session.ChannelId)] = session;
                                    sessionsUpdated++;
                                    break;
                                case false when session.CurrentlyEligible:
                                    session.EndEligiblePeriod(startTime);
                                    voiceSessions[(session.UserId, session.ChannelId)] = session;
                                    sessionsUpdated++;
                                    break;
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Error updating session for user {UserId} in guild {GuildId}",
                                session.UserId, guildId);
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error validating channel {ChannelId} in guild {GuildId}",
                        channelId, guildId);
                }

                // Add a delay between batches to reduce CPU spikes
                batchesProcessed++;
                if (batchesProcessed % 5 == 0)
                {
                    await Task.Delay(50).ConfigureAwait(false);
                }
            }

            if (sessionsUpdated > 0)
            {
                var elapsedMs = (DateTime.UtcNow - startTime).TotalMilliseconds;
                logger.LogDebug("Updated eligibility for {Count} voice sessions in {Time:F1}ms",
                    sessionsUpdated, elapsedMs);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error validating voice sessions");
        }
        finally
        {
            Interlocked.Exchange(ref isProcessing, 0);
        }
    }

    /// <summary>
    ///     Performs aggressive cleanup of stale data to prevent memory leaks.
    /// </summary>
    /// <param name="state">The state object.</param>
    private async void AggressiveCleanup(object state)
    {
        var sessionsRemoved = 0;

        try
        {
            // Take snapshot of session keys to avoid modification during iteration
            var sessionKeys = voiceSessions.Keys.ToList();

            // Process in batches to reduce CPU spikes
            const int batchSize = 100;
            for (var i = 0; i < sessionKeys.Count; i += batchSize)
            {
                var batchKeys = sessionKeys.Skip(i).Take(batchSize).ToList();
                foreach (var key in batchKeys)
                {
                    if (!voiceSessions.TryGetValue(key, out var session))
                        continue;

                    try
                    {
                        var guild = client.GetGuild(session.GuildId);
                        if (guild == null)
                        {
                            voiceSessions.TryRemove(key, out _);
                            sessionsRemoved++;
                            continue;
                        }

                        var user = guild.GetUser(session.UserId);
                        if (user == null)
                        {
                            voiceSessions.TryRemove(key, out _);
                            sessionsRemoved++;
                            continue;
                        }

                        // Check if user is still in this voice channel
                        if (user.VoiceChannel == null || user.VoiceChannel.Id != session.ChannelId)
                        {
                            voiceSessions.TryRemove(key, out _);
                            sessionsRemoved++;
                            continue;
                        }

                        // Additional cleanup checks for edge cases
                        var channel = guild.GetVoiceChannel(session.ChannelId);
                        if (channel == null)
                        {
                            // Channel was deleted
                            voiceSessions.TryRemove(key, out _);
                            sessionsRemoved++;
                            continue;
                        }

                        // Check if session has been running too long without activity (over 6 hours)
                        var sessionAge = DateTime.UtcNow - session.JoinTime;
                        if (sessionAge.TotalHours > 6)
                        {
                            logger.LogDebug(
                                "Removing stale voice session for user {UserId} - session age: {Hours:F1} hours",
                                session.UserId, sessionAge.TotalHours);
                            voiceSessions.TryRemove(key, out _);
                            sessionsRemoved++;
                            continue;
                        }

                        // Check if channel no longer meets minimum user requirements
                        var currentEligibleUsers = channel.Users.Count(u => !u.IsBot && IsParticipatingInVoice(u));
                        if (currentEligibleUsers < MinUsersForXp && session.CurrentlyEligible)
                        {
                            // End eligibility period but keep session for potential future eligibility
                            session.EndEligiblePeriod(DateTime.UtcNow);
                            voiceSessions[key] = session;
                        }
                    }
                    catch
                    {
                        // If any error occurs, remove the key to be safe
                        voiceSessions.TryRemove(key, out _);
                        sessionsRemoved++;
                    }
                }

                // Add a delay between batches
                await Task.Delay(50).ConfigureAwait(false);
            }

            // Compact the memory cache
            channelEligibilityCache.Compact(100);

            if (sessionsRemoved > 0)
            {
                var utilizationPercent = (voiceSessions.Count * 100.0) / MaxConcurrentSessions;
                logger.LogInformation("Aggressive voice tracker cleanup: removed {Count} stale sessions, " +
                                      "active sessions: {Active}/{Max} ({Percentage:F1}%)",
                    sessionsRemoved, voiceSessions.Count, MaxConcurrentSessions, utilizationPercent);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in aggressive voice tracker cleanup");
        }
    }

    /// <summary>
    ///     Checks if a guild is excluded from XP gain using Redis.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>A task representing the asynchronous operation with a boolean result.</returns>
    private async Task<bool> IsGuildExcludedAsync(ulong guildId)
    {
        try
        {
            var settings = await cacheManager.GetGuildXpSettingsAsync(guildId).ConfigureAwait(false);
            return settings.XpGainDisabled;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    ///     Checks if a channel is excluded using Redis.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="channelId">The channel ID.</param>
    /// <returns>A task representing the asynchronous operation with a boolean result.</returns>
    private async Task<bool> IsChannelExcludedAsync(ulong guildId, ulong channelId)
    {
        // Check from database using Redis caching
        await using var db = await dbFactory.CreateConnectionAsync();
        var isExcluded = await db.XpExcludedItems
            .AnyAsync(x => x.GuildId == guildId &&
                           x.ItemId == channelId &&
                           x.ItemType == (int)ExcludedItemType.Channel)
            .ConfigureAwait(false);

        return isExcluded;
    }

    /// <summary>
    ///     Checks if a user is excluded using Redis.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <returns>A task representing the asynchronous operation with a boolean result.</returns>
    private async Task<bool> IsUserExcludedAsync(ulong guildId, ulong userId)
    {
        // Check database for user exclusion
        await using var db = await dbFactory.CreateConnectionAsync();

        // Direct user exclusion
        var isExcluded = await db.XpExcludedItems
            .AnyAsync(x => x.GuildId == guildId &&
                           x.ItemId == userId &&
                           x.ItemType == (int)ExcludedItemType.User)
            .ConfigureAwait(false);

        if (!isExcluded)
        {
            // User could be excluded by role
            var user = client.GetGuild(guildId)?.GetUser(userId);
            if (user != null)
            {
                var excludedRoles = await db.XpExcludedItems
                    .Where(x => x.GuildId == guildId && x.ItemType == (int)ExcludedItemType.Role)
                    .Select(x => x.ItemId)
                    .ToListAsync()
                    .ConfigureAwait(false);

                isExcluded = user.Roles.Any(r => excludedRoles.Contains(r.Id));
            }
        }

        return isExcluded;
    }

    /// <summary>
    ///     Checks if a channel is eligible for XP (has at least 2 users).
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="channelId">The channel ID.</param>
    /// <param name="voiceChannel">The voice channel, if available.</param>
    /// <returns>A task representing the asynchronous operation with a boolean result.</returns>
    private Task<bool> IsChannelEligibleAsync(ulong guildId, ulong channelId,
        SocketVoiceChannel? voiceChannel = null)
    {
        // Check local cache for recent results
        var cacheKey = $"{guildId}:{channelId}";
        if (channelEligibilityCache.TryGetValue(cacheKey, out bool cachedResult))
        {
            return Task.FromResult(cachedResult);
        }

        // If we have the channel object, check it directly
        if (voiceChannel != null)
        {
            var isEligible = voiceChannel.Users.Count(u => !u.IsBot && IsParticipatingInVoice(u)) >= MinUsersForXp;
            var options = new MemoryCacheEntryOptions()
                .SetSize(1)
                .SetAbsoluteExpiration(eligibilityCacheTtl);
            channelEligibilityCache.Set(cacheKey, isEligible, options);
            return Task.FromResult(isEligible);
        }

        // Otherwise, get the channel and check
        var guild = client.GetGuild(guildId);
        var channel = guild?.GetVoiceChannel(channelId);

        if (channel == null)
        {
            var options = new MemoryCacheEntryOptions()
                .SetSize(1)
                .SetAbsoluteExpiration(eligibilityCacheTtl);
            channelEligibilityCache.Set(cacheKey, false, options);
            return Task.FromResult(false);
        }

        var eligibleCount = channel.Users.Count(u => !u.IsBot && IsParticipatingInVoice(u));
        var eligible = eligibleCount >= MinUsersForXp;

        var cacheEntryOptions = new MemoryCacheEntryOptions()
            .SetSize(1)
            .SetAbsoluteExpiration(eligibilityCacheTtl);
        channelEligibilityCache.Set(cacheKey, eligible, cacheEntryOptions);
        return Task.FromResult(eligible);
    }

    /// <summary>
    ///     Determines if a user is actively participating in a voice channel.
    /// </summary>
    /// <param name="user">The voice state user.</param>
    /// <returns>True if the user is participating, false otherwise.</returns>
    private static bool IsParticipatingInVoice(IVoiceState user)
    {
        return !user.IsDeafened && !user.IsMuted && !user.IsSelfDeafened && !user.IsSelfMuted &&
               user.VoiceChannel is not null;
    }

    /// <summary>
    ///     Logs diagnostic information about the voice tracker.
    /// </summary>
    private void LogDiagnostics()
    {
        var utilizationPercent = (voiceSessions.Count * 100.0) / MaxConcurrentSessions;
        var eligibleSessions = voiceSessions.Values.Count(s => s.CurrentlyEligible);

        logger.LogInformation(
            "XpVoiceTracker stats: Active sessions: {SessionCount}/{MaxSessions} ({Percentage:F1}%), " +
            "Eligible sessions: {EligibleCount}, Channel eligibility cache: {ChannelCount}",
            voiceSessions.Count, MaxConcurrentSessions, utilizationPercent,
            eligibleSessions, channelEligibilityCache.Count);

        // Warning if utilization is high
        if (utilizationPercent > 80)
        {
            logger.LogWarning(
                "Voice session utilization is high ({Percentage:F1}%) - consider monitoring for potential issues",
                utilizationPercent);
        }
    }

    /// <summary>
    ///     Represents a voice session for a user in a voice channel.
    /// </summary>
    private class VoiceSession
    {
        /// <summary>
        ///     Gets or sets the user ID.
        /// </summary>
        public ulong UserId { get; set; }

        /// <summary>
        ///     Gets or sets the guild ID.
        /// </summary>
        public ulong GuildId { get; set; }

        /// <summary>
        ///     Gets or sets the channel ID.
        /// </summary>
        public ulong ChannelId { get; set; }

        /// <summary>
        ///     Gets or sets the join time.
        /// </summary>
        public DateTime JoinTime { get; set; }

        /// <summary>
        ///     Gets or sets the total eligible duration.
        /// </summary>
        public TimeSpan TotalEligibleDuration { get; set; } = TimeSpan.Zero;

        /// <summary>
        ///     Gets or sets the last eligibility start time.
        /// </summary>
        public DateTime? LastEligibilityStart { get; set; }

        /// <summary>
        ///     Gets or sets a value indicating whether the session is currently eligible.
        /// </summary>
        public bool CurrentlyEligible { get; set; }

        /// <summary>
        ///     Gets or sets the last time the eligibility state changed.
        /// </summary>
        public DateTime LastEligibilityChange { get; set; }

        /// <summary>
        ///     Calculates the total eligible duration so far.
        /// </summary>
        /// <param name="referenceTime">The reference time to calculate up to.</param>
        /// <returns>The total eligible duration.</returns>
        public TimeSpan GetEligibleDuration(DateTime referenceTime)
        {
            if (!CurrentlyEligible)
                return TotalEligibleDuration;
            if (!LastEligibilityStart.HasValue)
                return TotalEligibleDuration;

            return TotalEligibleDuration + (referenceTime - LastEligibilityStart.Value);
        }

        /// <summary>
        ///     Starts tracking eligible time.
        /// </summary>
        /// <param name="timestamp">The timestamp when eligibility started.</param>
        public void StartEligiblePeriod(DateTime timestamp)
        {
            if (CurrentlyEligible)
                return;

            CurrentlyEligible = true;
            LastEligibilityChange = timestamp;
            LastEligibilityStart = timestamp;
        }

        /// <summary>
        ///     Ends current eligible period.
        /// </summary>
        /// <param name="timestamp">The timestamp when eligibility ended.</param>
        public void EndEligiblePeriod(DateTime timestamp)
        {
            if (!CurrentlyEligible || !LastEligibilityStart.HasValue)
                return;

            TotalEligibleDuration += timestamp - LastEligibilityStart.Value;
            CurrentlyEligible = false;
            LastEligibilityChange = timestamp;
            LastEligibilityStart = null;
        }
    }
}