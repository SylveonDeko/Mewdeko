namespace Mewdeko.Common.PubSub;

/// <summary>
///     Interface for a serialization service.
/// </summary>
public interface ISeria
{
    /// <summary>
    ///     Serializes the given data into a byte array.
    /// </summary>
    /// <typeparam name="T">The type of data to serialize.</typeparam>
    /// <param name="data">The data to serialize.</param>
    /// <returns>A byte array representing the serialized data.</returns>
    byte[] Serialize<T>(T data);

    /// <summary>
    ///     Deserializes the given byte array into an object of type T.
    /// </summary>
    /// <typeparam name="T">The type of object to deserialize into.</typeparam>
    /// <param name="data">The byte array to deserialize.</param>
    /// <returns>An object of type T representing the deserialized data, or null if deserialization fails.</returns>
    T? Deserialize<T>(byte[] data);
}