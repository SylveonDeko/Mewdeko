namespace Mewdeko.Modules.Games.Common.ChatterBot
{
    /// <summary>
    /// Represents a response from a ChatterBot.
    /// </summary>
    public class ChatterBotResponse
    {
        /// <summary>
        /// Gets or sets the conversation ID associated with the response.
        /// </summary>
        public string ConvoId { get; set; }

        /// <summary>
        /// Gets or sets the response from the ChatterBot.
        /// </summary>
        public string BotSay { get; set; }
    }
}