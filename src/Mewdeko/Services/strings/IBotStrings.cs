using System.Globalization;
using Mewdeko.Services.strings.impl;

namespace Mewdeko.Services.strings
{
    /// <summary>
    /// Defines methods to retrieve and reload bot strings.
    /// </summary>
    public interface IBotStrings
    {
        /// <summary>
        /// Gets the text associated with the specified key.
        /// </summary>
        /// <param name="key">The key of the text to retrieve.</param>
        /// <param name="guildId">The ID of the guild (server) if the text is specific to a guild. Default is null.</param>
        /// <param name="data">Additional data to format the text.</param>
        /// <returns>The retrieved text.</returns>
        string? GetText(string? key, ulong? guildId = null, params object?[] data);

        /// <summary>
        /// Gets the text associated with the specified key and culture.
        /// </summary>
        /// <param name="key">The key of the text to retrieve.</param>
        /// <param name="locale">The culture info specifying the locale of the text. Default is null.</param>
        /// <param name="data">Additional data to format the text.</param>
        /// <returns>The retrieved text.</returns>
        string GetText(string? key, CultureInfo? locale, params object?[] data);

        /// <summary>
        /// Reloads the bot strings.
        /// </summary>
        void Reload();

        /// <summary>
        /// Gets the command strings associated with the specified command name.
        /// </summary>
        /// <param name="commandName">The name of the command.</param>
        /// <param name="guildId">The ID of the guild (server) if the command strings are specific to a guild. Default is null.</param>
        /// <returns>The command strings.</returns>
        CommandStrings GetCommandStrings(string commandName, ulong? guildId = null);

        /// <summary>
        /// Gets the command strings associated with the specified command name and culture.
        /// </summary>
        /// <param name="commandName">The name of the command.</param>
        /// <param name="cultureInfo">The culture info specifying the locale of the command strings. Default is null.</param>
        /// <returns>The command strings.</returns>
        CommandStrings GetCommandStrings(string commandName, CultureInfo? cultureInfo);
    }
}