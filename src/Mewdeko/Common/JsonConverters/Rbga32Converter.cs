using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using SkiaSharp;

namespace Mewdeko.Common.JsonConverters;

/// <summary>
/// Provides a converter for the SKColor type for JSON operations.
/// </summary>
public class SkColorConverter : JsonConverter<SKColor>
{
    /// <summary>
    /// Reads and converts the JSON to type SKColor.
    /// </summary>
    /// <param name="reader">The Utf8JsonReader which will read the SKColor from the JSON.</param>
    /// <param name="typeToConvert">The type of object to convert.</param>
    /// <param name="options">The JsonSerializerOptions to use.</param>
    /// <returns>A SKColor that represents the converted JSON.</returns>
    public override SKColor Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        SKColor.Parse(reader.GetString());

    /// <summary>
    /// Writes a SKColor value to JSON.
    /// </summary>
    /// <param name="writer">The Utf8JsonWriter to write to.</param>
    /// <param name="value">The SKColor value to convert to JSON.</param>
    /// <param name="options">The JsonSerializerOptions to use.</param>
    public override void Write(Utf8JsonWriter writer, SKColor value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());
}

/// <summary>
/// Provides a converter for the CultureInfo type for JSON operations.
/// </summary>
public class CultureInfoConverter : JsonConverter<CultureInfo>
{
    /// <summary>
    /// Reads and converts the JSON to type CultureInfo.
    /// </summary>
    /// <param name="reader">The Utf8JsonReader which will read the CultureInfo from the JSON.</param>
    /// <param name="typeToConvert">The type of object to convert.</param>
    /// <param name="options">The JsonSerializerOptions to use.</param>
    /// <returns>A CultureInfo that represents the converted JSON.</returns>
    public override CultureInfo Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(reader.GetString() ?? string.Empty);

    /// <summary>
    /// Writes a CultureInfo value to JSON.
    /// </summary>
    /// <param name="writer">The Utf8JsonWriter to write to.</param>
    /// <param name="value">The CultureInfo value to convert to JSON.</param>
    /// <param name="options">The JsonSerializerOptions to use.</param>
    public override void Write(Utf8JsonWriter writer, CultureInfo value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Name);
}