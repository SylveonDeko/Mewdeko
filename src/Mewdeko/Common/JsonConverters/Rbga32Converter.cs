using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using SkiaSharp;

namespace Mewdeko.Common.JsonConverters;

public class SkColorConverter : JsonConverter<SKColor>
{
    public override SKColor Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => SKColor.Parse(reader.GetString());

    public override void Write(Utf8JsonWriter writer, SKColor value, JsonSerializerOptions options) => writer.WriteStringValue(value.ToString());
}

public class CultureInfoConverter : JsonConverter<CultureInfo>
{
    public override CultureInfo Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => new(reader.GetString() ?? string.Empty);

    public override void Write(Utf8JsonWriter writer, CultureInfo value, JsonSerializerOptions options) => writer.WriteStringValue(value.Name);
}