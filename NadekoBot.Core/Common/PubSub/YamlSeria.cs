using NadekoBot.Common.Yml;
using NadekoBot.Core.Common.Configs;
using YamlDotNet.Serialization;

namespace NadekoBot.Core.Common
{
    public class YamlSeria : ISettingsSeria
    {
        private readonly ISerializer _serializer;
        private readonly IDeserializer _deserializer;

        public YamlSeria()
        {
            _serializer = Yaml.Serializer;
            _deserializer = Yaml.Deserializer;
        }
        
        public string Serialize<T>(T obj) 
            => _serializer.Serialize(obj);

        public T Deserialize<T>(string data) 
            => _deserializer.Deserialize<T>(data);
    }
}