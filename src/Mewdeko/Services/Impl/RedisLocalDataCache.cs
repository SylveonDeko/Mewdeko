using System.IO;
using Mewdeko.Modules.Games.Common.Trivia;
using Newtonsoft.Json;
using Serilog;
using StackExchange.Redis;

namespace Mewdeko.Services.Impl
{
    /// <summary>
    /// Represents a Redis-based cache for local data.
    /// </summary>
    public class RedisLocalDataCache : ILocalDataCache
    {
        private const string QuestionsFile = "data/trivia_questions.json";
        private readonly ConnectionMultiplexer con;
        private readonly IBotCredentials creds;

        /// <summary>
        /// Initializes a new instance of the <see cref="RedisLocalDataCache"/> class.
        /// </summary>
        /// <param name="con">The connection multiplexer for Redis.</param>
        /// <param name="creds">The bot credentials.</param>
        /// <param name="shardId">The shard ID.</param>
        public RedisLocalDataCache(ConnectionMultiplexer con, IBotCredentials creds)
        {
            this.con = con;
            this.creds = creds;

            try
            {
                TriviaQuestions = JsonConvert.DeserializeObject<TriviaQuestion[]>(File.ReadAllText(QuestionsFile));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error loading local data");
                throw;
            }
        }

        private IDatabase Db => con.GetDatabase();

        /// <summary>
        /// Gets or sets the trivia questions stored in the cache.
        /// </summary>
        public TriviaQuestion[] TriviaQuestions
        {
            get => Get<TriviaQuestion[]>("trivia_questions");
            private set => Set("trivia_questions", value);
        }

        private T Get<T>(string key) where T : class =>
            JsonConvert.DeserializeObject<T>(Db.StringGet($"{creds.RedisKey()}_localdata_{key}"));

        private void Set(string key, object obj) =>
            Db.StringSet($"{creds.RedisKey()}_localdata_{key}", JsonConvert.SerializeObject(obj));
    }
}