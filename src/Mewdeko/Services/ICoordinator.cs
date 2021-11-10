using System.Collections.Generic;

namespace Mewdeko.Services
{
    public interface ICoordinator
    {
        bool RestartBot();
        void Die();
        bool RestartShard(int shardId);
        IList<ShardStatus> GetAllShardStatuses();
        int GetGuildCount();
    }

    public class ShardStatus
    {
        public Discord.ConnectionState ConnectionState { get; set; }
        public DateTime LastUpdate { get; set; }
        public int ShardId { get; set; }
        public int GuildCount { get; set; }
    }

}