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

    [Column("SubNotificationChannelId")]
    public ulong? SubNotificationChannelId { get; set; }

    [Column("SubNotificationMessage")]
    public string? SubNotificationMessage { get; set; }

    [Column("RaidNotificationChannelId")]
    public ulong? RaidNotificationChannelId { get; set; }

    [Column("RaidNotificationMessage")]
    public string? RaidNotificationMessage { get; set; }

    [Column("StreamRecapChannelId")]
    public ulong? StreamRecapChannelId { get; set; }

    [Column("StreamRecapEnabled")]
    public bool StreamRecapEnabled { get; set; }

    [Column("ScheduleMessage")]
    public string? ScheduleMessage { get; set; }

    [Column("SocialsMessage")]
    public string? SocialsMessage { get; set; }

    [Column("Language")]
    public string? Language { get; set; }

    [Column("TwitchUserId")]
    public string? TwitchUserId { get; set; }

    [Column("TwitchDisplayName")]
    public string? TwitchDisplayName { get; set; }

    [Column("UseEventSub")]
    public bool UseEventSub { get; set; } = true;

    [Column("AuthorizedByDiscordUserId")]
    public ulong? AuthorizedByDiscordUserId { get; set; }

    [Column("LastAuthorizedAt")]
    public DateTime? LastAuthorizedAt { get; set; }

    [Column("LastEventAt")]
    public DateTime? LastEventAt { get; set; }

    [Column("DateAdded")]
    public DateTime? DateAdded { get; set; }
}