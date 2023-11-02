using Newtonsoft.Json;

namespace Mewdeko.Common.JsonConverters;

public class StringToIntConverter : JsonConverter
{
    public override bool CanConvert(Type objectType)
    {
        return objectType == typeof(string);
    }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        if (value is string strValue && int.TryParse(strValue, out var intValue))
        {
            writer.WriteValue(intValue);
        }
        else
        {
            writer.WriteNull();
        }
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        // Convert back to string during deserialization
        return reader.Value?.ToString();
    }
}