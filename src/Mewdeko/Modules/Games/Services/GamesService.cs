using System.IO;
using System.Threading.Tasks;
using Mewdeko.Modules.Games.Common;
using Mewdeko.Modules.Games.Common.Acrophobia;
using Mewdeko.Modules.Games.Common.Hangman;
using Mewdeko.Modules.Games.Common.Nunchi;
using Mewdeko.Modules.Games.Common.Trivia;
using Newtonsoft.Json;
using Serilog;

namespace Mewdeko.Modules.Games.Services;

public class GamesService : INService, IUnloadableService
{
    private const string TypingArticlesPath = "data/typing_articles3.json";
    private readonly GamesConfigService gamesConfig;
    private readonly Random rng;

    public GamesService(GamesConfigService gamesConfig)
    {
        this.gamesConfig = gamesConfig;

        rng = new MewdekoRandom();

        // girl ratings is a stupid command

        try
        {
            TypingArticles =
                JsonConvert.DeserializeObject<List<TypingArticle>>(File.ReadAllText(TypingArticlesPath));
        }
        catch (Exception ex)
        {
            Log.Warning("Error while loading typing articles {0}", ex.ToString());
            TypingArticles = new List<TypingArticle>();
        }
    }

    public IReadOnlyList<string> EightBallResponses => gamesConfig.Data.EightBallResponses;

    public List<TypingArticle> TypingArticles { get; }

    //channelId, game
    public ConcurrentDictionary<ulong, AcrophobiaGame> AcrophobiaGames { get; } = new();

    public ConcurrentDictionary<ulong, Hangman> HangmanGames { get; } = new();
    public TermPool TermPool { get; } = new();

    public ConcurrentDictionary<ulong, TriviaGame> RunningTrivias { get; } = new();
    public Dictionary<ulong, TicTacToe> TicTacToeGames { get; } = new();
    public ConcurrentDictionary<ulong, TypingGame> RunningContests { get; } = new();
    public ConcurrentDictionary<ulong, NunchiGame> NunchiGames { get; } = new();

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
            Source = user.ToString(), Extra = $"Text added on {DateTime.UtcNow} by {user}.", Text = text.SanitizeMentions(true)
        });

        File.WriteAllText(TypingArticlesPath, JsonConvert.SerializeObject(TypingArticles));
    }

    public string GetEightballResponse(string _) => EightBallResponses[rng.Next(0, EightBallResponses.Count)];

    public TypingArticle? RemoveTypingArticle(int index)
    {
        if (index < 0 || index >= TypingArticles.Count)
            return null;

        var removed = TypingArticles[index];
        TypingArticles.RemoveAt(index);

        File.WriteAllText(TypingArticlesPath, JsonConvert.SerializeObject(TypingArticles));
        return removed;
    }
}