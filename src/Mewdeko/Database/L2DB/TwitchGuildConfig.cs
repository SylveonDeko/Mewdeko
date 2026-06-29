using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

[Table("TwitchGuildConfigs")]
public class TwitchGuildConfig
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    [Column("GuildId")]
    public ulong GuildId { get; set; }

    [Column("TwitchChannel")]
    public string TwitchChannel { get; set; } = "";

    [Column("CommandPrefix")]
    public string CommandPrefix { get; set; } = "!";

    [Column("Enabled")]
    public bool Enabled { get; set; } = true;

    [Column("GoLiveChannelId")]
    public ulong? GoLiveChannelId { get; set; }

    [Column("GoLiveMessage")]
    public string? GoLiveMessage { get; set; }

    [Column("Language")]
    public string? Language { get; set; }

    [Column("DateAdded")]
    public DateTime? DateAdded { get; set; }
}