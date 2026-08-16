using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

[Table("ChannelAccessBlacklists")]
public class ChannelAccessBlacklist
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; } // integer

    [Column("GuildId")]
    public ulong GuildId { get; set; } // numeric(20,0)

    [Column("ConfigId")]
    public int? ConfigId { get; set; } // integer

    [Column("UserId")]
    public ulong UserId { get; set; } // numeric(20,0)

    [Column("Reason")]
    public string? Reason { get; set; } // text

    [Column("AddedBy")]
    public ulong AddedBy { get; set; } // numeric(20,0)

    [Column("DateAdded")]
    public DateTime? DateAdded { get; set; } // timestamp (6) without time zone
}