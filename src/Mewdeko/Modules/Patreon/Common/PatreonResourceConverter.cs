using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mewdeko.Modules.Patreon.Common;

/// <summary>
///     Custom JSON converter for polymorphic PatreonResource deserialization.
///     Maps the JSON:API "type" field to the appropriate concrete class.
/// </summary>
public class PatreonResourceConverter : JsonConverter<PatreonResource>
{
    /// <summary>
    ///     Reads and converts JSON to a concrete PatreonResource type based on the "type" field.
    /// </summary>
    /// <param name="reader">The Utf8JsonReader to read from.</param>
    /// <param name="typeToConvert">The type to convert.</param>
    /// <param name="options">Serialization options.</param>
    /// <returns>The deserialized PatreonResource instance.</returns>
    public override PatreonResource? Read(ref Utf8JsonReader reader, Type typeToConvert,
        JsonSerializerOptions options)
    {
        // We need to read the entire object to determine its type
        using var jsonDoc = JsonDocument.ParseValue(ref reader);
        var root = jsonDoc.RootElement;

        // Get the "type" property to determine which concrete class to deserialize
        if (!root.TryGetProperty("type", out var typeProperty))
        {
            throw new JsonException("PatreonResource object missing required 'type' property");
        }

        var resourceType = typeProperty.GetString();

        // Map the type string to the appropriate concrete class
        var json = root.GetRawText();
        return resourceType switch
        {
            "user" => JsonSerializer.Deserialize<User>(json, options),
            "member" => JsonSerializer.Deserialize<Member>(json, options),
            "campaign" => JsonSerializer.Deserialize<Campaign>(json, options),
            "tier" => JsonSerializer.Deserialize<Tier>(json, options),
            "benefit" => JsonSerializer.Deserialize<Benefit>(json, options),
            "address" => JsonSerializer.Deserialize<Address>(json, options),
            "post" => JsonSerializer.Deserialize<Post>(json, options),
            "deliverable" => JsonSerializer.Deserialize<Deliverable>(json, options),
            "media" => JsonSerializer.Deserialize<Media>(json, options),
            "webhook" => JsonSerializer.Deserialize<Webhook>(json, options),
            _ => throw new JsonException($"Unknown PatreonResource type: '{resourceType}'")
        };
    }

    /// <summary>
    ///     Writes a PatreonResource object to JSON.
    /// </summary>
    /// <param name="writer">The Utf8JsonWriter to write to.</param>
    /// <param name="value">The PatreonResource value to serialize.</param>
    /// <param name="options">Serialization options.</param>
    public override void Write(Utf8JsonWriter writer, PatreonResource value, JsonSerializerOptions options)
    {
        // Serialize the concrete type
        JsonSerializer.Serialize(writer, value, value.GetType(), options);
    }
}