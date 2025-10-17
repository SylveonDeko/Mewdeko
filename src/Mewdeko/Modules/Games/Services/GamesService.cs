using System.IO;
using System.Text.Json;
using Mewdeko.Modules.Games.Common;
using Mewdeko.Modules.Games.Common.Acrophobia;
using Mewdeko.Modules.Games.Common.Hangman;
using Mewdeko.Modules.Games.Common.Kaladont;
using Mewdeko.Modules.Games.Common.Nunchi;
using Mewdeko.Modules.Games.Common.Trivia;

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
    ///     Gets a random starting word for Kaladont from the specified language dictionary.
    /// </summary>
    /// <param name="language">The language code ("en" or "sr").</param>
    /// <returns>A random starting word, or a default word if dictionary is empty.</returns>
    public string GetRandomKaladontStartingWord(string language)
    {
        var dictionary = language.ToLowerInvariant() == "sr" ? KaladontSerbianDictionary : KaladontEnglishDictionary;

        if (dictionary.Count == 0)
            return language == "sr" ? "tabla" : "table";

        // Get words that are 4-8 characters long for good starting words
        var goodStartingWords = dictionary
            .Where(w => w.Length is >= 4 and <= 8)
            .ToList();

        if (goodStartingWords.Count == 0)
            return dictionary.First();

        return goodStartingWords[rng.Next(goodStartingWords.Count)];
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