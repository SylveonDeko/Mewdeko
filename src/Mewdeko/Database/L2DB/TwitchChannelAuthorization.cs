using LinqToDB.Mapping;

#pragma warning disable 1591
#nullable enable

namespace DataModel;

[Table("TwitchChannelAuthorizations")]
public class TwitchChannelAuthorization
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    [Column("GuildId")]
    public ulong GuildId { get; set; }

    [Column("TwitchUserId")]
    public string TwitchUserId { get; set; } = "";

    [Column("TwitchUsername")]
    public string TwitchUsername { get; set; } = "";

    [Column("DisplayName")]
    public string DisplayName { get; set; } = "";

    [Column("AccessToken")]
    public string AccessToken { get; set; } = "";

    [Column("RefreshToken")]
    public string RefreshToken { get; set; } = "";

    [Column("Scopes")]
    public string Scopes { get; set; } = "";

    [Column("TokenExpiresAt")]
    public DateTime? TokenExpiresAt { get; set; }

    [Column("AuthorizedByDiscordUserId")]
    public ulong? AuthorizedByDiscordUserId { get; set; }

    [Column("DateAdded")]
    public DateTime? DateAdded { get; set; }

    [Column("LastRefreshedAt")]
    public DateTime? LastRefreshedAt { get; set; }
}