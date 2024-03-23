using System.Net.Http;
using Newtonsoft.Json;

namespace Mewdeko.Modules.Games.Common.ChatterBot
{
    /// <summary>
    /// Represents a session with Cleverbot.
    /// </summary>
    public class ChatterBotSession : IChatterBotSession
    {
        private const int BotId = 6;

        private readonly string chatterBotId;
        private readonly IHttpClientFactory httpFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="ChatterBotSession"/> class.
        /// </summary>
        /// <param name="httpFactory">The HTTP client factory.</param>
        public ChatterBotSession(IHttpClientFactory httpFactory)
        {
            chatterBotId = Rng.Next(0, 1000000).ToString().ToBase64();
            this.httpFactory = httpFactory;
        }

        private static MewdekoRandom Rng { get; } = new();

        private string ApiEndpoint =>
            $"https://api.program-o.com/v2/chatbot/?bot_id={BotId}&say={{0}}&convo_id=Mewdeko_{chatterBotId}&format=json";

        /// <summary>
        /// Sends a message to Cleverbot and retrieves its response.
        /// </summary>
        /// <param name="message">The message to send to the ChatterBot.</param>
        /// <returns>The response from the Cleverbot.</returns>
        public async Task<string> Think(string message)
        {
            using var http = httpFactory.CreateClient();
            var res = await http.GetStringAsync(string.Format(ApiEndpoint, message)).ConfigureAwait(false);
            var cbr = JsonConvert.DeserializeObject<ChatterBotResponse>(res);
            return cbr.BotSay.Replace("<br/>", "\n", StringComparison.InvariantCulture);
        }
    }
}