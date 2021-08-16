using System.Text.Json;
using Mewdeko.Core.Common.JsonConverters;

namespace Mewdeko.Core.Common
{
    public class JsonSeria : ISeria
    {
        private readonly JsonSerializerOptions serializerOptions = new()
        {
            Converters =
            {
                new Rgba32Converter(),
                new CultureInfoConverter()
            }
        };

        public byte[] Serialize<T>(T data)
        {
            return JsonSerializer.SerializeToUtf8Bytes(data, serializerOptions);
        }

        public T Deserialize<T>(byte[] data)
        {
            if (data is null)
                return default;


            return JsonSerializer.Deserialize<T>(data, serializerOptions);
        }
    }
}