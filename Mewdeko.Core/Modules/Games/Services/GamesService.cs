using Discord;
using Mewdeko.Common;
using Mewdeko.Core.Services;
using Mewdeko.Extensions;
using Mewdeko.Modules.Games.Common;
using Mewdeko.Modules.Games.Common.Acrophobia;
using Mewdeko.Modules.Games.Common.Hangman;
using Mewdeko.Modules.Games.Common.Nunchi;
using Mewdeko.Modules.Games.Common.Trivia;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace Mewdeko.Modules.Games.Services
{
    public class GamesService : INService, IUnloadableService
    {
        private readonly GamesConfigService _gamesConfig;

        public ConcurrentDictionary<ulong, GirlRating> GirlRatings { get; } = new ConcurrentDictionary<ulong, GirlRating>();

        public IReadOnlyList<string> EightBallResponses => _gamesConfig.Data.EightBallResponses;

        private readonly Timer _t;
        private readonly IHttpClientFactory _httpFactory;
        private readonly Random _rng;

        private const string TypingArticlesPath = "data/typing_articles3.json";

        public List<TypingArticle> TypingArticles { get; } = new List<TypingArticle>();

        //channelId, game
        public ConcurrentDictionary<ulong, AcrophobiaGame> AcrophobiaGames { get; } = new ConcurrentDictionary<ulong, AcrophobiaGame>();

        public ConcurrentDictionary<ulong, Hangman> HangmanGames { get; } = new ConcurrentDictionary<ulong, Hangman>();
        public TermPool TermPool { get; } = new TermPool();

        public ConcurrentDictionary<ulong, TriviaGame> RunningTrivias { get; } = new ConcurrentDictionary<ulong, TriviaGame>();
        public Dictionary<ulong, TicTacToe> TicTacToeGames { get; } = new Dictionary<ulong, TicTacToe>();
        public ConcurrentDictionary<ulong, TypingGame> RunningContests { get; } = new ConcurrentDictionary<ulong, TypingGame>();
        public ConcurrentDictionary<ulong, NunchiGame> NunchiGames { get; } = new ConcurrentDictionary<ulong, NunchiGame>();

        public AsyncLazy<RatingTexts> Ratings { get; }

        public class RatingTexts
        {
            public string Nog { get; set; }
            public string Tra { get; set; }
            public string Fun { get; set; }
            public string Uni { get; set; }
            public string Wif { get; set; }
            public string Dat { get; set; }
            public string Dan { get; set; }
        }

        public GamesService(GamesConfigService gamesConfig, IHttpClientFactory httpFactory)
        {
            _gamesConfig = gamesConfig;
            _httpFactory = httpFactory;

            Ratings = new AsyncLazy<RatingTexts>(GetRatingTexts);
            _rng = new MewdekoRandom();

            //girl ratings
            _t = new Timer((_) =>
            {
                GirlRatings.Clear();

            }, null, TimeSpan.FromDays(1), TimeSpan.FromDays(1));

            try
            {
                TypingArticles = JsonConvert.DeserializeObject<List<TypingArticle>>(File.ReadAllText(TypingArticlesPath));
            }
            catch (Exception ex)
            {
                Log.Warning("Error while loading typing articles {0}", ex.ToString());
                TypingArticles = new List<TypingArticle>();
            }
        }

        private async Task<RatingTexts> GetRatingTexts()
        {
            using (var http = _httpFactory.CreateClient())
            {
                var text = await http.GetStringAsync("https://Mewdeko-pictures.nyc3.digitaloceanspaces.com/other/rategirl/rates.json");
                return JsonConvert.DeserializeObject<RatingTexts>(text);
            }
        }

        public async Task Unload()
        {
            _t.Change(Timeout.Infinite, Timeout.Infinite);

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
                Text = text.SanitizeMentions(true),
            });

            File.WriteAllText(TypingArticlesPath, JsonConvert.SerializeObject(TypingArticles));
        }

        public string GetEightballResponse(string _)
        {
            return EightBallResponses[_rng.Next(0, EightBallResponses.Count)];
        }

        public TypingArticle RemoveTypingArticle(int index)
        {
            var articles = TypingArticles;
            if (index < 0 || index >= articles.Count)
                return null;

            var removed = articles[index];
            TypingArticles.RemoveAt(index);
                
            File.WriteAllText(TypingArticlesPath, JsonConvert.SerializeObject(articles));
            return removed;
        }
    }
}
