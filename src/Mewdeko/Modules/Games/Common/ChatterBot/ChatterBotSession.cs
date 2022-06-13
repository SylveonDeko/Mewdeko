using Newtonsoft.Json;
using System.Net.Http;
using System.Threading.Tasks;

namespace Mewdeko.Modules.Games.Common.ChatterBot;

public class ChatterBotSession : IChatterBotSession
{
    private const int BOT_ID = 6;

    private readonly string _chatterBotId;
    private readonly IHttpClientFactory _httpFactory;

    public ChatterBotSession(IHttpClientFactory httpFactory)
    {
        _chatterBotId = Rng.Next(0, 1000000).ToString().ToBase64();
        _httpFactory = httpFactory;
    }

    private static MewdekoRandom Rng { get; } = new();

    private string ApiEndpoint =>
        $"https://api.program-o.com/v2/chatbot/?bot_id={BOT_ID}&say={{0}}&convo_id=Mewdeko_{_chatterBotId}&format=json";

    public async Task<string> Think(string message)
    {
        using var http = _httpFactory.CreateClient();
        var res = await http.GetStringAsync(string.Format(ApiEndpoint, message)).ConfigureAwait(false);
        var cbr = JsonConvert.DeserializeObject<ChatterBotResponse>(res);
        return cbr.BotSay.Replace("<br/>", "\n", StringComparison.InvariantCulture);
    }
}