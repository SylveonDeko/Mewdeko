using Mewdeko.Common.Yml;
using Mewdeko.Core.Common.Configs;
using YamlDotNet.Serialization;

namespace Mewdeko.Core.Common
{
    public class YamlSeria : ISettingsSeria
    {
        private readonly IDeserializer _deserializer;
        private readonly ISerializer _serializer;

        public YamlSeria()
        {
            _serializer = Yaml.Serializer;
            _deserializer = Yaml.Deserializer;
        }

        public string Serialize<T>(T obj)
        {
            return _serializer.Serialize(obj);
        }

        public T Deserialize<T>(string data)
        {
            return _deserializer.Deserialize<T>(data);
        }
    }
}