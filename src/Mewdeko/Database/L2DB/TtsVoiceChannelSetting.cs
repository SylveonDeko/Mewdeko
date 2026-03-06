using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

[Table("TtsVoiceChannelSettings")]
public class TtsVoiceChannelSetting
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    [Column("GuildId")]
    public ulong GuildId { get; set; }

    [Column("VoiceChannelId")]
    public ulong VoiceChannelId { get; set; }

    [Column("Enabled")]
    public bool Enabled { get; set; }

    [Column("LinkedTextChannelId")]
    public ulong? LinkedTextChannelId { get; set; }

    [Column("AnnounceJoinLeave")]
    public bool AnnounceJoinLeave { get; set; }

    [Column("JoinFormat")]
    public string? JoinFormat { get; set; }

    [Column("LeaveFormat")]
    public string? LeaveFormat { get; set; }
}