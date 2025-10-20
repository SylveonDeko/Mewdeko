using System.Threading;
using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Modules.Games.Common.Kaladont;
using Mewdeko.Services.Strings;
using Microsoft.Extensions.Caching.Memory;

namespace Mewdeko.Modules.Games.Services;

/// <summary>
///     Service for managing persistent Kaladont channels.
/// </summary>
public class KaladontChannelService : INService, IReadyExecutor
{
    private const string CHANNEL_CACHE_KEY = "kaladont_channel_{0}";
    private readonly IMemoryCache cache;
    private readonly DiscordShardedClient client;

    private readonly IDataConnectionFactory dbFactory;
    private readonly EventHandler eventHandler;
    private readonly GamesService gamesService;
    private readonly ILogger<KaladontChannelService> logger;

    // Active persistent games by channel ID
    private readonly System.Collections.Concurrent.ConcurrentDictionary<ulong, PersistentKaladontGame> persistentGames =
        new();

    private readonly GeneratedBotStrings strings;

    /// <summary>
    ///     Initializes a new instance of the <see cref="KaladontChannelService" /> class.
    /// </summary>
    public KaladontChannelService(
        IDataConnectionFactory dbFactory,
        GamesService gamesService,
        ILogger<KaladontChannelService> logger,
        IMemoryCache cache,
        EventHandler eventHandler,
        DiscordShardedClient client,
        GeneratedBotStrings strings)
    {
        this.dbFactory = dbFactory;
        this.gamesService = gamesService;
        this.logger = logger;
        this.cache = cache;
        this.eventHandler = eventHandler;
        this.client = client;
        this.strings = strings;

        // Subscribe to message events
        this.eventHandler.Subscribe("MessageReceived", "KaladontChannelService", MessageReceived);
    }

    /// <summary>
    ///     Initializes the service when the bot is ready.
    /// </summary>
    public async Task OnReadyAsync()
    {
        logger.LogInformation("Kaladont Channel Service Ready - Loading persistent channels");

        await using var db = await dbFactory.CreateConnectionAsync();
        var channels = await db.GetTable<KaladontChannel>()
            .Where(x => x.IsActive)
            .ToListAsync();

        foreach (var channel in channels)
        {
            try
            {
                await StartPersistentGame(channel);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to start persistent Kaladont game in channel {ChannelId}",
                    channel.ChannelId);
            }
        }

        logger.LogInformation("Loaded {Count} persistent Kaladont channels", persistentGames.Count);
    }

    /// <summary>
    ///     Sets up a new persistent Kaladont channel. If already set up, replaces the configuration.
    /// </summary>
    public async Task<(bool Success, string StartingWord)> SetupChannel(ulong guildId, ulong channelId, string language,
        int mode, int turnTime)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        // Check if channel already exists - if so, delete it first
        var existing = await db.GetTable<KaladontChannel>()
            .FirstOrDefaultAsync(x => x.ChannelId == channelId);

        if (existing != null)
        {
            // Stop the existing game
            if (persistentGames.TryRemove(channelId, out var oldGame))
            {
                oldGame.Dispose();
            }

            await db.DeleteAsync(existing);

            cache.Remove(string.Format(CHANNEL_CACHE_KEY, channelId));
        }

        var dictionary = gamesService.GetKaladontDictionary(language);
        if (dictionary.Count == 0)
            return (false, string.Empty);

        var startingWord = gamesService.GetRandomKaladontStartingWord(language);

        var channel = new KaladontChannel
        {
            GuildId = guildId,
            ChannelId = channelId,
            Language = language,
            Mode = mode,
            TurnTime = turnTime,
            CurrentWord = startingWord,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            TotalWords = 0
        };

        await db.InsertAsync(channel);
        cache.Remove(string.Format(CHANNEL_CACHE_KEY, channelId));

        // Start the persistent game
        await StartPersistentGame(channel);

        return (true, startingWord);
    }

    /// <summary>
    ///     Disables a persistent Kaladont channel.
    /// </summary>
    public async Task<bool> DisableChannel(ulong channelId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        // Check if channel exists and is active
        var channel = await db.GetTable<KaladontChannel>()
            .FirstOrDefaultAsync(x => x.ChannelId == channelId && x.IsActive);

        if (channel == null)
            return false;

        var updated = await db.GetTable<KaladontChannel>()
            .Where(x => x.ChannelId == channelId)
            .UpdateAsync(x => new KaladontChannel
            {
                IsActive = false
            });

        if (updated > 0)
        {
            cache.Remove(string.Format(CHANNEL_CACHE_KEY, channelId));

            // Stop the persistent game
            if (persistentGames.TryRemove(channelId, out var game))
            {
                game.Dispose();
            }

            return true;
        }

        return false;
    }

    /// <summary>
    ///     Gets a kaladont channel configuration.
    /// </summary>
    public async Task<KaladontChannel?> GetChannel(ulong channelId)
    {
        var cacheKey = string.Format(CHANNEL_CACHE_KEY, channelId);

        if (cache.TryGetValue(cacheKey, out KaladontChannel? cached))
            return cached;

        await using var db = await dbFactory.CreateConnectionAsync();
        var channel = await db.GetTable<KaladontChannel>()
            .FirstOrDefaultAsync(x => x.ChannelId == channelId && x.IsActive);

        if (channel != null)
            cache.Set(cacheKey, channel, TimeSpan.FromMinutes(10));

        return channel;
    }

    private async Task StartPersistentGame(KaladontChannel channelConfig)
    {
        var dictionary = gamesService.GetKaladontDictionary(channelConfig.Language);
        if (dictionary.Count == 0)
        {
            logger.LogWarning("Cannot start persistent Kaladont in channel {ChannelId} - dictionary not loaded",
                channelConfig.ChannelId);
            return;
        }

        var game = new PersistentKaladontGame(
            channelConfig,
            dictionary,
            async () => await OnGameEnded(channelConfig.ChannelId));

        persistentGames[channelConfig.ChannelId] = game;
    }

    private async Task OnGameEnded(ulong channelId)
    {
        // Get fresh channel config
        var channelConfig = await GetChannel(channelId);
        if (channelConfig == null || !channelConfig.IsActive)
        {
            persistentGames.TryRemove(channelId, out _);
            return;
        }

        // Restart the game with a new word
        var newWord = gamesService.GetRandomKaladontStartingWord(channelConfig.Language);
        await using var db = await dbFactory.CreateConnectionAsync();

        await db.GetTable<KaladontChannel>()
            .Where(x => x.ChannelId == channelId)
            .UpdateAsync(x => new KaladontChannel
            {
                CurrentWord = newWord
            });

        cache.Remove(string.Format(CHANNEL_CACHE_KEY, channelId));

        // Restart the persistent game
        if (persistentGames.TryRemove(channelId, out var oldGame))
        {
            oldGame.Dispose();
        }

        channelConfig.CurrentWord = newWord;
        await StartPersistentGame(channelConfig);

        // Notify channel
        try
        {
            if (client.GetChannel(channelId) is ITextChannel textChannel)
            {
                var embed = new EmbedBuilder()
                    .WithOkColor()
                    .WithTitle($"🔄 {strings.KaladontNewRound(textChannel.GuildId)}")
                    .WithDescription(strings.KaladontStartingWord(textChannel.GuildId, newWord))
                    .Build();

                await textChannel.SendMessageAsync(embed: embed);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send restart message in channel {ChannelId}", channelId);
        }
    }

    private async Task MessageReceived(SocketMessage msg)
    {
        if (msg.Author.IsBot || msg.Channel is not ITextChannel textChannel)
            return;

        // Check if this is a persistent Kaladont channel
        if (!persistentGames.TryGetValue(msg.Channel.Id, out var game))
            return;

        var content = msg.Content?.Trim();
        if (string.IsNullOrEmpty(content))
            return;

        try
        {
            // Handle "kaladont" to give up
            if (content.Equals("kaladont", StringComparison.OrdinalIgnoreCase))
            {
                var gaveUp = await game.SayKaladont(msg.Author.Id);
                if (gaveUp)
                {
                    try
                    {
                        await msg.DeleteAsync();
                    }
                    catch
                    {
                        // Ignore
                    }

                    await UpdateStats(msg.Channel.Id, msg.Author.Id, false, true);
                }

                return;
            }

            // Get channel config to check current word
            var channelConfig = await GetChannel(msg.Channel.Id);
            if (channelConfig == null)
                return;

            var currentWord = channelConfig.CurrentWord;
            var isSerbianLanguage = channelConfig.Language.ToLowerInvariant() == "sr";

            // Get last 2 letters of current word (digraph-aware for Serbian)
            var lastTwo = isSerbianLanguage
                ? SerbianDigraphHelper.GetLastTwoLetters(currentWord)
                : currentWord.Length >= 2
                    ? currentWord[^2..]
                    : "";

            // Quick pre-filter: Only process if message could be a valid game move
            // - Single word (no spaces)
            // - At least 3 characters or 3 letters (for Serbian)
            // - Starts with the required 2 letters
            if (content.Contains(' '))
            {
                // Silently ignore - this is chat
                return;
            }

            if (isSerbianLanguage)
            {
                // For Serbian, check letter count and first 2 letters
                if (SerbianDigraphHelper.GetLetterCount(content) < 3)
                    return;

                var firstTwo = SerbianDigraphHelper.GetFirstTwoLetters(content);
                if (!firstTwo.Equals(lastTwo, StringComparison.OrdinalIgnoreCase))
                    return;
            }
            else
            {
                // For other languages, use character-based checking
                if (content.Length < 3)
                    return;

                if (!content.StartsWith(lastTwo, StringComparison.OrdinalIgnoreCase))
                    return;
            }

            // This looks like a game attempt - validate it
            var result = await game.PlayWord(msg.Author.Id, msg.Author.ToString(), content);

            if (result.Success)
            {
                // Update database
                await UpdateWord(msg.Channel.Id, content);
                await UpdateStats(msg.Channel.Id, msg.Author.Id, true, false);

                // Add reaction
                try
                {
                    await msg.AddReactionAsync(new Emoji("✅"));
                }
                catch
                {
                    // Ignore
                }
            }
            else if (result.ValidationResult != KaladontGame.ValidationResult.Valid)
            {
                // Show error for failed game attempts
                var guildId = textChannel.GuildId;
                var errorMsg = GetErrorMessage(result.ValidationResult, content, currentWord, guildId);
                if (!string.IsNullOrEmpty(errorMsg))
                {
                    try
                    {
                        await msg.AddReactionAsync(new Emoji("❌"));
                        var reply = await textChannel.SendMessageAsync(errorMsg);

                        // Delete error after 5 seconds
                        _ = Task.Run(async () =>
                        {
                            await Task.Delay(5000);
                            try
                            {
                                await reply.DeleteAsync();
                                await msg.DeleteAsync();
                            }
                            catch
                            {
                                // Ignore
                            }
                        });
                    }
                    catch
                    {
                        // Ignore
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing Kaladont message in channel {ChannelId}", msg.Channel.Id);
        }
    }

    private async Task UpdateWord(ulong channelId, string word)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        await db.GetTable<KaladontChannel>()
            .Where(x => x.ChannelId == channelId)
            .UpdateAsync(x => new KaladontChannel
            {
                CurrentWord = word.Trim().ToLowerInvariant(), TotalWords = x.TotalWords + 1
            });

        cache.Remove(string.Format(CHANNEL_CACHE_KEY, channelId));
    }

    private async Task UpdateStats(ulong channelId, ulong userId, bool wordPlayed, bool eliminated)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var stats = await db.GetTable<KaladontStats>()
            .FirstOrDefaultAsync(x => x.ChannelId == channelId && x.UserId == userId);

        if (stats == null)
        {
            stats = new KaladontStats
            {
                ChannelId = channelId,
                UserId = userId,
                WordsCount = wordPlayed ? 1 : 0,
                Eliminations = eliminated ? 1 : 0,
                LastPlayed = DateTime.UtcNow
            };

            await db.InsertAsync(stats);
        }
        else
        {
            await db.GetTable<KaladontStats>()
                .Where(x => x.Id == stats.Id)
                .UpdateAsync(x => new KaladontStats
                {
                    WordsCount = wordPlayed ? x.WordsCount + 1 : x.WordsCount,
                    Eliminations = eliminated ? x.Eliminations + 1 : x.Eliminations,
                    LastPlayed = DateTime.UtcNow
                });
        }
    }

    private string GetErrorMessage(KaladontGame.ValidationResult result, string word, string currentWord, ulong guildId)
    {
        var lastTwo = currentWord.Length >= 2 ? currentWord[^2..].ToUpperInvariant() : "??";

        return result switch
        {
            KaladontGame.ValidationResult.TooShort => strings.KaladontInvalidLength(guildId),
            KaladontGame.ValidationResult.AlreadyUsed => strings.KaladontAlreadyUsed(guildId, Format.Bold(word)),
            KaladontGame.ValidationResult.WrongLetters =>
                $"{strings.KaladontWrongLetters(guildId, Format.Bold(lastTwo))}\nCurrent: **{currentWord.ToUpperInvariant()}**",
            KaladontGame.ValidationResult.KaladontLoop => strings.KaladontLoopDetected(guildId, Format.Bold(word)),
            KaladontGame.ValidationResult.NotInDictionary => strings.KaladontNotFound(guildId, Format.Bold(word)),
            KaladontGame.ValidationResult.DeadEnd => strings.KaladontDeadEnd(guildId, Format.Bold(word),
                Format.Bold(word.Length >= 2 ? word[^2..].ToUpperInvariant() : "")),
            _ => string.Empty
        };
    }

    /// <summary>
    ///     Represents a persistent Kaladont game that runs continuously in a channel.
    /// </summary>
    private class PersistentKaladontGame : IDisposable
    {
        private readonly HashSet<ulong> currentPlayers = [];
        private readonly HashSet<string> dictionary;
        private readonly string language;
        private readonly SemaphoreSlim locker = new(1, 1);
        private readonly int mode;
        private readonly Func<Task> onGameEnded;
        private readonly HashSet<string> usedWords = new(StringComparer.OrdinalIgnoreCase);
        private string currentWord;

        public PersistentKaladontGame(KaladontChannel config, HashSet<string> dictionary, Func<Task> onGameEnded)
        {
            this.dictionary = dictionary;
            this.onGameEnded = onGameEnded;
            mode = config.Mode;
            language = config.Language;
            currentWord = config.CurrentWord;
            usedWords.Add(currentWord);
        }

        public void Dispose()
        {
            locker.Dispose();
            usedWords.Clear();
            currentPlayers.Clear();
        }

        public async Task<bool> SayKaladont(ulong userId)
        {
            await locker.WaitAsync();
            try
            {
                if (!currentPlayers.Contains(userId))
                    return false;

                currentPlayers.Remove(userId);

                // If no players left, restart
                if (currentPlayers.Count == 0)
                {
                    await onGameEnded();
                }

                return true;
            }
            finally
            {
                locker.Release();
            }
        }

        public async Task<(bool Success, KaladontGame.ValidationResult ValidationResult)> PlayWord(ulong userId,
            string userName, string word)
        {
            await locker.WaitAsync();
            try
            {
                var validation = ValidateWord(word);
                if (validation != KaladontGame.ValidationResult.Valid)
                {
                    // Player is NOT eliminated in persistent mode - just rejected
                    return (false, validation);
                }

                // Word is valid
                var normalized = word.Trim().ToLowerInvariant();
                usedWords.Add(normalized);
                currentWord = normalized;

                // Add player if not already in
                currentPlayers.Add(userId);

                return (true, KaladontGame.ValidationResult.Valid);
            }
            finally
            {
                locker.Release();
            }
        }

        public string GetCurrentWord()
        {
            return currentWord;
        }

        private KaladontGame.ValidationResult ValidateWord(string word)
        {
            var normalized = word.Trim().ToLowerInvariant();
            var isSerbianLanguage = language.ToLowerInvariant() == "sr";

            // Check minimum length
            if (isSerbianLanguage)
            {
                var letterCount = SerbianDigraphHelper.GetLetterCount(normalized);
                if (letterCount < 3)
                    return KaladontGame.ValidationResult.TooShort;
            }
            else
            {
                if (normalized.Length < 3)
                    return KaladontGame.ValidationResult.TooShort;
            }

            if (usedWords.Contains(normalized))
                return KaladontGame.ValidationResult.AlreadyUsed;

            // Get last 2 letters of current word and first 2 of new word
            string lastTwo, firstTwo;
            if (isSerbianLanguage)
            {
                lastTwo = SerbianDigraphHelper.GetLastTwoLetters(currentWord);
                firstTwo = SerbianDigraphHelper.GetFirstTwoLetters(normalized);
            }
            else
            {
                lastTwo = currentWord[^2..];
                firstTwo = normalized[..2];
            }

            if (!lastTwo.Equals(firstTwo, StringComparison.OrdinalIgnoreCase))
                return KaladontGame.ValidationResult.WrongLetters;

            // Check for kaladont loop
            string endTwo;
            if (isSerbianLanguage)
            {
                endTwo = SerbianDigraphHelper.GetLastTwoLetters(normalized);
            }
            else
            {
                endTwo = normalized[^2..];
            }

            if (firstTwo.Equals(endTwo, StringComparison.OrdinalIgnoreCase))
                return KaladontGame.ValidationResult.KaladontLoop;

            if (!dictionary.Contains(normalized))
                return KaladontGame.ValidationResult.NotInDictionary;

            // In endless mode, check for dead-end
            if (mode == 1) // Endless
            {
                var nextLastTwo = isSerbianLanguage
                    ? SerbianDigraphHelper.GetLastTwoLetters(normalized)
                    : normalized[^2..];

                var hasValidContinuation = dictionary.Any(dictWord =>
                {
                    if (usedWords.Contains(dictWord))
                        return false;

                    var dictFirstTwo = isSerbianLanguage
                        ? SerbianDigraphHelper.GetFirstTwoLetters(dictWord)
                        : dictWord.Length >= 2
                            ? dictWord[..2]
                            : "";

                    if (!dictFirstTwo.Equals(nextLastTwo, StringComparison.OrdinalIgnoreCase))
                        return false;

                    var dictEndTwo = isSerbianLanguage
                        ? SerbianDigraphHelper.GetLastTwoLetters(dictWord)
                        : dictWord.Length >= 2
                            ? dictWord[^2..]
                            : "";

                    return !dictFirstTwo.Equals(dictEndTwo, StringComparison.OrdinalIgnoreCase);
                });

                if (!hasValidContinuation)
                    return KaladontGame.ValidationResult.DeadEnd;
            }

            return KaladontGame.ValidationResult.Valid;
        }
    }
}