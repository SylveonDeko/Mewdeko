using System.IO;
using System.Text.Json;
using Serilog;
using YamlDotNet.Serialization;

namespace Mewdeko.Services.strings.impl;

/// <summary>
///     Loads strings from the local default file paths.
/// </summary>
public class LocalFileStringsSource : IStringsSource
{
    private readonly string commandsPath;
    private readonly string responsesPath;

    /// <summary>
    ///     Initializes a new instance of the <see cref="LocalFileStringsSource" /> class.
    /// </summary>
    /// <param name="responsesPath">The path to the responses files.</param>
    /// <param name="commandsPath">The path to the commands files.</param>
    public LocalFileStringsSource(string responsesPath = "data/strings/responses",
        string commandsPath = "data/strings/commands")
    {
        this.responsesPath = responsesPath;
        this.commandsPath = commandsPath;
    }

    /// <summary>
    ///     Gets the response strings from the local files.
    /// </summary>
    /// <returns>A dictionary containing response strings for each locale.</returns>
    public Dictionary<string, Dictionary<string, string>> GetResponseStrings()
    {
        var outputDict = new Dictionary<string, Dictionary<string, string>>();
        foreach (var file in Directory.GetFiles(responsesPath))
        {
            try
            {
                var langDict = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(file));
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

    /// <summary>
    ///     Gets the command strings from the local files.
    ///     Supports both single-file format (commands.en-US.yml) and split-file format (en-US/*.yml).
    /// </summary>
    /// <returns>A dictionary containing command strings for each locale.</returns>
    public Dictionary<string, Dictionary<string, CommandStrings>> GetCommandStrings()
    {
        var deserializer = new DeserializerBuilder().Build();
        var outputDict = new Dictionary<string, Dictionary<string, CommandStrings>>();

        // First, load any single-file locales (legacy format: commands.en-US.yml)
        foreach (var file in Directory.GetFiles(commandsPath, "*.yml"))
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

        // Then, load split-file locales (new format: en-US/*.yml)
        foreach (var localeDir in Directory.GetDirectories(commandsPath))
        {
            var localeName = Path.GetFileName(localeDir);
            var mergedDict = outputDict.TryGetValue(localeName, out var existing)
                ? existing
                : new Dictionary<string, CommandStrings>();

            foreach (var file in Directory.GetFiles(localeDir, "*.yml"))
            {
                try
                {
                    var text = File.ReadAllText(file);
                    var moduleDict = deserializer.Deserialize<Dictionary<string, CommandStrings>>(text);

                    if (moduleDict == null)
                        continue;

                    // Merge into the locale dictionary
                    foreach (var (key, value) in moduleDict)
                    {
                        mergedDict[key] = value;
                    }

                    Log.Debug("Loaded {Count} commands from {File}", moduleDict.Count, Path.GetFileName(file));
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error loading {FileName} command strings: {ErrorMessage}", file, ex.Message);
                }
            }

            outputDict[localeName] = mergedDict;
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