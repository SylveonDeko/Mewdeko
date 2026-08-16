using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

[Table("ChannelAccessConfigs")]
public class ChannelAccessConfig
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; } // integer

    [Column("GuildId")]
    public ulong GuildId { get; set; } // numeric(20,0)

    [Column("ChannelId")]
    public ulong ChannelId { get; set; } // numeric(20,0)

    [Column("AccessRoleId")]
    public ulong? AccessRoleId { get; set; } // numeric(20,0)

    [Column("GrantMode")]
    public int GrantMode { get; set; } // integer

    [Column("ReviewChannelId")]
    public ulong? ReviewChannelId { get; set; } // numeric(20,0)

    [Column("LogChannelId")]
    public ulong? LogChannelId { get; set; } // numeric(20,0)

    [Column("PanelChannelId")]
    public ulong? PanelChannelId { get; set; } // numeric(20,0)

    [Column("PanelMessageId")]
    public ulong? PanelMessageId { get; set; } // numeric(20,0)

    [Column("VoterRoleId")]
    public ulong? VoterRoleId { get; set; } // numeric(20,0)

    [Column("PingRoleId")]
    public ulong? PingRoleId { get; set; } // numeric(20,0)

    [Column("Enabled")]
    public bool Enabled { get; set; } // boolean

    [Column("RequiredApprovals")]
    public int RequiredApprovals { get; set; } // integer

    [Column("RequiredDenials")]
    public int RequiredDenials { get; set; } // integer

    [Column("VoteDurationHours")]
    public int VoteDurationHours { get; set; } // integer

    [Column("OnExpiry")]
    public int OnExpiry { get; set; } // integer

    [Column("AllowAbstain")]
    public bool AllowAbstain { get; set; } // boolean

    [Column("AnonymousVotes")]
    public bool AnonymousVotes { get; set; } // boolean

    [Column("AnonymousApplicant")]
    public bool AnonymousApplicant { get; set; } // boolean

    [Column("MinAccountAgeDays")]
    public int MinAccountAgeDays { get; set; } // integer

    [Column("MinServerAgeDays")]
    public int MinServerAgeDays { get; set; } // integer

    [Column("ReapplyCooldownHours")]
    public int ReapplyCooldownHours { get; set; } // integer

    [Column("DmOnDecision")]
    public bool DmOnDecision { get; set; } // boolean

    [Column("CreatedBy")]
    public ulong CreatedBy { get; set; } // numeric(20,0)

    [Column("DateAdded")]
    public DateTime? DateAdded { get; set; } // timestamp (6) without time zone
}