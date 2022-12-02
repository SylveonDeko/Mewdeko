namespace Mewdeko.Coordinator.Shared;

public class JsonStatusObject
{
    public int? Pid { get; init; }
    public int GuildCount { get; init; }
    public ConnState ConnectionState { get; init; }
    public int UserCount { get; init; }
}