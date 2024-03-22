using System.Text.Json;
using Mewdeko.Common.JsonConverters;

namespace Mewdeko.Common.PubSub;

/// <summary>
/// Class that implements the ISeria interface for JSON serialization and deserialization.
/// </summary>
public class JsonSeria : ISeria
{
    /// <summary>
    /// Options for the JSON serializer.
    /// </summary>
    private readonly JsonSerializerOptions serializerOptions = new()
    {
        Converters =
        {
            new SkColorConverter(), new CultureInfoConverter()
        }
    };

    /// <summary>
    /// Serializes the given data into a byte array using JSON format.
    /// </summary>
    /// <typeparam name="T">The type of data to serialize.</typeparam>
    /// <param name="data">The data to serialize.</param>
    /// <returns>A byte array representing the serialized data.</returns>
    public byte[] Serialize<T>(T data)
        => JsonSerializer.SerializeToUtf8Bytes(data, serializerOptions);

    /// <summary>
    /// Deserializes the given byte array into an object of type T using JSON format.
    /// </summary>
    /// <typeparam name="T">The type of object to deserialize into.</typeparam>
    /// <param name="data">The byte array to deserialize.</param>
    /// <returns>An object of type T representing the deserialized data, or null if deserialization fails.</returns>
    public T? Deserialize<T>(byte[]? data)
    {
        return data is null ? default : JsonSerializer.Deserialize<T>(data, serializerOptions);
    }
}