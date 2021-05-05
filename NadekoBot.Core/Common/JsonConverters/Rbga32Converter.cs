using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using SixLabors.ImageSharp.PixelFormats;

namespace NadekoBot.Core.Common.JsonConverters
{
    public class Rgba32Converter : JsonConverter<Rgba32>
    {
        public override Rgba32 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return Rgba32.ParseHex(reader.GetString());
        }

        public override void Write(Utf8JsonWriter writer, Rgba32 value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToHex());
        }
    }
    
    public class CultureInfoConverter : JsonConverter<CultureInfo>
    {
        public override CultureInfo Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return new CultureInfo(reader.GetString());
        }

        public override void Write(Utf8JsonWriter writer, CultureInfo value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.Name);
        }
    }
}