using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

[Table("LockdownJoinSettings")]
public class LockdownJoinSetting
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    [Column("GuildId")]
    public ulong GuildId { get; set; }

    [Column("PunishmentAction")]
    public int PunishmentAction { get; set; }

    [Column("DateAdded")]
    public DateTime? DateAdded { get; set; }

    [Column("DateUpdated")]
    public DateTime? DateUpdated { get; set; }
}