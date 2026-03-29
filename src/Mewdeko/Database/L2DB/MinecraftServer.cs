using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

[Table("MinecraftServers")]
public class MinecraftServer
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; } // integer

    [Column("GuildId")]
    public ulong GuildId { get; set; } // numeric(20,0)

    [Column("Name")]
    public string Name { get; set; } = null!; // text

    [Column("Address")]
    public string Address { get; set; } = null!; // text

    [Column("Port")]
    public int Port { get; set; } // integer

    [Column("ServerType")]
    public int ServerType { get; set; } // integer

    [Column("IsDefault")]
    public bool IsDefault { get; set; } // boolean

    [Column("WatchChannelId")]
    public ulong? WatchChannelId { get; set; } // numeric(20,0)

    [Column("WatchMessageId")]
    public ulong? WatchMessageId { get; set; } // numeric(20,0)

    [Column("QueryPort")]
    public int QueryPort { get; set; } // integer

    [Column("WatchInterval")]
    public int WatchInterval { get; set; } // integer

    [Column("WatchMode")]
    public int WatchMode { get; set; } // integer

    [Column("CustomEmbedTemplate")]
    public string? CustomEmbedTemplate { get; set; } // text

    [Column("LastOnline")]
    public bool? LastOnline { get; set; } // boolean

    [Column("LastStatusJson")]
    public string? LastStatusJson { get; set; } // text

    [Column("DateAdded")]
    public DateTime? DateAdded { get; set; } // timestamp (6) without time zone
}