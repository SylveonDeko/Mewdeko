using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mewdeko.Modules.Patreon.Common;

/// <summary>
///     Custom JSON converter for SocialConnection to handle both null and object values from Patreon API.
/// </summary>
public class SocialConnectionConverter : JsonConverter<SocialConnection?>
{
    /// <summary>
    ///     Reads and converts JSON to a SocialConnection, handling both null and object representations.
    /// </summary>
    /// <param name="reader">The Utf8JsonReader to read from.</param>
    /// <param name="typeToConvert">The type to convert.</param>
    /// <param name="options">Serialization options.</param>
    /// <returns>The deserialized SocialConnection instance or null.</returns>
    public override SocialConnection? Read(ref Utf8JsonReader reader, Type typeToConvert,
        JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return null;

            case JsonTokenType.String:
            {
                // Handle string values (API v1 compatibility or legacy)
                var stringValue = reader.GetString();
                return string.IsNullOrEmpty(stringValue)
                    ? null
                    : new SocialConnection
                    {
                        UserId = stringValue
                    };
            }

            case JsonTokenType.StartObject:
            {
                // Handle object values (API v2)
                using var doc = JsonDocument.ParseValue(ref reader);
                var root = doc.RootElement;

                var userId = root.TryGetProperty("user_id", out var userIdProp)
                    ? userIdProp.GetString()
                    : null;

                var url = root.TryGetProperty("url", out var urlProp) ? urlProp.GetString() : null;

                // If both are null, return null instead of an empty object
                if (userId == null && url == null)
                    return null;

                return new SocialConnection
                {
                    UserId = userId, Url = url
                };
            }

            default:
                throw new JsonException(
                    $"Unexpected token type '{reader.TokenType}' when deserializing SocialConnection");
        }
    }

    /// <summary>
    ///     Writes a SocialConnection object to JSON.
    /// </summary>
    /// <param name="writer">The Utf8JsonWriter to write to.</param>
    /// <param name="value">The SocialConnection value to serialize.</param>
    /// <param name="options">Serialization options.</param>
    public override void Write(Utf8JsonWriter writer, SocialConnection? value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartObject();

        if (value.UserId != null)
        {
            writer.WriteString("user_id", value.UserId);
        }

        if (value.Url != null)
        {
            writer.WriteString("url", value.Url);
        }

        writer.WriteEndObject();
    }
}