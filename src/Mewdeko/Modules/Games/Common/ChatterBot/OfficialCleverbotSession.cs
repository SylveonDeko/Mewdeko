using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Serilog;

namespace Mewdeko.Modules.Games.Common.ChatterBot;

public class OfficialCleverbotSession : IChatterBotSession
{
    private readonly string apiKey;
    private readonly IHttpClientFactory httpFactory;
    private string cs;

    public OfficialCleverbotSession(string apiKey, IHttpClientFactory factory)
    {
        this.apiKey = apiKey;
        httpFactory = factory;
    }

    private string QueryString =>
        $"https://www.cleverbot.com/getreply?key={apiKey}&wrapper=Mewdeko&input={{0}}&cs={{1}}";

    public async Task<string>? Think(string input)
    {
        using var http = httpFactory.CreateClient();
        var dataString = await http.GetStringAsync(string.Format(QueryString, input, cs))
            .ConfigureAwait(false);
        try
        {
            var data = JsonConvert.DeserializeObject<CleverbotResponse>(dataString);

            cs = data?.Cs;
            return data?.Output;
        }
        catch
        {
            Log.Warning("Unexpected cleverbot response received: ");
            Log.Warning(dataString);
            return null;
        }
    }
}

public class CleverbotIoSession : IChatterBotSession
{
    private readonly string askEndpoint = "https://cleverbot.io/1.0/ask";

    private readonly string createEndpoint = "https://cleverbot.io/1.0/create";
    private readonly IHttpClientFactory httpFactory;
    private readonly string key;
    private readonly AsyncLazy<string> nick;
    private readonly string user;

    public CleverbotIoSession(string user, string key, IHttpClientFactory factory)
    {
        this.key = key;
        this.user = user;
        httpFactory = factory;

        nick = new AsyncLazy<string>(GetNick);
    }

    public async Task<string> Think(string input)
    {
        using var http = httpFactory.CreateClient();
        using var msg = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("user", user), new KeyValuePair<string, string>("key", key), new KeyValuePair<string, string>("nick", await nick),
            new KeyValuePair<string, string>("text", input)
        });
        using var data = await http.PostAsync(askEndpoint, msg).ConfigureAwait(false);
        var str = await data.Content.ReadAsStringAsync().ConfigureAwait(false);
        var obj = JsonConvert.DeserializeObject<CleverbotIoAskResponse>(str);
        if (obj.Status != "success")
            throw new OperationCanceledException(obj.Status);

        return obj.Response;
    }

    private async Task<string> GetNick()
    {
        using var http = httpFactory.CreateClient();
        using var msg = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("user", user), new KeyValuePair<string, string>("key", key)
        });
        using var data = await http.PostAsync(createEndpoint, msg).ConfigureAwait(false);
        var str = await data.Content.ReadAsStringAsync().ConfigureAwait(false);
        var obj = JsonConvert.DeserializeObject<CleverbotIoCreateResponse>(str);
        if (obj.Status != "success")
            throw new OperationCanceledException(obj.Status);

        return obj.Nick;
    }
}