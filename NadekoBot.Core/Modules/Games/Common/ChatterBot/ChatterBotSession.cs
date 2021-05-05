using System;
using System.Net.Http;
using System.Threading.Tasks;
using NadekoBot.Common;
using NadekoBot.Extensions;
using Newtonsoft.Json;

namespace NadekoBot.Modules.Games.Common.ChatterBot
{
    public class ChatterBotSession : IChatterBotSession
    {
        private static NadekoRandom Rng { get; } = new NadekoRandom();

        private readonly string _chatterBotId;
        private readonly IHttpClientFactory _httpFactory;
        private readonly int _botId = 6;

        public ChatterBotSession(IHttpClientFactory httpFactory)
        {
            _chatterBotId = Rng.Next(0, 1000000).ToString().ToBase64();
            _httpFactory = httpFactory;
        }

        private string ApiEndpoint => "http://api.program-o.com/v2/chatbot/" +
                                      $"?bot_id={_botId}&" +
                                      "say={0}&" +
                                      $"convo_id=nadekobot_{_chatterBotId}&" +
                                      "format=json";

        public async Task<string> Think(string message)
        {
            using (var http = _httpFactory.CreateClient())
            {
                var res = await http.GetStringAsync(string.Format(ApiEndpoint, message)).ConfigureAwait(false);
                var cbr = JsonConvert.DeserializeObject<ChatterBotResponse>(res);
                return cbr.BotSay.Replace("<br/>", "\n", StringComparison.InvariantCulture);
            }
        }
    }
}
