using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

[Table("StatChannels")]
public class StatChannel
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    [Column("GuildId")]
    public ulong GuildId { get; set; }

    [Column("ChannelId")]
    public ulong ChannelId { get; set; }

    [Column("StatType")]
    public int StatType { get; set; }

    [Column("Template")]
    public string Template { get; set; } = "{count}";

    [Column("RoleId")]
    public ulong? RoleId { get; set; }

    [Column("CountdownDate")]
    public DateTime? CountdownDate { get; set; }

    [Column("GoalTarget")]
    public int GoalTarget { get; set; }

    [Column("DateAdded")]
    public DateTime? DateAdded { get; set; }
}