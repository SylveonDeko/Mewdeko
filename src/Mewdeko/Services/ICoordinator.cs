namespace Mewdeko.Services;

public interface ICoordinator
{
    bool RestartBot();
    void Die();
    bool RestartShard(int shardId);
    IList<ShardStatus> GetAllShardStatuses();
    int GetGuildCount();
    int GetUserCount();
}

public class ShardStatus
{
    public ConnectionState ConnectionState { get; set; }
    public DateTime LastUpdate { get; set; }
    public int ShardId { get; set; }
    public int GuildCount { get; set; }
    public int UserCount { get; set; }
}