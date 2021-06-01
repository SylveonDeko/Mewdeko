using System.Text.RegularExpressions;
using Mewdeko.Common.Yml;
using Mewdeko.Core.Common.Configs;
using YamlDotNet.Serialization;

namespace Mewdeko.Core.Common
{
    public class YamlSeria : IConfigSeria
    {
        private readonly ISerializer _serializer;
        private readonly IDeserializer _deserializer;

        private static readonly Regex CodePointRegex
            = new Regex(@"(\\U(?<code>[a-zA-Z0-9]{8})|\\u(?<code>[a-zA-Z0-9]{4})|\\x(?<code>[a-zA-Z0-9]{2}))",
                RegexOptions.Compiled);

        public YamlSeria()
        {
            _serializer = Yaml.Serializer;
            _deserializer = Yaml.Deserializer;
        }
        
        public string Serialize<T>(T obj)
        {
            var escapedOutput = _serializer.Serialize(obj);
            var output = CodePointRegex.Replace(escapedOutput, me =>
            {
                var str = me.Groups["code"].Value;
                var newString = YamlHelper.UnescapeUnicodeCodePoint(str);
                return newString;
            });
            return output;
        }

        public T Deserialize<T>(string data) 
            => _deserializer.Deserialize<T>(data);
    }
}