using System.IO;
using Newtonsoft.Json;
using Serilog;
using YamlDotNet.Serialization;

namespace Mewdeko.Services.strings.impl;

/// <summary>
///     Loads strings from the local default filepath <see cref="responsesPath" />
/// </summary>
public class LocalFileStringsSource : IStringsSource
{
    private readonly string commandsPath;
    private readonly string responsesPath;

    public LocalFileStringsSource(string responsesPath = "data/strings/responses",
        string commandsPath = "data/strings/commands")
    {
        this.responsesPath = responsesPath;
        this.commandsPath = commandsPath;
    }

    public Dictionary<string, Dictionary<string, string>> GetResponseStrings()
    {
        var outputDict = new Dictionary<string, Dictionary<string, string>>();
        foreach (var file in Directory.GetFiles(responsesPath))
        {
            try
            {
                var langDict = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(file));
                var localeName = GetLocaleName(file);
                outputDict[localeName] = langDict;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error loading {FileName} response strings: {ErrorMessage}", file, ex.Message);
            }
        }

        return outputDict;
    }

    public Dictionary<string, Dictionary<string, CommandStrings>> GetCommandStrings()
    {
        var deserializer = new DeserializerBuilder()
            .Build();

        var outputDict = new Dictionary<string, Dictionary<string, CommandStrings>>();
        foreach (var file in Directory.GetFiles(commandsPath))
        {
            try
            {
                var text = File.ReadAllText(file);
                var langDict = deserializer.Deserialize<Dictionary<string, CommandStrings>>(text);
                var localeName = GetLocaleName(file);
                outputDict[localeName] = langDict;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error loading {FileName} command strings: {ErrorMessage}", file, ex.Message);
            }
        }

        return outputDict;
    }

    private static string GetLocaleName(string fileName)
    {
        fileName = Path.GetFileName(fileName);
        var dotIndex = fileName.IndexOf('.') + 1;
        var secondDotIndex = fileName.LastIndexOf('.');
        return fileName.Substring(dotIndex, secondDotIndex - dotIndex);
    }
}