using System.IO;
using System.Text.Json;
using Mewdeko.Modules.Games.Common;
using Mewdeko.Modules.Games.Common.Acrophobia;
using Mewdeko.Modules.Games.Common.Hangman;
using Mewdeko.Modules.Games.Common.Kaladont;
using Mewdeko.Modules.Games.Common.Nunchi;
using Mewdeko.Modules.Games.Common.Trivia;
using SerbianDigraphHelper = Mewdeko.Modules.Games.Common.Kaladont.SerbianDigraphHelper;

namespace Mewdeko.Modules.Games.Services;

/// <summary>
///     Service for managing various games and game-related operations.
/// </summary>
public class GamesService : INService, IUnloadableService
{
    private const string TypingArticlesPath = "data/typing_articles3.json";
    private const string KaladontEnglishDictPath = "data/kaladont/words_en.txt";
    private const string KaladontSerbianDictPath = "data/kaladont/words_sr.txt";

    private readonly GamesConfigService gamesConfig;
    private readonly ILogger<GamesService> logger;
    private readonly Random rng;


    /// <summary>
    ///     Initializes a new instance of the <see cref="GamesService" /> class.
    /// </summary>
    /// <param name="gamesConfig">The configuration service for games.</param>
    /// <param name="logger">The logger instance for structured logging.</param>
    public GamesService(GamesConfigService gamesConfig, ILogger<GamesService> logger)
    {
        this.gamesConfig = gamesConfig;
        this.logger = logger;

        rng = new MewdekoRandom();

        // Attempt to load typing articles from JSON file
        try
        {
            TypingArticles =
                JsonSerializer.Deserialize<List<TypingArticle>>(File.ReadAllText(TypingArticlesPath));
        }
        catch (Exception ex)
        {
            // Log a warning if loading typing articles fails
            logger.LogWarning("Error while loading typing articles {0}", ex.ToString());
            TypingArticles = [];
        }

        // Attempt to load Kaladont dictionaries
        try
        {
            KaladontEnglishDictionary = LoadDictionary(KaladontEnglishDictPath);
            logger.LogInformation("Loaded {Count} English words for Kaladont", KaladontEnglishDictionary.Count);
        }
        catch (Exception ex)
        {
            logger.LogWarning("Error while loading Kaladont English dictionary: {Error}", ex.Message);
            KaladontEnglishDictionary = [];
        }

        try
        {
            KaladontSerbianDictionary = LoadDictionary(KaladontSerbianDictPath);
            logger.LogInformation("Loaded {Count} Serbian words for Kaladont", KaladontSerbianDictionary.Count);
        }
        catch (Exception ex)
        {
            logger.LogWarning("Error while loading Kaladont Serbian dictionary: {Error}", ex.Message);
            KaladontSerbianDictionary = [];
        }

        EnglishPrefixLookup = BuildPrefixLookup(KaladontEnglishDictionary, false);
        SerbianPrefixLookup = BuildPrefixLookup(KaladontSerbianDictionary, true);
        logger.LogInformation("Built Kaladont prefix lookups for fast continuation checks");
    }

    /// <summary>
    ///     Gets the responses used by the EightBall game.
    /// </summary>
    public IReadOnlyList<string> EightBallResponses
    {
        get
        {
            return gamesConfig.Data.EightBallResponses;
        }
    }

    /// <summary>
    ///     Gets the list of typing articles.
    /// </summary>
    public List<TypingArticle> TypingArticles { get; }

    //channelId, game
    /// <summary>
    ///     Represents a collection of Acrophobia games.
    /// </summary>
    public ConcurrentDictionary<ulong, AcrophobiaGame> AcrophobiaGames { get; } = new();

    /// <summary>
    ///     Represents a collection of Hangman games.
    /// </summary>
    public ConcurrentDictionary<ulong, Hangman> HangmanGames { get; } = new();

    /// <summary>
    ///     Represents the term pool for Hangman games.
    /// </summary>
    public TermPool TermPool { get; } = new();

    /// <summary>
    ///     Represents a collection of running Trivia games.
    /// </summary>
    public ConcurrentDictionary<ulong, TriviaGame> RunningTrivias { get; } = new();

    /// <summary>
    ///     Represents a collection of TicTacToe games.
    /// </summary>
    public Dictionary<ulong, TicTacToe> TicTacToeGames { get; } = new();

    /// <summary>
    ///     Represents a collection of running Typing contests.
    /// </summary>
    public ConcurrentDictionary<ulong, TypingGame> RunningContests { get; } = new();

    /// <summary>
    ///     Represents a collection of Nunchi games.
    /// </summary>
    public ConcurrentDictionary<ulong, NunchiGame> NunchiGames { get; } = new();

    /// <summary>
    ///     Represents a collection of Kaladont games.
    /// </summary>
    public ConcurrentDictionary<ulong, KaladontGame> KaladontGames { get; } = new();

    /// <summary>
    ///     Gets the English dictionary for Kaladont games.
    /// </summary>
    public HashSet<string> KaladontEnglishDictionary { get; }

    /// <summary>
    ///     Gets the Serbian dictionary for Kaladont games.
    /// </summary>
    public HashSet<string> KaladontSerbianDictionary { get; }

    /// <summary>
    ///     Pre-computed lookup for English words by their first two letters.
    /// </summary>
    private Dictionary<string, HashSet<string>> EnglishPrefixLookup { get; }

    /// <summary>
    ///     Pre-computed lookup for Serbian words by their first two letters.
    /// </summary>
    private Dictionary<string, HashSet<string>> SerbianPrefixLookup { get; }

    /// <summary>
    ///     Unloads all active games and clears game-related data.
    /// </summary>
    public async Task Unload()
    {
        // Dispose and clear Acrophobia games
        AcrophobiaGames.ForEach(x => x.Value.Dispose());
        AcrophobiaGames.Clear();

        // Dispose and clear Hangman games
        HangmanGames.ForEach(x => x.Value.Dispose());
        HangmanGames.Clear();

        // Stop all running trivia games
        await Task.WhenAll(RunningTrivias.Select(x => x.Value.StopGame())).ConfigureAwait(false);
        RunningTrivias.Clear();

        // Clear TicTacToe games
        TicTacToeGames.Clear();

        // Stop all running typing contests
        await Task.WhenAll(RunningContests.Select(x => x.Value.Stop())).ConfigureAwait(false);
        RunningContests.Clear();

        // Dispose and clear Nunchi games
        NunchiGames.ForEach(x => x.Value.Dispose());
        NunchiGames.Clear();

        // Dispose and clear Kaladont games
        KaladontGames.ForEach(x => x.Value.Dispose());
        KaladontGames.Clear();
    }

    /// <summary>
    ///     Adds a new typing article to the list.
    /// </summary>
    /// <param name="user">The user who added the article.</param>
    /// <param name="text">The text of the article.</param>
    public void AddTypingArticle(IUser user, string text)
    {
        TypingArticles.Add(new TypingArticle
        {
            Source = user.ToString(),
            Extra = $"Text added on {DateTime.UtcNow} by {user}.",
            Text = text.SanitizeMentions(true)
        });

        // Save the updated list to the JSON file
        File.WriteAllText(TypingArticlesPath, JsonSerializer.Serialize(TypingArticles));
    }

    /// <summary>
    ///     Retrieves a response from the EightBall game.
    /// </summary>
    /// <param name="_">The input string (not used).</param>
    /// <returns>A response from the EightBall game.</returns>
    public string GetEightballResponse(string _)
    {
        return EightBallResponses[rng.Next(0, EightBallResponses.Count)];
    }

    /// <summary>
    ///     Removes a typing article from the list.
    /// </summary>
    /// <param name="index">The index of the article to remove.</param>
    /// <returns>The removed typing article, or null if the index is out of range.</returns>
    public TypingArticle? RemoveTypingArticle(int index)
    {
        if (index < 0 || index >= TypingArticles.Count)
            return null;

        var removed = TypingArticles[index];
        TypingArticles.RemoveAt(index);

        // Save the updated list to the JSON file
        File.WriteAllText(TypingArticlesPath, JsonSerializer.Serialize(TypingArticles));
        return removed;
    }

    /// <summary>
    ///     Loads a dictionary from a file.
    /// </summary>
    /// <param name="path">The path to the dictionary file.</param>
    /// <returns>A HashSet containing all words from the dictionary.</returns>
    private static HashSet<string> LoadDictionary(string path)
    {
        if (!File.Exists(path))
            return [];

        var words = File.ReadAllLines(path)
            .Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith('#'))
            .Select(line => line.Trim().ToLowerInvariant())
            .Where(word => word.Length >= 3)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return words;
    }

    /// <summary>
    ///     Builds a prefix lookup dictionary for fast continuation checks.
    /// </summary>
    /// <param name="dictionary">The dictionary to build the lookup from.</param>
    /// <param name="isSerbianDict">Whether this is the Serbian dictionary.</param>
    /// <returns>A dictionary mapping first two letters to all words starting with those letters.</returns>
    private static Dictionary<string, HashSet<string>> BuildPrefixLookup(HashSet<string> dictionary, bool isSerbianDict)
    {
        var lookup = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var word in dictionary)
        {
            if (word.Length < 2)
                continue;

            var firstTwo = isSerbianDict
                ? SerbianDigraphHelper.GetFirstTwoLetters(word)
                : word[..2];

            if (!lookup.TryGetValue(firstTwo, out var wordSet))
            {
                wordSet = [];
                lookup[firstTwo] = wordSet;
            }

            wordSet.Add(word);
        }

        return lookup;
    }

    /// <summary>
    ///     Gets a random starting word for Kaladont from the specified language dictionary.
    /// </summary>
    /// <param name="language">The language code ("en" or "sr").</param>
    /// <returns>A random starting word, or a default word if dictionary is empty.</returns>
    public string GetRandomKaladontStartingWord(string language)
    {
        var dictionary = language.ToLowerInvariant() == "sr" ? KaladontSerbianDictionary : KaladontEnglishDictionary;

        if (dictionary.Count == 0)
            return language == "sr" ? "tabla" : "table";

        // Get words that are 4-8 characters long AND have valid continuations
        var goodStartingWords = dictionary
            .Where(w => w.Length is >= 4 and <= 8 && HasValidContinuation(w, dictionary))
            .ToList();

        if (goodStartingWords.Count == 0)
        {
            // Fallback: just find any word with valid continuations
            goodStartingWords = dictionary
                .Where(w => HasValidContinuation(w, dictionary))
                .ToList();
        }

        if (goodStartingWords.Count == 0)
            return dictionary.First();

        return goodStartingWords[rng.Next(goodStartingWords.Count)];
    }

    /// <summary>
    ///     Checks if a word has at least one valid continuation in the dictionary.
    /// </summary>
    /// <param name="word">The word to check.</param>
    /// <param name="dictionary">The dictionary to search.</param>
    /// <returns>True if there's at least one valid continuation; otherwise, false.</returns>
    private bool HasValidContinuation(string word, HashSet<string> dictionary)
    {
        if (word.Length < 2)
            return true;

        // Determine if this is Serbian dictionary
        var isSerbianDict = dictionary == KaladontSerbianDictionary;

        var lastTwo = isSerbianDict
            ? SerbianDigraphHelper.GetLastTwoLetters(word)
            : word[^2..];

        // Get the appropriate prefix lookup
        var prefixLookup = isSerbianDict ? SerbianPrefixLookup : EnglishPrefixLookup;

        // O(1) lookup instead of O(n) scan
        if (!prefixLookup.TryGetValue(lastTwo, out var candidateWords))
            return false;

        // Check if any candidate word is valid (not the same word, not a loop)
        foreach (var w in candidateWords)
        {
            if (w.Equals(word, StringComparison.OrdinalIgnoreCase))
                continue;

            var endTwo = isSerbianDict
                ? SerbianDigraphHelper.GetLastTwoLetters(w)
                : w.Length >= 2
                    ? w[^2..]
                    : "";

            // Valid if it doesn't loop back to itself (first two != last two)
            if (!lastTwo.Equals(endTwo, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>
    ///     Gets the dictionary for the specified language.
    /// </summary>
    /// <param name="language">The language code ("en" or "sr").</param>
    /// <returns>The dictionary HashSet for the specified language.</returns>
    public HashSet<string> GetKaladontDictionary(string language)
    {
        return language.ToLowerInvariant() == "sr" ? KaladontSerbianDictionary : KaladontEnglishDictionary;
    }
}