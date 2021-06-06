namespace Mewdeko.Core.Services.Database.Models
{
    public class MusicSettings : DbEntity
    {
        public int GuildConfigId { get; set; }
        public GuildConfig GuildConfig { get; set; }

        public bool SongAutoDelete { get; set; } = false;
        public ulong? MusicChannelId { get; set; } = null;
    }
    public class MusicPlayerSettings
    {
        /// <summary>
        /// Auto generated Id 
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Id of the guild
        /// </summary>
        public ulong GuildId { get; set; }

        /// <summary>
        /// Queue repeat type
        /// </summary>
        public PlayerRepeatType PlayerRepeat { get; set; } = PlayerRepeatType.Queue;

        /// <summary>
        /// Channel id the bot will always try to send track related messages to
        /// </summary>
        public ulong? MusicChannelId { get; set; } = null;

        /// <summary>
        /// Default volume player will be created with
        /// </summary>
        public int Volume { get; set; } = 100;

        /// <summary>
        /// Whether the bot should auto disconnect from the voice channel once the queue is done
        /// This only has effect if 
        /// </summary>
        public bool AutoDisconnect { get; set; } = false;
    }

    public enum PlayerRepeatType
    {
        None,
        Track,
        Queue
    }

}
