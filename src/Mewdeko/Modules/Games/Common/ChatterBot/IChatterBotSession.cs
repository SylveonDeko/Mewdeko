namespace Mewdeko.Modules.Games.Common.ChatterBot
{
    /// <summary>
    /// Interface for interacting with a CleverBot session.
    /// </summary>
    public interface IChatterBotSession
    {
        /// <summary>
        /// Sends an input message to the Cleverbot and receives its response.
        /// </summary>
        /// <param name="input">The input message to send to the CleverBot.</param>
        /// <returns>The response from CleverBot.</returns>
        Task<string> Think(string input);
    }
}