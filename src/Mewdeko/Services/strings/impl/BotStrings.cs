using System.Globalization;
using Mewdeko.Modules.Utility.Services;
using Serilog;
using YamlDotNet.Serialization;

namespace Mewdeko.Services.strings.impl;

/// <summary>
///     Represents a service for retrieving bot strings.
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
    ///     Initializes a new instance of the <see cref="BotStrings" /> class.
    /// </summary>
    /// <param name="loc">The localization service.</param>
    /// <param name="stringsProvider">The provider for bot strings.</param>
    public BotStrings(ILocalization loc, IBotStringsProvider stringsProvider)
    {
        localization = loc;
        this.stringsProvider = stringsProvider;
    }

    /// <summary>
    ///     Retrieves the localized text corresponding to the specified key, optionally for the specified guild.
    /// </summary>
    public string? GetText(string? key, ulong? guildId = null, params object?[] data)
    {
        return GetText(key, localization.GetCultureInfo(guildId), data);
    }

    /// <summary>
    ///     Retrieves the localized text corresponding to the specified key and culture information.
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
    ///     Retrieves the strings associated with a command, optionally for the specified guild.
    /// </summary>
    /// <param name="commandName">The commandname string.</param>
    /// <param name="guildId">The guild identifier.</param>
    public CommandStrings GetCommandStrings(string commandName, ulong? guildId = null)
    {
        return GetCommandStrings(commandName, localization.GetCultureInfo(guildId));
    }

    /// <summary>
    ///     Retrieves the strings associated with a command and the specified culture information.
    /// </summary>
    /// <param name="commandName">The commandname string.</param>
    /// <param name="cultureInfo">The cultureInfo parameter.</param>
    public CommandStrings GetCommandStrings(string commandName, CultureInfo? cultureInfo)
    {
        var lookupName = cultureInfo.Name;
        var cmdStrings = stringsProvider.GetCommandStrings(lookupName, commandName);

        if (cmdStrings is null && TryCreateSpecificCulture(cultureInfo, out var specificCulture))
        {
            lookupName = specificCulture.Name;
            cmdStrings = stringsProvider.GetCommandStrings(lookupName, commandName);
        }

        if (cmdStrings is not null)
        {
            // Apply owo transformation if needed
            if (cultureInfo.Name == "owo" && cmdStrings.Overloads.Count == 0)
            {
                cmdStrings.Desc = OwoServices.OwoIfy(cmdStrings.Desc);
                cmdStrings.Args = cmdStrings.Args.Select(OwoServices.OwoIfy).ToArray();

                // Transform parameter descriptions
                foreach (var param in cmdStrings.Parameters)
                {
                    param.Description = OwoServices.OwoIfy(param.Description);
                }
            }

            return cmdStrings;
        }

        // Try to get overloads if available
        var overloadedCmdStrings = stringsProvider.GetCommandOverloads(lookupName, commandName);
        if (overloadedCmdStrings is { Count: > 0 })
        {
            // Construct a unified CommandStrings from overloads
            var result = new CommandStrings
            {
                Desc = overloadedCmdStrings[0].Desc,
                Args = overloadedCmdStrings[0].Args,
                IsOverload = false,
                Overloads = overloadedCmdStrings.Skip(1).ToList() // First one becomes main, rest are overloads
            };

            if (cultureInfo.Name == "owo")
            {
                result.Desc = OwoServices.OwoIfy(result.Desc);
                result.Args = result.Args.Select(OwoServices.OwoIfy).ToArray();
                // Transform overloads too
                foreach (var overload in result.Overloads)
                {
                    overload.Desc = OwoServices.OwoIfy(overload.Desc);
                    overload.Args = overload.Args.Select(OwoServices.OwoIfy).ToArray();
                    foreach (var param in overload.Parameters)
                    {
                        param.Description = OwoServices.OwoIfy(param.Description);
                    }
                }
            }

            return result;
        }

        // Fall back to US culture if needed
        if (cultureInfo.Name != usCultureInfo.Name)
            return GetCommandStrings(commandName, usCultureInfo);

        Log.Warning("'{CommandName}' doesn't exist in 'en-US' command strings. Please report this",
            commandName);

        return new CommandStrings
        {
            Args = [""], Desc = "?", Parameters = [], Overloads = []
        };
    }

    /// <summary>
    ///     Reloads the bot strings.
    /// </summary>
    public void Reload()
    {
        stringsProvider.Reload();
    }

    private string? GetString(string? key, CultureInfo? cultureInfo)
    {
        var text = stringsProvider.GetText(cultureInfo.Name, key);
        if (!string.IsNullOrWhiteSpace(text))
            return text;

        // e.g. a guild/default locale of "en" (neutral) should fall back to "en-US" (specific)
        // before we give up and log a missing-key warning, since our string files are keyed
        // by specific culture names except where the locale itself is neutral (e.g. "ar").
        return TryCreateSpecificCulture(cultureInfo, out var specificCulture)
            ? stringsProvider.GetText(specificCulture.Name, key)
            : text;
    }

    /// <summary>
    ///     Attempts to resolve the default specific culture for a neutral culture (e.g. "en" -&gt; "en-US"),
    ///     used as a secondary lookup when the exact culture name has no matching strings.
    /// </summary>
    private static bool TryCreateSpecificCulture(CultureInfo cultureInfo, out CultureInfo specificCulture)
    {
        specificCulture = null!;

        // "owo"/"ts-TS" are custom pseudo-locales, not real neutral cultures - leave them alone.
        if (!cultureInfo.IsNeutralCulture || cultureInfo.Name == "owo")
            return false;

        try
        {
            specificCulture = CultureInfo.CreateSpecificCulture(cultureInfo.Name);
            return true;
        }
        catch (CultureNotFoundException)
        {
            return false;
        }
    }

    /// <summary>
    ///     Retrieves the localized text corresponding to the specified key and culture information.
    /// </summary>
    /// <param name="key">The key string.</param>
    /// <param name="cultureInfo">The cultureInfo parameter.</param>
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
///     Represents strings associated with a command.
/// </summary>
public class CommandStrings
{
    /// <summary>
    ///     Gets or sets the description of the command.
    /// </summary>
    [YamlMember(Alias = "desc")]
    public string Desc { get; set; }

    /// <summary>
    ///     Command usage examples
    /// </summary>
    [YamlMember(Alias = "args")]
    public string[] Args { get; set; }

    /// <summary>
    ///     Parameter information for this command
    /// </summary>
    [YamlMember(Alias = "params")]
    public List<ParameterString> Parameters { get; set; } = [];

    /// <summary>
    ///     Overloaded versions of this command (if any)
    /// </summary>
    [YamlMember(Alias = "overloads")]
    public List<CommandOverload> Overloads { get; set; } = [];

    /// <summary>
    ///     The method signature for this command (for overload identification)
    /// </summary>
    [YamlMember(Alias = "signature")]
    public string Signature { get; set; } = string.Empty;

    /// <summary>
    ///     Whether this command is an overload of another command
    /// </summary>
    [YamlMember(Alias = "isOverload")]
    public bool IsOverload { get; set; }
}

/// <summary>
///     Represents a parameter in a command
/// </summary>
public class ParameterString
{
    /// <summary>
    ///     Gets or sets the name of the parameter
    /// </summary>
    [YamlMember(Alias = "name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the description of the parameter
    /// </summary>
    [YamlMember(Alias = "desc")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the type of the parameter
    /// </summary>
    [YamlMember(Alias = "type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    ///     Whether the parameter is optional
    /// </summary>
    [YamlMember(Alias = "optional")]
    public bool IsOptional { get; set; }

    /// <summary>
    ///     Default value for optional parameters
    /// </summary>
    [YamlMember(Alias = "default")]
    public string DefaultValue { get; set; } = string.Empty;

    /// <summary>
    ///     Whether this is a params parameter
    /// </summary>
    [YamlMember(Alias = "isParams")]
    public bool IsParams { get; set; }
}

/// <summary>
///     Represents an overloaded version of a command
/// </summary>
public class CommandOverload
{
    /// <summary>
    ///     Gets or sets the description of this overload
    /// </summary>
    [YamlMember(Alias = "desc")]
    public string Desc { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the usage examples for this overload
    /// </summary>
    [YamlMember(Alias = "args")]
    public string[] Args { get; set; } = [];

    /// <summary>
    ///     Gets or sets the parameters for this overload
    /// </summary>
    [YamlMember(Alias = "params")]
    public List<ParameterString> Parameters { get; set; } = [];

    /// <summary>
    ///     Gets or sets the signature for this overload
    /// </summary>
    [YamlMember(Alias = "signature")]
    public string Signature { get; set; } = string.Empty;
}