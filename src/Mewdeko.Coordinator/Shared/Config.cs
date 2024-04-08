namespace Mewdeko.Coordinator.Shared;

public readonly struct Config(
    int totalShards,
    int recheckIntervalMs,
    string shardStartCommand,
    string shardStartArgs,
    double unresponsiveSec,
    ulong clientId)
{
    public int TotalShards { get; init; } = totalShards;
    public int RecheckIntervalMs { get; init; } = recheckIntervalMs;
    public string ShardStartCommand { get; init; } = shardStartCommand;
    public string ShardStartArgs { get; init; } = shardStartArgs;
    public double UnresponsiveSec { get; init; } = unresponsiveSec;
    public ulong ClientId { get; init; } = clientId;
}