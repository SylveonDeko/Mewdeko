using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Mewdeko.Modules.Games.Common.ChatterBot;

public class ChatterBotSession : IChatterBotSession
{
    private const int BotId = 6;

    private readonly string chatterBotId;
    private readonly IHttpClientFactory httpFactory;

    public ChatterBotSession(IHttpClientFactory httpFactory)
    {
        chatterBotId = Rng.Next(0, 1000000).ToString().ToBase64();
        this.httpFactory = httpFactory;
    }

    private static MewdekoRandom Rng { get; } = new();

    private string ApiEndpoint =>
        $"https://api.program-o.com/v2/chatbot/?bot_id={BotId}&say={{0}}&convo_id=Mewdeko_{chatterBotId}&format=json";

    public async Task<string> Think(string message)
    {
        using var http = httpFactory.CreateClient();
        var res = await http.GetStringAsync(string.Format(ApiEndpoint, message)).ConfigureAwait(false);
        var cbr = JsonConvert.DeserializeObject<ChatterBotResponse>(res);
        return cbr.BotSay.Replace("<br/>", "\n", StringComparison.InvariantCulture);
    }
}