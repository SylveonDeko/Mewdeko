using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mewdeko.Votes.Common.JsonConverters;

public class CultureInfoConverter : JsonConverter<CultureInfo>
{
    public override CultureInfo Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => new(reader.GetString() ?? string.Empty);

    public override void Write(Utf8JsonWriter writer, CultureInfo value, JsonSerializerOptions options) => writer.WriteStringValue(value.Name);
}