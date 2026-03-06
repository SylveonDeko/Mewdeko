using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel
{
    [Table("MusicPlayerSettings")]
    public class MusicPlayerSetting
    {
        [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
        public int Id { get; set; }

        [Column("GuildId")]
        public ulong GuildId { get; set; }

        [Column("PlayerRepeat")]
        public int PlayerRepeat { get; set; }

        [Column("MusicChannelId")]
        public ulong? MusicChannelId { get; set; }

        [Column("Volume")]
        public int Volume { get; set; }

        [Column("AutoDisconnect")]
        public int AutoDisconnect { get; set; }

        [Column("AutoPlay")]
        public int AutoPlay { get; set; }

        [Column("DjRoleId")]
        public ulong? DjRoleId { get; set; }

        [Column("VoteSkipEnabled")]
        public bool VoteSkipEnabled { get; set; }

        [Column("VoteSkipThreshold")]
        public int VoteSkipThreshold { get; set; }

        [Column("TtsChannelId")]
        public ulong? TtsChannelId { get; set; }

        [Column("TtsVolume")]
        public int TtsVolume { get; set; }

        [Column("TtsDefaultVoice")]
        public string? TtsDefaultVoice { get; set; }

        [Column("TtsSpeed")]
        public float TtsSpeed { get; set; }

        [Column("TtsReplyContext")]
        public bool TtsReplyContext { get; set; }

        [Column("TtsAttachmentNarration")]
        public bool TtsAttachmentNarration { get; set; }

        [Column("TtsMaxQueueSize")]
        public int TtsMaxQueueSize { get; set; }

        [Column("TtsConsecutiveGrouping")]
        public bool TtsConsecutiveGrouping { get; set; }

        [Column("TtsRoleId")]
        public ulong? TtsRoleId { get; set; }
    }
}