using Mewdeko.Modules.Utility.Services;
using Serilog;
using System.Globalization;
using YamlDotNet.Serialization;

namespace Mewdeko.Services.strings.impl;

public class BotStrings : IBotStrings
{
    private readonly ILocalization _localization;
    private readonly IBotStringsProvider _stringsProvider;

    /// <summary>
    ///     Used as failsafe in case response key doesn't exist in the selected or default language.
    /// </summary>
    private readonly CultureInfo? _usCultureInfo = new("en-US");

    public BotStrings(ILocalization loc, IBotStringsProvider stringsProvider)
    {
        _localization = loc;
        _stringsProvider = stringsProvider;
    }

    public string? GetText(string? key, ulong? guildId = null, params object?[] data) => GetText(key, _localization.GetCultureInfo(guildId), data);

    public string? GetText(string? key, CultureInfo? cultureInfo, params object?[] data)
    {
        // ReSharper disable once CoVariantArrayConversion
        if (cultureInfo.Name == "owo") data = data.Select(x => OwoServices.OwoIfy(x.ToString())).ToArray();
        try
        {
            return string.Format(GetText(key, cultureInfo), data);
        }
        catch (FormatException)
        {
            Log.Warning(
                " Key '{Key}' is not properly formatted in '{LanguageName}' response strings. Please report this",
                key, cultureInfo.Name);
            if (cultureInfo.Name != _usCultureInfo.Name)
                return GetText(key, _usCultureInfo, data);
            return
                $"I can't tell you if the command is executed, because there was an error printing out the response.\nKey '{key}' is not properly formatted. Please report this.";
        }
    }

    public CommandStrings GetCommandStrings(string commandName, ulong? guildId = null) => GetCommandStrings(commandName, _localization.GetCultureInfo(guildId));

    public CommandStrings GetCommandStrings(string commandName, CultureInfo? cultureInfo)
    {
        var cmdStrings = _stringsProvider.GetCommandStrings(cultureInfo.Name, commandName);
        if (cmdStrings is not null) return cmdStrings;
        if (cultureInfo.Name == "owo")
        {
            cmdStrings = _stringsProvider.GetCommandStrings("en-US", commandName);
            cmdStrings.Desc = OwoServices.OwoIfy(cmdStrings.Desc);
            cmdStrings.Args = cmdStrings.Args.Select(x => OwoServices.OwoIfy(x)).ToArray();
        }
        if (cultureInfo.Name != _usCultureInfo.Name) return GetCommandStrings(commandName, _usCultureInfo);
        Log.Warning("'{CommandName}' doesn't exist in 'en-US' command strings. Please report this",
            commandName);

        return new CommandStrings
        {
            Args = new[] { "" },
            Desc = "?"
        };
    }

    public void Reload() => _stringsProvider.Reload();

    private string? GetString(string? key, CultureInfo? cultureInfo) => _stringsProvider.GetText(cultureInfo.Name, key);

    public string GetText(string? key, CultureInfo? cultureInfo)
    {
        var text = GetString(key, cultureInfo);

        if (string.IsNullOrWhiteSpace(text))
        {
            if (cultureInfo.Name == "owo")
                return OwoServices.OwoIfy(GetString(key, _usCultureInfo));
            Log.Warning(
                "'{Key}' key is missing from '{LanguageName}' response strings. You may ignore this message", key,
                cultureInfo.Name);
            text = GetString(key, _usCultureInfo) ?? $"Error: dkey {key} not found!";
            if (string.IsNullOrWhiteSpace(text))
            {
                return
                    $"I can't tell you if the command is executed, because there was an error printing out the response. Key '{key}' is missing from resources. You may ignore this message.";
            }
        }

        return text;
    }
}

public class CommandStrings
{
    [YamlMember(Alias = "desc")] public string Desc { get; set; }

    [YamlMember(Alias = "args")] public string[] Args { get; set; }
}