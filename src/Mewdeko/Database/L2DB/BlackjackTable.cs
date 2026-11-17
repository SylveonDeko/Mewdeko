using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

[Table("BlackjackTables")]
public class BlackjackTableRow
{
    [Column("GuildId", IsPrimaryKey = true, PrimaryKeyOrder = 0)]
    public ulong GuildId { get; set; }

    [Column("ChannelId", IsPrimaryKey = true, PrimaryKeyOrder = 1)]
    public ulong ChannelId { get; set; }

    [Column("MessageId")]
    public ulong MessageId { get; set; }

    [Column("OpenerId")]
    public ulong OpenerId { get; set; }

    [Column("Phase")]
    public short Phase { get; set; }

    [Column("Seats")]
    public string Seats { get; set; } = "[]";

    [Column("DealerCards")]
    public string DealerCards { get; set; } = "";

    [Column("Turn")]
    public int Turn { get; set; }

    [Column("Version")]
    public int Version { get; set; }

    [Column("StartedAt")]
    public DateTime StartedAt { get; set; }

    [Column("LastActionAt")]
    public DateTime LastActionAt { get; set; }
}

[Table("BlackjackSeats")]
public class BlackjackSeatRow
{
    [Column("GuildId", IsPrimaryKey = true, PrimaryKeyOrder = 0)]
    public ulong GuildId { get; set; }

    [Column("UserId", IsPrimaryKey = true, PrimaryKeyOrder = 1)]
    public ulong UserId { get; set; }

    [Column("ChannelId")]
    public ulong ChannelId { get; set; }
}
