using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

[Table("MinecraftServerSnapshots")]
public class MinecraftServerSnapshot
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; } // integer

    [Column("ServerId")]
    public int ServerId { get; set; } // integer

    [Column("IsOnline")]
    public bool IsOnline { get; set; } // boolean

    [Column("PlayersOnline")]
    public int PlayersOnline { get; set; } // integer

    [Column("PlayersMax")]
    public int PlayersMax { get; set; } // integer

    [Column("Latency")]
    public int Latency { get; set; } // integer

    [Column("Version")]
    public string? Version { get; set; } // text

    [Column("Timestamp")]
    public DateTime Timestamp { get; set; } // timestamp (6) without time zone
}