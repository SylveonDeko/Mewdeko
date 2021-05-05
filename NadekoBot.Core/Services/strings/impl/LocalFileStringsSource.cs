using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using YamlDotNet.Serialization;

namespace NadekoBot.Core.Services
{
    /// <summary>
    /// Loads strings from the local default filepath <see cref="_responsesPath"/>
    /// </summary>
    public class LocalFileStringsSource : IStringsSource
    {
        private readonly string _responsesPath = "data/strings/responses";
        private readonly string _commandsPath = "data/strings/commands";

        public LocalFileStringsSource(string responsesPath = "data/strings/responses",
            string commandsPath = "data/strings/commands")
        {
            _responsesPath = responsesPath;
            _commandsPath = commandsPath;
        }
        
        public Dictionary<string, Dictionary<string, string>> GetResponseStrings()
        {
            var outputDict = new Dictionary<string, Dictionary<string, string>>();
            foreach (var file in Directory.GetFiles(_responsesPath))
            {
                var langDict = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(file));
                var localeName = GetLocaleName(file);
                outputDict[localeName] = langDict;
            }

            return outputDict;
        }

        public Dictionary<string, Dictionary<string, CommandStrings>> GetCommandStrings()
        {
            var deserializer = new DeserializerBuilder()
                .Build();
            
            var outputDict = new Dictionary<string, Dictionary<string, CommandStrings>>();
            foreach (var file in Directory.GetFiles(_commandsPath))
            {
                var text = File.ReadAllText(file);
                var langDict = deserializer.Deserialize<Dictionary<string, CommandStrings>>(text);
                var localeName = GetLocaleName(file);
                outputDict[localeName] = langDict;
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