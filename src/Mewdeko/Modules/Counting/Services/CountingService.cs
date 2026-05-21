using System.Text.RegularExpressions;
using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Database.DbContextStuff;
using Mewdeko.Modules.Counting.Common;
using Mewdeko.Services.Strings;
using Microsoft.Extensions.Caching.Memory;

namespace Mewdeko.Modules.Counting.Services;

/// <summary>
/// Core service for managing counting functionality across Discord guilds.
/// </summary>
public class CountingService : INService, IReadyExecutor
{
    // Cache keys
    private const string COUNTING_CHANNEL_CACHE_KEY = "counting_channel_{0}";
    private const string COUNTING_CONFIG_CACHE_KEY = "counting_config_{0}";
    private const string USER_COOLDOWN_CACHE_KEY = "counting_cooldown_{0}_{1}";

    // Regex patterns for different number formats
    private static readonly Dictionary<CountingPattern, Regex> PatternRegexes = new()
    {
        [CountingPattern.Normal] = new Regex(@"^\s*(\d+)\s*$", RegexOptions.Compiled),
        [CountingPattern.Roman] = new Regex(@"^\s*([IVXLCDM]+)\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        [CountingPattern.Binary] = new Regex(@"^\s*([01]+)\s*$", RegexOptions.Compiled),
        [CountingPattern.Hexadecimal] = new Regex(@"^\s*([0-9A-Fa-f]+)\s*$", RegexOptions.Compiled),
        [CountingPattern.Words] =
            new Regex(@"^\s*(\w+(?:\s+\w+)*)\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase)
    };

    private readonly IMemoryCache cache;
    private readonly DiscordShardedClient client;
    private readonly IDataConnectionFactory dbFactory;
    private readonly EventHandler eventHandler;
    private readonly ILogger<CountingService> logger;
    private readonly CountingModerationService moderationService;
    private readonly CountingStatsService statsService;
    private readonly GeneratedBotStrings strings;

    /// <summary>
    /// Initializes a new instance of the CountingService.
    /// </summary>
    public CountingService(
        IDataConnectionFactory dbFactory,
        IMemoryCache cache,
        ILogger<CountingService> logger,
        DiscordShardedClient client,
        CountingStatsService statsService,
        CountingModerationService moderationService,
        EventHandler eventHandler, GeneratedBotStrings strings)
    {
        this.dbFactory = dbFactory;
        this.cache = cache;
        this.logger = logger;
        this.client = client;
        this.statsService = statsService;
        this.moderationService = moderationService;
        this.eventHandler = eventHandler;
        this.strings = strings;

        // Subscribe to message events
        this.eventHandler.Subscribe("MessageReceived", "CountingService", MessageReceived);
        this.eventHandler.Subscribe("MessageUpdated", "CountingService", MessageUpdated);
        this.eventHandler.Subscribe("MessageDeleted", "CountingService", MessageDeleted);
    }

    /// <summary>
    /// Initializes the counting service when the bot is ready.
    /// </summary>
    public async Task OnReadyAsync()
    {
        logger.LogInformation("Counting Service Ready");
        await Task.CompletedTask;
    }

    /// <summary>
    /// Gets counting channel configuration, with caching.
    /// </summary>
    public async Task<CountingChannel?> GetCountingChannelAsync(ulong channelId)
    {
        var cacheKey = string.Format(COUNTING_CHANNEL_CACHE_KEY, channelId);

        if (cache.TryGetValue(cacheKey, out CountingChannel? cachedChannel))
            return cachedChannel;

        await using var db = await dbFactory.CreateConnectionAsync();
        var channel = await db.CountingChannels
            .FirstOrDefaultAsync(x => x.ChannelId == channelId && x.IsActive);

        if (channel != null)
        {
            cache.Set(cacheKey, channel, TimeSpan.FromMinutes(10));
        }

        return channel;
    }

    /// <summary>
    /// Gets counting channel configuration with settings.
    /// </summary>
    public async Task<CountingChannelConfig?> GetCountingConfigAsync(ulong channelId)
    {
        var cacheKey = string.Format(COUNTING_CONFIG_CACHE_KEY, channelId);

        if (cache.TryGetValue(cacheKey, out CountingChannelConfig? cachedConfig))
            return cachedConfig;

        await using var db = await dbFactory.CreateConnectionAsync();
        var config = await db.CountingChannelConfigs
            .FirstOrDefaultAsync(x => x.ChannelId == channelId);

        if (config != null)
        {
            cache.Set(cacheKey, config, TimeSpan.FromMinutes(10));
        }

        return config;
    }

    /// <summary>
    /// Sets up a new counting channel.
    /// </summary>
    public async Task<CountingSetupResult> SetupCountingChannelAsync(
        ulong guildId,
        ulong channelId,
        long startNumber = 1,
        int increment = 1)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        // Check if channel already exists
        var existing = await db.CountingChannels
            .FirstOrDefaultAsync(x => x.ChannelId == channelId);

        if (existing != null)
        {
            return new CountingSetupResult
            {
                Success = false, ErrorMessage = "Counting is already set up in this channel.", Channel = existing
            };
        }

        // Create new counting channel
        var newChannel = new CountingChannel
        {
            GuildId = guildId,
            ChannelId = channelId,
            CurrentNumber = startNumber - increment, // So the first count will be startNumber
            StartNumber = startNumber,
            Increment = increment,
            LastUserId = 0,
            LastMessageId = 0,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            HighestNumber = 0,
            TotalCounts = 0
        };

        await db.InsertAsync(newChannel);

        // Create default configuration
        var config = new CountingChannelConfig
        {
            ChannelId = channelId,
            AllowRepeatedUsers = false,
            Cooldown = 0,
            MaxNumber = 0,
            ResetOnError = false,
            DeleteWrongMessages = true,
            Pattern = (int)CountingPattern.Normal,
            NumberBase = 10,
            EnableAchievements = true,
            EnableCompetitions = true
        };

        await db.InsertAsync(config);

        // Clear cache
        cache.Remove(string.Format(COUNTING_CHANNEL_CACHE_KEY, channelId));

        // Log event
        await LogEventAsync(channelId, CountingEventType.ChannelSetup, 0, null, startNumber, 0);

        return new CountingSetupResult
        {
            Success = true, Channel = newChannel, Config = config
        };
    }

    /// <summary>
    /// Processes a counting attempt from a user message.
    /// </summary>
    public async Task<CountingResult> ProcessCountingAttemptAsync(
        ulong channelId,
        ulong userId,
        ulong messageId,
        string messageContent)
    {
        var channel = await GetCountingChannelAsync(channelId);
        if (channel == null)
            return new CountingResult
            {
                Success = false, ErrorType = CountingError.NotSetup
            };

        var config = await GetCountingConfigAsync(channelId);
        if (config == null)
            return new CountingResult
            {
                Success = false, ErrorType = CountingError.ConfigNotFound
            };

        // Check if user is on cooldown
        if (await IsUserOnCooldownAsync(channelId, userId, config.Cooldown))
            return new CountingResult
            {
                Success = false, ErrorType = CountingError.OnCooldown
            };

        // Check if same user is trying to count again (if not allowed)
        if (!config.AllowRepeatedUsers && channel.LastUserId == userId && userId != 0)
            return new CountingResult
            {
                Success = false, ErrorType = CountingError.SameUserRepeating
            };

        // Parse the number based on the pattern
        var parseResult = await ParseNumberAsync(messageContent, (CountingPattern)config.Pattern, config.NumberBase);
        if (!parseResult.Success)
            return new CountingResult
            {
                Success = false, ErrorType = CountingError.InvalidNumber
            };

        // Calculate expected number
        var expectedNumber = channel.CurrentNumber + channel.Increment;

        // Validate the number
        if (parseResult.Number != expectedNumber)
        {
            await HandleWrongNumberAsync(channel, config, userId, messageId, parseResult.Number, expectedNumber);
            return new CountingResult
            {
                Success = false,
                ErrorType = CountingError.WrongNumber,
                ExpectedNumber = expectedNumber,
                ActualNumber = parseResult.Number
            };
        }

        // Check maximum number limit
        if (config.MaxNumber > 0 && parseResult.Number > config.MaxNumber)
        {
            await HandleMaxNumberReachedAsync(channel, config, userId);
            return new CountingResult
            {
                Success = false, ErrorType = CountingError.MaxNumberReached
            };
        }

        // Update the counting channel
        await UpdateCountingChannelAsync(channel, parseResult.Number, userId, messageId);

        // Update user statistics
        await statsService.UpdateUserStatsAsync(channelId, userId, parseResult.Number);

        // Set user cooldown
        if (config.Cooldown > 0)
            SetUserCooldown(channelId, userId, config.Cooldown);

        // Check for milestones
        await CheckForMilestonesAsync(channelId, parseResult.Number, userId);

        // Log the successful count
        await LogEventAsync(channelId, CountingEventType.SuccessfulCount, userId,
            channel.CurrentNumber, parseResult.Number, messageId);

        return new CountingResult
        {
            Success = true, Number = parseResult.Number, IsNewRecord = parseResult.Number > channel.HighestNumber
        };
    }

    /// <summary>
    /// Resets a counting channel to a specific number.
    /// </summary>
    public async Task<bool> ResetCountingChannelAsync(ulong channelId, long newNumber, ulong resetBy,
        string? reason = null)
    {
        var channel = await GetCountingChannelAsync(channelId);
        if (channel == null) return false;

        await using var db = await dbFactory.CreateConnectionAsync();

        var oldNumber = channel.CurrentNumber;

        await db.CountingChannels
            .Where(x => x.ChannelId == channelId)
            .UpdateAsync(x => new CountingChannel
            {
                CurrentNumber = newNumber, LastUserId = 0, LastMessageId = 0
            });

        // Clear cache
        cache.Remove(string.Format(COUNTING_CHANNEL_CACHE_KEY, channelId));

        // Log the reset
        await LogEventAsync(channelId, CountingEventType.ManualReset, resetBy, oldNumber, newNumber, 0, reason);

        return true;
    }

    /// <summary>
    /// Creates a save point for a counting channel.
    /// </summary>
    public async Task<int> CreateSavePointAsync(ulong channelId, ulong userId, string? reason = null)
    {
        var channel = await GetCountingChannelAsync(channelId);
        if (channel == null) return -1;

        await using var db = await dbFactory.CreateConnectionAsync();

        var save = new CountingSaves
        {
            ChannelId = channelId,
            SavedNumber = channel.CurrentNumber,
            SavedAt = DateTime.UtcNow,
            SavedBy = userId,
            Reason = reason ?? "Manual save",
            IsActive = true
        };

        var saveId = await db.InsertWithInt32IdentityAsync(save);

        // Log the save creation
        await LogEventAsync(channelId, CountingEventType.SaveCreated, userId, null, channel.CurrentNumber, 0, reason);

        return saveId;
    }

    /// <summary>
    /// Restores a counting channel from a save point.
    /// </summary>
    public async Task<bool> RestoreFromSaveAsync(ulong channelId, int saveId, ulong restoredBy)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var save = await db.CountingSaves
            .FirstOrDefaultAsync(x => x.Id == saveId && x.ChannelId == channelId && x.IsActive);

        if (save == null) return false;

        var channel = await GetCountingChannelAsync(channelId);
        if (channel == null) return false;

        var oldNumber = channel.CurrentNumber;

        // Restore the channel to the saved state
        await db.CountingChannels
            .Where(x => x.ChannelId == channelId)
            .UpdateAsync(x => new CountingChannel
            {
                CurrentNumber = save.SavedNumber, LastUserId = 0, LastMessageId = 0
            });

        // Clear cache
        cache.Remove(string.Format(COUNTING_CHANNEL_CACHE_KEY, channelId));

        // Log the restoration
        await LogEventAsync(channelId, CountingEventType.SaveRestored, restoredBy, oldNumber, save.SavedNumber, 0,
            $"Restored from save #{saveId}");

        return true;
    }

    /// <summary>
    /// Handles when a wrong number is submitted.
    /// </summary>
    private async Task HandleWrongNumberAsync(
        CountingChannel channel,
        CountingChannelConfig config,
        ulong userId,
        ulong messageId,
        long actualNumber,
        long expectedNumber)
    {
        // Log the error
        await LogEventAsync(channel.ChannelId, CountingEventType.WrongNumber, userId,
            expectedNumber, actualNumber, messageId);

        // Update user error count
        await statsService.IncrementUserErrorsAsync(channel.ChannelId, userId);

        // Handle reset on error
        if (config.ResetOnError)
        {
            await ResetCountingChannelAsync(channel.ChannelId, channel.StartNumber - channel.Increment, 0,
                "Automatic reset due to error");
        }

        // Apply moderation if needed
        await moderationService.HandleCountingErrorAsync(channel.ChannelId, userId, actualNumber, expectedNumber);
    }

    /// <summary>
    /// Handles when the maximum number is reached.
    /// </summary>
    private async Task HandleMaxNumberReachedAsync(CountingChannel channel, CountingChannelConfig config, ulong userId)
    {
        await LogEventAsync(channel.ChannelId, CountingEventType.MaxNumberReached, userId,
            channel.CurrentNumber, config.MaxNumber, 0);

        // Could trigger special rewards or reset here
        await moderationService.HandleMaxNumberReachedAsync(channel.ChannelId, userId, config.MaxNumber);
    }

    /// <summary>
    /// Updates the counting channel with a new valid count.
    /// </summary>
    private async Task UpdateCountingChannelAsync(CountingChannel channel, long newNumber, ulong userId,
        ulong messageId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var isNewRecord = newNumber > channel.HighestNumber;

        await db.CountingChannels
            .Where(x => x.ChannelId == channel.ChannelId)
            .UpdateAsync(x => new CountingChannel
            {
                CurrentNumber = newNumber,
                LastUserId = userId,
                LastMessageId = messageId,
                HighestNumber = isNewRecord ? newNumber : x.HighestNumber,
                HighestNumberReachedAt = isNewRecord ? DateTime.UtcNow : x.HighestNumberReachedAt,
                TotalCounts = x.TotalCounts + 1
            });

        // Clear cache
        cache.Remove(string.Format(COUNTING_CHANNEL_CACHE_KEY, channel.ChannelId));
    }

    /// <summary>
    /// Parses a number from text based on the specified pattern.
    /// </summary>
    private async Task<NumberParseResult> ParseNumberAsync(string text, CountingPattern pattern, int numberBase)
    {
        try
        {
            return pattern switch
            {
                CountingPattern.Normal => ParseNormalNumber(text, numberBase),
                CountingPattern.Roman => ParseRomanNumber(text),
                CountingPattern.Binary => ParseBinaryNumber(text),
                CountingPattern.Hexadecimal => ParseHexNumber(text),
                CountingPattern.Words => await ParseWordsNumber(text),
                CountingPattern.Fibonacci => ParseFibonacci(text),
                CountingPattern.Primes => ParsePrimes(text),
                _ => new NumberParseResult
                {
                    Success = false
                }
            };
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error parsing number: {Text} with pattern {Pattern}", text, pattern);
            return new NumberParseResult
            {
                Success = false
            };
        }
    }

    /// <summary>
    /// Parses a normal decimal number.
    /// </summary>
    private NumberParseResult ParseNormalNumber(string text, int numberBase)
    {
        if (!PatternRegexes[CountingPattern.Normal].IsMatch(text))
            return new NumberParseResult
            {
                Success = false
            };

        var cleanText = text.Trim();

        if (numberBase == 10)
        {
            if (long.TryParse(cleanText, out var number))
                return new NumberParseResult
                {
                    Success = true, Number = number
                };
        }
        else
        {
            try
            {
                var number = Convert.ToInt64(cleanText, numberBase);
                return new NumberParseResult
                {
                    Success = true, Number = number
                };
            }
            catch
            {
                // Fall through to failure case
            }
        }

        return new NumberParseResult
        {
            Success = false
        };
    }

    /// <summary>
    /// Parses a Roman numeral.
    /// </summary>
    private NumberParseResult ParseRomanNumber(string text)
    {
        if (!PatternRegexes[CountingPattern.Roman].IsMatch(text))
            return new NumberParseResult
            {
                Success = false
            };

        try
        {
            var number = CountingPatternHelper.RomanToDecimal(text.Trim().ToUpper());
            return new NumberParseResult
            {
                Success = true, Number = number
            };
        }
        catch
        {
            return new NumberParseResult
            {
                Success = false
            };
        }
    }

    /// <summary>
    /// Parses a binary number.
    /// </summary>
    private NumberParseResult ParseBinaryNumber(string text)
    {
        if (!PatternRegexes[CountingPattern.Binary].IsMatch(text))
            return new NumberParseResult
            {
                Success = false
            };

        try
        {
            var number = Convert.ToInt64(text.Trim(), 2);
            return new NumberParseResult
            {
                Success = true, Number = number
            };
        }
        catch
        {
            return new NumberParseResult
            {
                Success = false
            };
        }
    }

    /// <summary>
    /// Parses a hexadecimal number.
    /// </summary>
    private NumberParseResult ParseHexNumber(string text)
    {
        if (!PatternRegexes[CountingPattern.Hexadecimal].IsMatch(text))
            return new NumberParseResult
            {
                Success = false
            };

        try
        {
            var number = Convert.ToInt64(text.Trim(), 16);
            return new NumberParseResult
            {
                Success = true, Number = number
            };
        }
        catch
        {
            return new NumberParseResult
            {
                Success = false
            };
        }
    }

    /// <summary>
    /// Parses a number written in words.
    /// </summary>
    private async Task<NumberParseResult> ParseWordsNumber(string text)
    {
        // This would be a complex implementation for parsing numbers written as words
        // For now, return a basic implementation
        try
        {
            var number = CountingPatternHelper.WordsToNumber(text.Trim());
            return new NumberParseResult
            {
                Success = number > 0, Number = number
            };
        }
        catch
        {
            return new NumberParseResult
            {
                Success = false
            };
        }
    }

    /// <summary>
    /// Parses a Fibonacci sequence number.
    /// </summary>
    private NumberParseResult ParseFibonacci(string text)
    {
        if (!PatternRegexes[CountingPattern.Normal].IsMatch(text))
            return new NumberParseResult
            {
                Success = false
            };

        if (long.TryParse(text.Trim(), out var number))
        {
            if (CountingPatternHelper.IsFibonacci(number))
                return new NumberParseResult
                {
                    Success = true, Number = number
                };
        }

        return new NumberParseResult
        {
            Success = false
        };
    }

    /// <summary>
    /// Parses a prime number.
    /// </summary>
    private NumberParseResult ParsePrimes(string text)
    {
        if (!PatternRegexes[CountingPattern.Normal].IsMatch(text))
            return new NumberParseResult
            {
                Success = false
            };

        if (long.TryParse(text.Trim(), out var number))
        {
            if (CountingPatternHelper.IsPrime(number))
                return new NumberParseResult
                {
                    Success = true, Number = number
                };
        }

        return new NumberParseResult
        {
            Success = false
        };
    }

    /// <summary>
    /// Checks if a user is currently on cooldown.
    /// </summary>
    private async Task<bool> IsUserOnCooldownAsync(ulong channelId, ulong userId, int cooldownSeconds)
    {
        if (cooldownSeconds <= 0) return false;

        var cacheKey = string.Format(USER_COOLDOWN_CACHE_KEY, channelId, userId);
        return cache.TryGetValue(cacheKey, out _);
    }

    /// <summary>
    /// Sets a user on cooldown for the specified duration.
    /// </summary>
    private void SetUserCooldown(ulong channelId, ulong userId, int cooldownSeconds)
    {
        var cacheKey = string.Format(USER_COOLDOWN_CACHE_KEY, channelId, userId);
        cache.Set(cacheKey, true, TimeSpan.FromSeconds(cooldownSeconds));
    }

    /// <summary>
    /// Checks for milestones and handles rewards.
    /// </summary>
    private async Task CheckForMilestonesAsync(ulong channelId, long number, ulong userId)
    {
        var milestoneTypes = new[]
        {
            100, 500, 1000, 5000, 10000
        };

        foreach (var milestone in milestoneTypes)
        {
            if (number % milestone == 0)
            {
                await HandleMilestoneAsync(channelId, number, userId, milestone);
            }
        }
    }

    /// <summary>
    /// Handles milestone achievements.
    /// </summary>
    private async Task HandleMilestoneAsync(ulong channelId, long number, ulong userId, int milestoneType)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        // Check if milestone already exists
        var existing = await db.CountingMilestones
            .AnyAsync(x => x.ChannelId == channelId && x.Number == number);

        if (existing) return;

        // Create milestone record
        var milestone = new CountingMilestones
        {
            ChannelId = channelId,
            Number = number,
            ReachedAt = DateTime.UtcNow,
            UserId = userId,
            Type = milestoneType,
            RewardGiven = false
        };

        await db.InsertAsync(milestone);

        // Log the milestone
        await LogEventAsync(channelId, CountingEventType.MilestoneReached, userId, null, number, 0,
            $"Milestone: {milestoneType}");
    }

    /// <summary>
    /// Logs a counting event for audit trail.
    /// </summary>
    private async Task LogEventAsync(
        ulong channelId,
        CountingEventType eventType,
        ulong userId,
        long? oldNumber,
        long? newNumber,
        ulong? messageId,
        string? details = null)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();

            var eventRecord = new CountingEvents
            {
                ChannelId = channelId,
                EventType = (int)eventType,
                UserId = userId,
                OldNumber = oldNumber,
                NewNumber = newNumber,
                Timestamp = DateTime.UtcNow,
                MessageId = messageId,
                Details = details
            };

            await db.InsertAsync(eventRecord);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to log counting event for channel {ChannelId}", channelId);
        }
    }

    /// <summary>
    /// Handles message received events for counting processing.
    /// </summary>
    private async Task MessageReceived(SocketMessage msg)
    {
        if (msg.Author.IsBot || msg.Author is not IGuildUser user)
            return;

        if (msg is not IUserMessage userMsg)
            return;

        try
        {
            // Only process in counting channels
            var countingChannel = await GetCountingChannelAsync(msg.Channel.Id);
            if (countingChannel == null) return;

            // Check if user is banned from counting
            if (await moderationService.IsUserBannedAsync(msg.Channel.Id, msg.Author.Id))
                return;

            // Check if user should be ignored based on roles
            if (await moderationService.ShouldIgnoreUserAsync(msg.Channel.Id, user))
            {
                // Check if we should delete ignored messages
                if (await moderationService.ShouldDeleteIgnoredMessagesAsync(msg.Channel.Id, user))
                {
                    try
                    {
                        await userMsg.DeleteAsync();
                    }
                    catch
                    {
                        // Ignore deletion errors (missing permissions, message already deleted, etc.)
                    }
                }

                return;
            }

            // First check if bot can react and if user has bot blocked (for valid counts only)
            var parseResult = await ParseCountingMessageAsync(msg.Content, msg.Channel.Id);
            if (parseResult.IsValidCount)
            {
                // Check bot blocking before processing the count
                var botBlockResult = await CheckBotBlockingAsync(userMsg);
                if (botBlockResult.IsBlocked)
                {
                    // User has bot blocked, delete message and show warning
                    try
                    {
                        await userMsg.DeleteAsync();
                    }
                    catch
                    {
                        // Ignore deletion errors
                    }

                    await botBlockResult.SendWarningMessage();
                    return; // Don't process the count at all
                }
            }

            // Process the counting attempt
            var result = await ProcessCountingAttemptAsync(msg.Channel.Id, msg.Author.Id, msg.Id, msg.Content);

            // If it's not a valid number, handle as non-number message
            if (!result.Success && result.ErrorType == CountingError.InvalidNumber)
            {
                await moderationService.HandleNonNumberMessageAsync(msg.Channel.Id, msg.Author.Id, userMsg);
                return;
            }

            // Handle result (reactions, deletions, etc.) - bot blocking already checked for valid counts
            await HandleCountingResultAsync(userMsg, result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing counting message from {UserId} in channel {ChannelId}",
                msg.Author.Id, msg.Channel.Id);
        }
    }

    /// <summary>
    ///     Checks if the bot has permission to add reactions in the channel.
    /// </summary>
    private async Task<bool> CanBotReactInChannel(ITextChannel channel)
    {
        try
        {
            var botUser = await channel.Guild.GetUserAsync(client.CurrentUser.Id);
            var permissions = botUser?.GetPermissions(channel);
            return permissions?.AddReactions == true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    ///     Quickly parses a message to check if it contains a valid counting number.
    /// </summary>
    private async Task<CountingParseResult> ParseCountingMessageAsync(string content, ulong channelId)
    {
        try
        {
            var config = await GetCountingConfigAsync(channelId);
            if (config == null)
                return new CountingParseResult
                {
                    IsValidCount = false
                };

            var parseResult = await ParseNumberAsync(content, (CountingPattern)config.Pattern, config.NumberBase);
            return new CountingParseResult
            {
                IsValidCount = parseResult.Success, Number = parseResult.Number
            };
        }
        catch
        {
            return new CountingParseResult
            {
                IsValidCount = false
            };
        }
    }

    /// <summary>
    ///     Checks if user has bot blocked by attempting to react.
    /// </summary>
    private async Task<BotBlockResult> CheckBotBlockingAsync(IUserMessage message)
    {
        try
        {
            if (message.Channel is not ITextChannel textChannel)
                return new BotBlockResult
                {
                    IsBlocked = false
                };

            // Check if bot has permission to react
            if (!await CanBotReactInChannel(textChannel))
                return new BotBlockResult
                {
                    IsBlocked = false
                };

            var config = await GetCountingConfigAsync(message.Channel.Id);
            if (config == null)
                return new BotBlockResult
                {
                    IsBlocked = false
                };

            // Determine which emote to use for testing
            IEmote testEmote;
            if (!string.IsNullOrEmpty(config.SuccessEmote))
            {
                if (Emote.TryParse(config.SuccessEmote, out var customEmote))
                    testEmote = customEmote;
                else if (Emoji.TryParse(config.SuccessEmote, out var customEmoji))
                    testEmote = customEmoji;
                else
                    testEmote = new Emoji("✅");
            }
            else
            {
                testEmote = new Emoji("✅");
            }

            // Try to react to check if user has bot blocked
            var isBlocked = await IsUserBlockingBotAsync(message, testEmote);

            return new BotBlockResult
            {
                IsBlocked = isBlocked,
                SendWarningMessage = async () =>
                {
                    try
                    {
                        var embed = new EmbedBuilder()
                            .WithErrorColor()
                            .WithDescription(strings.CountingUserBotBlocked(textChannel.GuildId))
                            .Build();

                        var warningMessage = await textChannel.SendMessageAsync(embed: embed);

                        // Delete warning message after 5 seconds
                        _ = Task.Run(async () =>
                        {
                            await Task.Delay(5000);
                            try
                            {
                                await warningMessage.DeleteAsync();
                            }
                            catch
                            {
                                // Ignore deletion errors
                            }
                        });
                    }
                    catch
                    {
                        // Ignore errors sending warning
                    }
                }
            };
        }
        catch
        {
            return new BotBlockResult
            {
                IsBlocked = false
            };
        }
    }

    /// <summary>
    ///     Checks if a user has the bot blocked by attempting to react to their message.
    ///     Only call this after confirming bot has reaction permissions.
    /// </summary>
    private async Task<bool> IsUserBlockingBotAsync(IUserMessage message, IEmote reactionEmote)
    {
        try
        {
            await message.AddReactionAsync(reactionEmote);
            return false; // Reaction succeeded, user hasn't blocked the bot
        }
        catch (Exception ex)
        {
            // If adding reaction fails, the user likely has the bot blocked
            logger.LogDebug(ex,
                "Failed to add reaction to message {MessageId} from user {UserId}, user likely has bot blocked",
                message.Id, message.Author.Id);
            return true;
        }
    }

    /// <summary>
    /// Handles the result of a counting attempt.
    /// </summary>
    private async Task HandleCountingResultAsync(IUserMessage message, CountingResult result)
    {
        try
        {
            var config = await GetCountingConfigAsync(message.Channel.Id);
            if (config == null) return;

            // Check if bot has permission to react
            if (message.Channel is not ITextChannel textChannel || !await CanBotReactInChannel(textChannel))
            {
                logger.LogWarning("Bot lacks permission to add reactions in channel {ChannelId}", message.Channel.Id);
                return;
            }

            // Determine which emote to use and add reaction
            if (result.Success)
            {
                if (!string.IsNullOrEmpty(config.SuccessEmote))
                {
                    if (Emote.TryParse(config.SuccessEmote, out var emote))
                        await message.AddReactionAsync(emote);
                    else if (Emoji.TryParse(config.SuccessEmote, out var emoji))
                        await message.AddReactionAsync(emoji);
                    else
                        await message.AddReactionAsync(new Emoji("✅"));
                }
                else
                {
                    await message.AddReactionAsync(new Emoji("✅"));
                }
            }
            else
            {
                if (!string.IsNullOrEmpty(config.ErrorEmote))
                {
                    if (Emote.TryParse(config.ErrorEmote, out var emote))
                        await message.AddReactionAsync(emote);
                    else if (Emoji.TryParse(config.ErrorEmote, out var emoji))
                        await message.AddReactionAsync(emoji);
                    else
                        await message.AddReactionAsync(new Emoji("❌"));
                }
                else
                {
                    await message.AddReactionAsync(new Emoji("❌"));
                }

                // Delete wrong messages if configured
                if (config.DeleteWrongMessages)
                {
                    await Task.Delay(2000); // Give time for users to see the error
                    await message.DeleteAsync();
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error handling counting result for message {MessageId}", message.Id);
        }
    }

    /// <summary>
    /// Handles message updated events for counting edit protection.
    /// </summary>
    private async Task MessageUpdated(Cacheable<IMessage, ulong> before, SocketMessage after,
        ISocketMessageChannel channel)
    {
        if (after.Author.IsBot || after.Author is not IGuildUser user)
            return;

        if (after is not IUserMessage userMsg)
            return;

        try
        {
            // Only process in counting channels
            var countingChannel = await GetCountingChannelAsync(channel.Id);
            if (countingChannel == null) return;

            // Check if user is banned from counting
            if (await moderationService.IsUserBannedAsync(channel.Id, after.Author.Id))
                return;

            // Check if user should be ignored based on roles
            if (await moderationService.ShouldIgnoreUserAsync(channel.Id, user))
                return;

            // Handle message edit - even if original message is not cached
            if (before.HasValue && before.Value is IUserMessage originalUserMsg)
            {
                await moderationService.HandleMessageEditAsync(channel.Id, after.Author.Id, originalUserMsg, userMsg);
            }
            else
            {
                // Original message not in cache, still handle edit protection
                await moderationService.HandleMessageEditAsync(channel.Id, after.Author.Id, null, userMsg);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing counting message edit from {UserId} in channel {ChannelId}",
                after.Author.Id, channel.Id);
        }
    }

    /// <summary>
    ///     Handles message deleted events for counting channels.
    /// </summary>
    private async Task MessageDeleted(Cacheable<IMessage, ulong> cachedMsg, Cacheable<IMessageChannel, ulong> channel)
    {
        try
        {
            var channelId = channel.Id;

            // Only process in counting channels
            var countingChannel = await GetCountingChannelAsync(channelId);
            if (countingChannel == null) return;

            // Check if this was the last counting message
            if (cachedMsg.HasValue && cachedMsg.Value.Id == countingChannel.LastMessageId)
            {
                // Use same cache key as moderation service to prevent duplicates
                var hintCacheKey = $"counting_last_hint_{channelId}";
                if (!cache.TryGetValue(hintCacheKey, out _))
                {
                    try
                    {
                        if (client.GetChannel(channelId) is ITextChannel textChannel)
                        {
                            var nextNumber = countingChannel.CurrentNumber + countingChannel.Increment;
                            var embed = new EmbedBuilder()
                                .WithErrorColor()
                                .WithDescription(strings.CountingLastNumberWas(textChannel.GuildId,
                                    countingChannel.CurrentNumber, nextNumber))
                                .Build();
                            await textChannel.SendMessageAsync(embed: embed);

                            // Cache to prevent duplicates for 5 seconds
                            cache.Set(hintCacheKey, true, TimeSpan.FromSeconds(5));
                        }
                    }
                    catch { }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing counting message deletion in channel {ChannelId}", channel.Id);
        }
    }

    /// <summary>
    /// Gets all counting channels for a guild.
    /// </summary>
    public async Task<List<CountingChannel>> GetGuildCountingChannelsAsync(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        return await db.CountingChannels
            .Where(x => x.GuildId == guildId && x.IsActive)
            .ToListAsync();
    }

    /// <summary>
    /// Gets counting statistics for a channel.
    /// </summary>
    public async Task<CountingChannelStats> GetChannelStatsAsync(ulong channelId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var channel = await GetCountingChannelAsync(channelId);
        if (channel == null) return null;

        var stats = new CountingChannelStats
        {
            Channel = channel,
            TotalParticipants = await db.CountingStats
                .CountAsync(x => x.ChannelId == channelId),
            TotalErrors = await db.CountingEvents
                .CountAsync(x => x.ChannelId == channelId && x.EventType == (int)CountingEventType.WrongNumber),
            MilestonesReached = await db.CountingMilestones
                .CountAsync(x => x.ChannelId == channelId),
            TopContributor = await GetTopContributorAsync(db, channelId)
        };

        return stats;
    }

    /// <summary>
    /// Gets the top contributor for a channel.
    /// </summary>
    private async Task<CountingStats?> GetTopContributorAsync(MewdekoDb db, ulong channelId)
    {
        return await db.CountingStats
            .Where(x => x.ChannelId == channelId)
            .OrderByDescending(x => x.ContributionsCount)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Disables a counting channel by setting it as inactive.
    /// </summary>
    public async Task<bool> DisableCountingChannelAsync(ulong channelId, ulong disabledBy, string? reason = null)
    {
        var channel = await GetCountingChannelAsync(channelId);
        if (channel == null) return false;

        await using var db = await dbFactory.CreateConnectionAsync();

        await db.CountingChannels
            .Where(x => x.ChannelId == channelId)
            .UpdateAsync(x => new CountingChannel
            {
                IsActive = false
            });

        // Clear cache
        cache.Remove(string.Format(COUNTING_CHANNEL_CACHE_KEY, channelId));

        // Log the deletion
        await LogEventAsync(channelId, CountingEventType.ChannelDeleted, disabledBy, channel.CurrentNumber, null, 0,
            reason);

        return true;
    }

    /// <summary>
    ///     Purges a counting channel and all associated data.
    /// </summary>
    public async Task<bool> PurgeCountingChannelAsync(ulong channelId, ulong purgedBy, string? reason = null)
    {
        var channel = await GetCountingChannelAsync(channelId);
        if (channel == null) return false;

        await using var db = await dbFactory.CreateConnectionAsync();

        // Delete all counting stats for this channel
        await db.CountingStats
            .Where(x => x.ChannelId == channelId)
            .DeleteAsync();

        // Delete all counting saves for this channel
        await db.CountingSaves
            .Where(x => x.ChannelId == channelId)
            .DeleteAsync();

        // Delete all counting events for this channel
        await db.CountingEvents
            .Where(x => x.ChannelId == channelId)
            .DeleteAsync();

        // Delete all counting user bans for this channel
        await moderationService.PurgeChannelBansAsync(channelId);

        // Delete counting configuration
        await db.CountingChannelConfigs
            .Where(x => x.ChannelId == channelId)
            .DeleteAsync();

        // Finally delete the channel itself
        await db.CountingChannels
            .Where(x => x.ChannelId == channelId)
            .DeleteAsync();

        // Clear all caches
        cache.Remove(string.Format(COUNTING_CHANNEL_CACHE_KEY, channelId));
        cache.Remove(string.Format(COUNTING_CONFIG_CACHE_KEY, channelId));

        // Clear stats cache
        await statsService.ClearChannelCacheAsync(channelId);

        // Log the purge (will fail since channel is deleted, but that's ok)
        try
        {
            await LogEventAsync(channelId, CountingEventType.ChannelDeleted, purgedBy, channel.CurrentNumber, null, 0,
                $"PURGED: {reason}");
        }
        catch
        {
            /* Ignore logging errors after purge */
        }

        return true;
    }

    /// <summary>
    /// Updates counting channel configuration.
    /// </summary>
    public async Task<bool> UpdateCountingConfigAsync(ulong channelId, CountingChannelConfig updatedConfig)
    {
        var existingConfig = await GetCountingConfigAsync(channelId);
        if (existingConfig == null) return false;

        await using var db = await dbFactory.CreateConnectionAsync();

        await db.CountingChannelConfigs
            .Where(x => x.ChannelId == channelId)
            .UpdateAsync(x => new CountingChannelConfig
            {
                AllowRepeatedUsers = updatedConfig.AllowRepeatedUsers,
                Cooldown = updatedConfig.Cooldown,
                RequiredRoles = updatedConfig.RequiredRoles,
                BannedRoles = updatedConfig.BannedRoles,
                MaxNumber = updatedConfig.MaxNumber,
                ResetOnError = updatedConfig.ResetOnError,
                DeleteWrongMessages = updatedConfig.DeleteWrongMessages,
                Pattern = updatedConfig.Pattern,
                NumberBase = updatedConfig.NumberBase,
                SuccessEmote = updatedConfig.SuccessEmote,
                ErrorEmote = updatedConfig.ErrorEmote,
                EnableAchievements = updatedConfig.EnableAchievements,
                EnableCompetitions = updatedConfig.EnableCompetitions
            });

        // Clear cache
        cache.Remove(string.Format(COUNTING_CONFIG_CACHE_KEY, channelId));

        // Log the configuration change
        await LogEventAsync(channelId, CountingEventType.ConfigChanged, 0, null, null, 0,
            "Configuration updated via API");

        return true;
    }

    /// <summary>
    ///     Result of parsing a counting message to check if it's a valid count.
    /// </summary>
    private class CountingParseResult
    {
        public bool IsValidCount { get; set; }
        public long Number { get; set; }
    }

    /// <summary>
    ///     Result of checking if user has bot blocked.
    /// </summary>
    private class BotBlockResult
    {
        public bool IsBlocked { get; set; }
        public Func<Task>? SendWarningMessage { get; set; }
    }

    #region Customization Methods

    /// <summary>
    ///     Sets a custom success message for a counting channel.
    /// </summary>
    public async Task<bool> SetSuccessMessageAsync(ulong channelId, string message)
    {
        var config = await GetCountingConfigAsync(channelId);
        if (config == null) return false;

        config.SuccessMessage = message;
        return await UpdateCountingConfigAsync(channelId, config);
    }

    /// <summary>
    ///     Gets the custom success message for a counting channel.
    /// </summary>
    public async Task<string?> GetSuccessMessageAsync(ulong channelId)
    {
        var config = await GetCountingConfigAsync(channelId);
        return config?.SuccessMessage;
    }

    /// <summary>
    ///     Resets the success message to default for a counting channel.
    /// </summary>
    public async Task<bool> ResetSuccessMessageAsync(ulong channelId)
    {
        var config = await GetCountingConfigAsync(channelId);
        if (config == null) return false;

        config.SuccessMessage = null;
        return await UpdateCountingConfigAsync(channelId, config);
    }

    /// <summary>
    ///     Sets a custom failure message for a counting channel.
    /// </summary>
    public async Task<bool> SetFailureMessageAsync(ulong channelId, string message)
    {
        var config = await GetCountingConfigAsync(channelId);
        if (config == null) return false;

        config.FailureMessage = message;
        return await UpdateCountingConfigAsync(channelId, config);
    }

    /// <summary>
    ///     Gets the custom failure message for a counting channel.
    /// </summary>
    public async Task<string?> GetFailureMessageAsync(ulong channelId)
    {
        var config = await GetCountingConfigAsync(channelId);
        return config?.FailureMessage;
    }

    /// <summary>
    ///     Resets the failure message to default for a counting channel.
    /// </summary>
    public async Task<bool> ResetFailureMessageAsync(ulong channelId)
    {
        var config = await GetCountingConfigAsync(channelId);
        if (config == null) return false;

        config.FailureMessage = null;
        return await UpdateCountingConfigAsync(channelId, config);
    }

    /// <summary>
    ///     Sets a custom milestone message for a counting channel.
    /// </summary>
    public async Task<bool> SetMilestoneMessageAsync(ulong channelId, string message)
    {
        var config = await GetCountingConfigAsync(channelId);
        if (config == null) return false;

        config.MilestoneMessage = message;
        return await UpdateCountingConfigAsync(channelId, config);
    }

    /// <summary>
    ///     Gets the custom milestone message for a counting channel.
    /// </summary>
    public async Task<string?> GetMilestoneMessageAsync(ulong channelId)
    {
        var config = await GetCountingConfigAsync(channelId);
        return config?.MilestoneMessage;
    }

    /// <summary>
    ///     Resets the milestone message to default for a counting channel.
    /// </summary>
    public async Task<bool> ResetMilestoneMessageAsync(ulong channelId)
    {
        var config = await GetCountingConfigAsync(channelId);
        if (config == null) return false;

        config.MilestoneMessage = null;
        return await UpdateCountingConfigAsync(channelId, config);
    }

    /// <summary>
    ///     Sets the milestone channel for a counting channel.
    /// </summary>
    public async Task<bool> SetMilestoneChannelAsync(ulong channelId, ulong? milestoneChannelId)
    {
        var config = await GetCountingConfigAsync(channelId);
        if (config == null) return false;

        config.MilestoneChannelId = milestoneChannelId;
        return await UpdateCountingConfigAsync(channelId, config);
    }

    /// <summary>
    ///     Sets the failure channel for a counting channel.
    /// </summary>
    public async Task<bool> SetFailureChannelAsync(ulong channelId, ulong? failureChannelId)
    {
        var config = await GetCountingConfigAsync(channelId);
        if (config == null) return false;

        config.FailureChannelId = failureChannelId;
        return await UpdateCountingConfigAsync(channelId, config);
    }

    /// <summary>
    ///     Sets custom milestones for a counting channel.
    /// </summary>
    public async Task<bool> SetMilestonesAsync(ulong channelId, List<long> milestones)
    {
        var config = await GetCountingConfigAsync(channelId);
        if (config == null) return false;

        config.Milestones = string.Join(",", milestones);
        return await UpdateCountingConfigAsync(channelId, config);
    }

    /// <summary>
    ///     Gets the custom milestones for a counting channel.
    /// </summary>
    public async Task<List<long>> GetMilestonesAsync(ulong channelId)
    {
        var config = await GetCountingConfigAsync(channelId);
        if (config?.Milestones == null)
            return
            [
                100,
                250,
                500,
                1000,
                2500,
                5000,
                10000
            ];

        return config.Milestones.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(long.Parse)
            .ToList();
    }

    /// <summary>
    ///     Resets milestones to default for a counting channel.
    /// </summary>
    public async Task<bool> ResetMilestonesAsync(ulong channelId)
    {
        var config = await GetCountingConfigAsync(channelId);
        if (config == null) return false;

        config.Milestones = null;
        return await UpdateCountingConfigAsync(channelId, config);
    }

    /// <summary>
    ///     Sets the failure threshold for a counting channel.
    /// </summary>
    public async Task<bool> SetFailureThresholdAsync(ulong channelId, int threshold)
    {
        var config = await GetCountingConfigAsync(channelId);
        if (config == null) return false;

        config.FailureThreshold = threshold;
        return await UpdateCountingConfigAsync(channelId, config);
    }

    /// <summary>
    ///     Gets the failure threshold for a counting channel.
    /// </summary>
    public async Task<int> GetFailureThresholdAsync(ulong channelId)
    {
        var config = await GetCountingConfigAsync(channelId);
        return config?.FailureThreshold ?? 3;
    }

    /// <summary>
    ///     Sets the cooldown for a counting channel.
    /// </summary>
    public async Task<bool> SetCooldownAsync(ulong channelId, int seconds)
    {
        var config = await GetCountingConfigAsync(channelId);
        if (config == null) return false;

        config.CooldownSeconds = seconds;
        return await UpdateCountingConfigAsync(channelId, config);
    }

    /// <summary>
    ///     Gets the cooldown for a counting channel.
    /// </summary>
    public async Task<int> GetCooldownAsync(ulong channelId)
    {
        var config = await GetCountingConfigAsync(channelId);
        return config?.CooldownSeconds ?? 0;
    }

    #endregion
}