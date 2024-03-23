namespace Mewdeko.Modules.Games.Common.ChatterBot
{
    /// <summary>
    /// Represents a response from Cleverbot.
    /// </summary>
    public class CleverbotResponse
    {
        /// <summary>
        /// Gets or sets the conversation state.
        /// </summary>
        public string Cs { get; set; }

        /// <summary>
        /// Gets or sets the output from Cleverbot.
        /// </summary>
        public string Output { get; set; }
    }

    /// <summary>
    /// Represents a response from Cleverbot.io when creating a bot.
    /// </summary>
    public class CleverbotIoCreateResponse
    {
        /// <summary>
        /// Gets or sets the status of the response.
        /// </summary>
        public string Status { get; set; }

        /// <summary>
        /// Gets or sets the nickname of the created bot.
        /// </summary>
        public string Nick { get; set; }
    }

    /// <summary>
    /// Represents a response from Cleverbot.io when asking a question.
    /// </summary>
    public class CleverbotIoAskResponse
    {
        /// <summary>
        /// Gets or sets the status of the response.
        /// </summary>
        public string Status { get; set; }

        /// <summary>
        /// Gets or sets the response from Cleverbot.
        /// </summary>
        public string Response { get; set; }
    }
}