using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

[Table("TwitchAccountLinks")]
public class TwitchAccountLink
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    [Column("GuildId")]
    public ulong GuildId { get; set; }

    [Column("DiscordUserId")]
    public ulong DiscordUserId { get; set; }

    [Column("TwitchUsername")]
    public string TwitchUsername { get; set; } = "";

    [Column("DateAdded")]
    public DateTime? DateAdded { get; set; }
}