using System.Globalization;
using Mewdeko.Modules.Utility.Services;
using Serilog;
using YamlDotNet.Serialization;

namespace Mewdeko.Services.strings.impl
{
    /// <summary>
    /// Represents a service for retrieving bot strings.
    /// </summary>
    public class BotStrings : IBotStrings
    {
        private readonly ILocalization localization;
        private readonly IBotStringsProvider stringsProvider;

        /// <summary>
        ///     Used as failsafe in case response key doesn't exist in the selected or default language.
        /// </summary>
        private readonly CultureInfo? usCultureInfo = new("en-US");

        /// <summary>
        /// Initializes a new instance of the <see cref="BotStrings"/> class.
        /// </summary>
        /// <param name="loc">The localization service.</param>
        /// <param name="stringsProvider">The provider for bot strings.</param>
        public BotStrings(ILocalization loc, IBotStringsProvider stringsProvider)
        {
            localization = loc;
            this.stringsProvider = stringsProvider;
        }

        /// <summary>
        /// Retrieves the localized text corresponding to the specified key, optionally for the specified guild.
        /// </summary>
        public string? GetText(string? key, ulong? guildId = null, params object?[] data) =>
            GetText(key, localization.GetCultureInfo(guildId), data);

        /// <summary>
        /// Retrieves the localized text corresponding to the specified key and culture information.
        /// </summary>
        public string? GetText(string? key, CultureInfo? cultureInfo, params object?[] data)
        {
            // ReSharper disable once CoVariantArrayConversion
            if (cultureInfo.Name == "owo")
                data = data.Select(x => OwoServices.OwoIfy(x.ToString())).ToArray();
            try
            {
                return string.Format(GetText(key, cultureInfo), data);
            }
            catch (FormatException)
            {
                Log.Warning(
                    " Key '{Key}' is not properly formatted in '{LanguageName}' response strings. Please report this",
                    key, cultureInfo.Name);
                if (cultureInfo.Name != usCultureInfo.Name)
                    return GetText(key, usCultureInfo, data);
                return
                    $"I can't tell you if the command is executed, because there was an error printing out the response.\nKey '{key}' is not properly formatted. Please report this.";
            }
        }

        /// <summary>
        /// Retrieves the strings associated with a command, optionally for the specified guild.
        /// </summary>
        public CommandStrings GetCommandStrings(string commandName, ulong? guildId = null) =>
            GetCommandStrings(commandName, localization.GetCultureInfo(guildId));

        /// <summary>
        /// Retrieves the strings associated with a command and the specified culture information.
        /// </summary>
        public CommandStrings GetCommandStrings(string commandName, CultureInfo? cultureInfo)
        {
            var cmdStrings = stringsProvider.GetCommandStrings(cultureInfo.Name, commandName);
            if (cmdStrings is not null)
                return cmdStrings;
            if (cultureInfo.Name == "owo")
            {
                cmdStrings = stringsProvider.GetCommandStrings("en-US", commandName);
                cmdStrings.Desc = OwoServices.OwoIfy(cmdStrings.Desc);
                cmdStrings.Args = cmdStrings.Args.Select(OwoServices.OwoIfy).ToArray();
            }

            if (cultureInfo.Name != usCultureInfo.Name)
                return GetCommandStrings(commandName, usCultureInfo);
            Log.Warning("'{CommandName}' doesn't exist in 'en-US' command strings. Please report this",
                commandName);

            return new CommandStrings
            {
                Args =
                [
                    ""
                ],
                Desc = "?"
            };
        }

        /// <summary>
        /// Reloads the bot strings.
        /// </summary>
        public void Reload() => stringsProvider.Reload();

        private string? GetString(string? key, CultureInfo? cultureInfo) =>
            stringsProvider.GetText(cultureInfo.Name, key);

        /// <summary>
        /// Retrieves the localized text corresponding to the specified key and culture information.
        /// </summary>
        public string GetText(string? key, CultureInfo? cultureInfo)
        {
            var text = GetString(key, cultureInfo);

            if (string.IsNullOrWhiteSpace(text))
            {
                if (cultureInfo.Name == "owo")
                    return OwoServices.OwoIfy(GetString(key, usCultureInfo) ?? "to nya or to not nya?");
                Log.Warning(
                    "'{Key}' key is missing from '{LanguageName}' response strings. You may ignore this message", key,
                    cultureInfo.Name);
                text = GetString(key, usCultureInfo) ?? $"Error: dkey {key} not found!";
                if (string.IsNullOrWhiteSpace(text))
                {
                    return
                        $"I can't tell you if the command is executed, because there was an error printing out the response. Key '{key}' is missing from resources. You may ignore this message.";
                }
            }

            return text;
        }
    }

    /// <summary>
    /// Represents strings associated with a command.
    /// </summary>
    public class CommandStrings
    {
        /// <summary>
        /// Gets or sets the description of the command.
        /// </summary>
        [YamlMember(Alias = "desc")]
        public string Desc { get; set; }

        /// <summary>
        ///
        /// </summary>
        [YamlMember(Alias = "args")]
        public string[] Args { get; set; }
    }
}