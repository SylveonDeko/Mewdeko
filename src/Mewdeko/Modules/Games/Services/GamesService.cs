using Mewdeko.Modules.Games.Common;
using Mewdeko.Modules.Games.Common.Acrophobia;
using Mewdeko.Modules.Games.Common.Hangman;
using Mewdeko.Modules.Games.Common.Nunchi;
using Mewdeko.Modules.Games.Common.Trivia;
using Newtonsoft.Json;
using Serilog;
using System.IO;
using System.Threading.Tasks;

namespace Mewdeko.Modules.Games.Services;

public class GamesService : INService, IUnloadableService
{
    private const string TYPING_ARTICLES_PATH = "data/typing_articles3.json";
    private readonly GamesConfigService _gamesConfig;
    private readonly Random _rng;

    public GamesService(GamesConfigService gamesConfig)
    {
        _gamesConfig = gamesConfig;

        _rng = new MewdekoRandom();

        // girl ratings is a stupid command

        try
        {
            TypingArticles =
                JsonConvert.DeserializeObject<List<TypingArticle>>(File.ReadAllText(TYPING_ARTICLES_PATH));
        }
        catch (Exception ex)
        {
            Log.Warning("Error while loading typing articles {0}", ex.ToString());
            TypingArticles = new List<TypingArticle>();
        }
    }

    public IReadOnlyList<string> EightBallResponses => _gamesConfig.Data.EightBallResponses;

    public List<TypingArticle> TypingArticles { get; }

    //channelId, game
    public ConcurrentDictionary<ulong, AcrophobiaGame> AcrophobiaGames { get; } = new();

    public ConcurrentDictionary<ulong, Hangman> HangmanGames { get; } = new();
    public TermPool TermPool { get; } = new();

    public ConcurrentDictionary<ulong, TriviaGame> RunningTrivias { get; } = new();
    public Dictionary<ulong, TicTacToe> TicTacToeGames { get; } = new();
    public ConcurrentDictionary<ulong, TypingGame> RunningContests { get; } = new();
    public ConcurrentDictionary<ulong, NunchiGame> NunchiGames { get; } = new();

    public AsyncLazy<RatingTexts> Ratings { get; }

    public async Task Unload()
    {
        AcrophobiaGames.ForEach(x => x.Value.Dispose());
        AcrophobiaGames.Clear();
        HangmanGames.ForEach(x => x.Value.Dispose());
        HangmanGames.Clear();
        await Task.WhenAll(RunningTrivias.Select(x => x.Value.StopGame())).ConfigureAwait(false);
        RunningTrivias.Clear();

        TicTacToeGames.Clear();

        await Task.WhenAll(RunningContests.Select(x => x.Value.Stop()))
            .ConfigureAwait(false);
        RunningContests.Clear();
        NunchiGames.ForEach(x => x.Value.Dispose());
        NunchiGames.Clear();
    }

    public void AddTypingArticle(IUser user, string text)
    {
        TypingArticles.Add(new TypingArticle
        {
            Source = user.ToString(),
            Extra = $"Text added on {DateTime.UtcNow} by {user}.",
            Text = text.SanitizeMentions(true)
        });

        File.WriteAllText(TYPING_ARTICLES_PATH, JsonConvert.SerializeObject(TypingArticles));
    }

    public string GetEightballResponse(string _) => EightBallResponses[_rng.Next(0, EightBallResponses.Count)];

    public TypingArticle? RemoveTypingArticle(int index)
    {
        var articles = TypingArticles;
        if (index < 0 || index >= articles.Count)
            return null;

        var removed = articles[index];
        TypingArticles.RemoveAt(index);

        File.WriteAllText(TYPING_ARTICLES_PATH, JsonConvert.SerializeObject(articles));
        return removed;
    }

    public class RatingTexts
    {
        public string Nog { get; set; }
        public string Fun { get; set; }
        public string Uni { get; set; }
        public string Wif { get; set; }
        public string Dat { get; set; }
        public string Dan { get; set; }
    }
}
