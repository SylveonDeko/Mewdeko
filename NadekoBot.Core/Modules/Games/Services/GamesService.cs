using Discord;
using NadekoBot.Common;
using NadekoBot.Core.Services;
using NadekoBot.Core.Services.Impl;
using NadekoBot.Extensions;
using NadekoBot.Modules.Games.Common;
using NadekoBot.Modules.Games.Common.Acrophobia;
using NadekoBot.Modules.Games.Common.Hangman;
using NadekoBot.Modules.Games.Common.Nunchi;
using NadekoBot.Modules.Games.Common.Trivia;
using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace NadekoBot.Modules.Games.Services
{
    public class GamesService : INService, IUnloadableService
    {
        private readonly IBotConfigProvider _bc;

        public ConcurrentDictionary<ulong, GirlRating> GirlRatings { get; } = new ConcurrentDictionary<ulong, GirlRating>();

        public ImmutableArray<string> EightBallResponses { get; }

        private readonly Timer _t;
        private readonly CommandHandler _cmd;
        private readonly IBotStrings _strings;
        private readonly IImageCache _images;
        private readonly Logger _log;
        private readonly NadekoRandom _rng;
        private readonly ICurrencyService _cs;
        private readonly FontProvider _fonts;
        private readonly IHttpClientFactory _httpFactory;

        public string TypingArticlesPath { get; } = "data/typing_articles3.json";
        private readonly CommandHandler _cmdHandler;

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

        public GamesService(CommandHandler cmd, IBotConfigProvider bc, NadekoBot bot,
            IBotStrings strings, IDataCache data, CommandHandler cmdHandler,
            ICurrencyService cs, FontProvider fonts, IHttpClientFactory httpFactory)
        {
            _bc = bc;
            _cmd = cmd;
            _strings = strings;
            _images = data.LocalImages;
            _cmdHandler = cmdHandler;
            _log = LogManager.GetCurrentClassLogger();
            _rng = new NadekoRandom();
            _cs = cs;
            _fonts = fonts;
            _httpFactory = httpFactory;

            Ratings = new AsyncLazy<RatingTexts>(GetRatingTexts);

            //8ball
            EightBallResponses = _bc.BotConfig.EightBallResponses.Select(ebr => ebr.Text).ToImmutableArray();

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
                _log.Warn("Error while loading typing articles {0}", ex.ToString());
                TypingArticles = new List<TypingArticle>();
            }
        }

        private async Task<RatingTexts> GetRatingTexts()
        {
            using (var http = _httpFactory.CreateClient())
            {
                var text = await http.GetStringAsync("https://nadeko-pictures.nyc3.digitaloceanspaces.com/other/rategirl/rates.json");
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
                Text = text.SanitizeMentions(true),
            });

            File.WriteAllText(TypingArticlesPath, JsonConvert.SerializeObject(TypingArticles));
        }
        private ConcurrentDictionary<ulong, object> _locks { get; } = new ConcurrentDictionary<ulong, object>();

        private string GetText(ITextChannel ch, string key, params object[] rep)
            => _strings.GetText(key, ch.GuildId, rep);
    }
}
