namespace Mewdeko.Services
{
    /// <summary>
    /// Interface for coordinating bot actions and retrieving shard information.
    /// </summary>
    public interface ICoordinator
    {
        /// <summary>
        /// Restarts the entire bot.
        /// </summary>
        /// <returns>True if the restart was successful, otherwise false.</returns>
        bool RestartBot();

        /// <summary>
        /// Shuts down the bot.
        /// </summary>
        void Die();

        /// <summary>
        /// Restarts the specified shard.
        /// </summary>
        /// <param name="shardId">The ID of the shard to restart.</param>
        /// <returns>True if the restart was successful, otherwise false.</returns>
        bool RestartShard(int shardId);

        /// <summary>
        /// Retrieves the status of all shards.
        /// </summary>
        /// <returns>A list of <see cref="ShardStatus"/> objects representing the status of each shard.</returns>
        IList<ShardStatus> GetAllShardStatuses();

        /// <summary>
        /// Retrieves the total number of guilds across all shards.
        /// </summary>
        /// <returns>The total number of guilds.</returns>
        int GetGuildCount();

        /// <summary>
        /// Retrieves the total number of users across all shards.
        /// </summary>
        /// <returns>The total number of users.</returns>
        int GetUserCount();
    }

    /// <summary>
    /// Represents the status of a shard.
    /// </summary>
    public class ShardStatus
    {
        /// <summary>
        /// Gets or sets the connection state of the shard.
        /// </summary>
        public ConnectionState ConnectionState { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of the last status update.
        /// </summary>
        public DateTime LastUpdate { get; set; }

        /// <summary>
        /// Gets or sets the ID of the shard.
        /// </summary>
        public int ShardId { get; set; }

        /// <summary>
        /// Gets or sets the number of guilds the shard is connected to.
        /// </summary>
        public int GuildCount { get; set; }

        /// <summary>
        /// Gets or sets the number of users the shard is aware of.
        /// </summary>
        public int UserCount { get; set; }
    }
}