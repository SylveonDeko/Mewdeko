using System.IO;
using Mewdeko.Modules.Games.Common.Trivia;
using Newtonsoft.Json;
using Serilog;
using StackExchange.Redis;

namespace Mewdeko.Services.Impl;

public class RedisLocalDataCache : ILocalDataCache
{
    private const string QuestionsFile = "data/trivia_questions.json";
    private readonly ConnectionMultiplexer con;
    private readonly IBotCredentials creds;

    public RedisLocalDataCache(ConnectionMultiplexer con, IBotCredentials creds, int shardId)
    {
        this.con = con;
        this.creds = creds;

        if (shardId != 0) return;
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

    public TriviaQuestion[] TriviaQuestions
    {
        get => Get<TriviaQuestion[]>("trivia_questions");
        private set => Set("trivia_questions", value);
    }

    private T Get<T>(string key) where T : class => JsonConvert.DeserializeObject<T>(Db.StringGet($"{creds.RedisKey()}_localdata_{key}"));

    private void Set(string key, object obj) => Db.StringSet($"{creds.RedisKey()}_localdata_{key}", JsonConvert.SerializeObject(obj));
}