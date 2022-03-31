using Mewdeko.Extensions;
using Mewdeko.Modules.Games.Common.Trivia;
using Newtonsoft.Json;
using Serilog;
using StackExchange.Redis;
using System.IO;

namespace Mewdeko.Services.Impl;

public class RedisLocalDataCache : ILocalDataCache
{
    private const string QUESTIONS_FILE = "data/trivia_questions.json";
    private readonly ConnectionMultiplexer _con;
    private readonly IBotCredentials _creds;

    public RedisLocalDataCache(ConnectionMultiplexer con, IBotCredentials creds, int shardId)
    {
        _con = con;
        _creds = creds;

        if (shardId != 0) return;
        try
        {
            TriviaQuestions = JsonConvert.DeserializeObject<TriviaQuestion[]>(File.ReadAllText(QUESTIONS_FILE));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error loading local data");
            throw;
        }
    }

    private IDatabase Db => _con.GetDatabase();

    public TriviaQuestion[] TriviaQuestions
    {
        get => Get<TriviaQuestion[]>("trivia_questions");
        private set => Set("trivia_questions", value);
    }


    private T Get<T>(string key) where T : class => JsonConvert.DeserializeObject<T>(Db.StringGet($"{_creds.RedisKey()}_localdata_{key}"));

    private void Set(string key, object obj) => Db.StringSet($"{_creds.RedisKey()}_localdata_{key}", JsonConvert.SerializeObject(obj));
}