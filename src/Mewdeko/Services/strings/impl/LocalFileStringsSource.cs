using System.IO;
using Newtonsoft.Json;
using Serilog;
using YamlDotNet.Serialization;

namespace Mewdeko.Services.strings.impl
{
    /// <summary>
    /// Loads strings from the local default file paths.
    /// </summary>
    public class LocalFileStringsSource : IStringsSource
    {
        private readonly string commandsPath;
        private readonly string responsesPath;

        /// <summary>
        /// Initializes a new instance of the <see cref="LocalFileStringsSource"/> class.
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
        /// Gets the response strings from the local files.
        /// </summary>
        /// <returns>A dictionary containing response strings for each locale.</returns>
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

        /// <summary>
        /// Gets the command strings from the local files.
        /// </summary>
        /// <returns>A dictionary containing command strings for each locale.</returns>
        public Dictionary<string, Dictionary<string, CommandStrings>> GetCommandStrings()
        {
            var deserializer = new DeserializerBuilder().Build();
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
}