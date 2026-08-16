using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

[Table("ChannelAccessApplications")]
public class ChannelAccessApplication
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; } // integer

    [Column("ConfigId")]
    public int ConfigId { get; set; } // integer

    [Column("GuildId")]
    public ulong GuildId { get; set; } // numeric(20,0)

    [Column("UserId")]
    public ulong UserId { get; set; } // numeric(20,0)

    [Column("Status")]
    public int Status { get; set; } // integer

    [Column("MessageChannelId")]
    public ulong? MessageChannelId { get; set; } // numeric(20,0)

    [Column("MessageId")]
    public ulong? MessageId { get; set; } // numeric(20,0)

    [Column("ThreadId")]
    public ulong? ThreadId { get; set; } // numeric(20,0)

    [Column("ExpiresAt")]
    public DateTime? ExpiresAt { get; set; } // timestamp (6) without time zone

    [Column("ResolvedAt")]
    public DateTime? ResolvedAt { get; set; } // timestamp (6) without time zone

    [Column("ResolvedBy")]
    public ulong? ResolvedBy { get; set; } // numeric(20,0)

    [Column("ResolutionReason")]
    public string? ResolutionReason { get; set; } // text

    [Column("DateAdded")]
    public DateTime? DateAdded { get; set; } // timestamp (6) without time zone
}