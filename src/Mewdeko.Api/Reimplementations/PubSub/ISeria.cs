namespace Mewdeko.Api.Reimplementations.PubSub;

public interface ISeria
{
    byte[] Serialize<T>(T data);
    T? Deserialize<T>(byte[] data);
}