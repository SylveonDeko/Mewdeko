using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Mewdeko.Common;
using Mewdeko.Core.Services;
using Mewdeko.Core.Services.Impl;
using Mewdeko.Extensions;
using Mewdeko.Modules.Games.Common;
using Mewdeko.Modules.Games.Common.Acrophobia;
using Mewdeko.Modules.Games.Common.Hangman;
using Mewdeko.Modules.Games.Common.Nunchi;
using Mewdeko.Modules.Games.Common.Trivia;
using Newtonsoft.Json;
using NLog;

namespace Mewdeko.Modules.Games.Services
{
    public class GamesService : INService, IUnloadableService
    {
        private readonly IBotConfigProvider _bc;
        private readonly CommandHandler _cmd;
        private readonly CommandHandler _cmdHandler;
        private readonly ICurrencyService _cs;
        private readonly FontProvider _fonts;
        private readonly IHttpClientFactory _httpFactory;
        private readonly IImageCache _images;
        private readonly Logger _log;
        private readonly MewdekoRandom _rng;
        private readonly IBotStrings _strings;

        private readonly Timer _t;

        public GamesService(CommandHandler cmd, IBotConfigProvider bc, Mewdeko bot,
            IBotStrings strings, IDataCache data, CommandHandler cmdHandler,
            ICurrencyService cs, FontProvider fonts, IHttpClientFactory httpFactory)
        {
            _bc = bc;
            _cmd = cmd;
            _strings = strings;
            _images = data.LocalImages;
            _cmdHandler = cmdHandler;
            _log = LogManager.GetCurrentClassLogger();
            _rng = new MewdekoRandom();
            _cs = cs;
            _fonts = fonts;
            _httpFactory = httpFactory;

            Ratings = new AsyncLazy<RatingTexts>(GetRatingTexts);

            //8ball
            EightBallResponses = _bc.BotConfig.EightBallResponses.Select(ebr => ebr.Text).ToImmutableArray();

            //girl ratings
            _t = new Timer(_ => { GirlRatings.Clear(); }, null, TimeSpan.FromDays(1), TimeSpan.FromDays(1));

            try
            {
                TypingArticles =
                    JsonConvert.DeserializeObject<List<TypingArticle>>(File.ReadAllText(TypingArticlesPath));
            }
            catch (Exception ex)
            {
                _log.Warn("Error while loading typing articles {0}", ex.ToString());
                TypingArticles = new List<TypingArticle>();
            }
        }

        public ConcurrentDictionary<ulong, GirlRating> GirlRatings { get; } = new();

        public ImmutableArray<string> EightBallResponses { get; }

        public string TypingArticlesPath { get; } = "data/typing_articles3.json";

        public List<TypingArticle> TypingArticles { get; } = new();

        //channelId, game
        public ConcurrentDictionary<ulong, AcrophobiaGame> AcrophobiaGames { get; } = new();

        public ConcurrentDictionary<ulong, Hangman> HangmanGames { get; } = new();
        public TermPool TermPool { get; } = new();

        public ConcurrentDictionary<ulong, TriviaGame> RunningTrivias { get; } = new();
        public Dictionary<ulong, TicTacToe> TicTacToeGames { get; } = new();
        public ConcurrentDictionary<ulong, TypingGame> RunningContests { get; } = new();
        public ConcurrentDictionary<ulong, NunchiGame> NunchiGames { get; } = new();

        public AsyncLazy<RatingTexts> Ratings { get; }
        private ConcurrentDictionary<ulong, object> _locks { get; } = new();

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

        private async Task<RatingTexts> GetRatingTexts()
        {
            using (var http = _httpFactory.CreateClient())
            {
                var text = await http.GetStringAsync(
                    "https://Mewdeko-pictures.nyc3.digitaloceanspaces.com/other/rategirl/rates.json");
                return JsonConvert.DeserializeObject<RatingTexts>(text);
            }
        }

        private void DisposeElems(IEnumerable<IDisposable> xs)
        {
            xs.ForEach(x => x.Dispose());
        }

        public void AddTypingArticle(IUser user, string text)
        {
            TypingArticles.Add(new TypingArticle
            {
                Source = user.ToString(),
                Extra = $"Text added on {DateTime.UtcNow} by {user}.",
                Text = text.SanitizeMentions(true)
            });

            File.WriteAllText(TypingArticlesPath, JsonConvert.SerializeObject(TypingArticles));
        }

        private string GetText(ITextChannel ch, string key, params object[] rep)
        {
            return _strings.GetText(key, ch.GuildId, rep);
        }

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
    }
}