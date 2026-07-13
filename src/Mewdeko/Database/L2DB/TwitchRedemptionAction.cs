#nullable enable

using LinqToDB.Mapping;

namespace DataModel;

/// <summary>
///     Represents an action triggered by a Twitch channel point redemption.
/// </summary>
[Table("TwitchRedemptionActions")]
public class TwitchRedemptionAction
{
    /// <summary>
    ///     Gets or sets the database row identifier.
    /// </summary>
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    /// <summary>
    ///     Gets or sets the Discord guild ID that owns this action.
    /// </summary>
    [Column("GuildId")]
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the Twitch reward title matched by this action.
    /// </summary>
    [Column("RewardTitle")]
    public string RewardTitle { get; set; } = "";

    /// <summary>
    ///     Gets or sets the optional Twitch chat response.
    /// </summary>
    [Column("TwitchResponse")]
    public string? TwitchResponse { get; set; }

    /// <summary>
    ///     Gets or sets the optional Discord channel ID for action posts.
    /// </summary>
    [Column("DiscordChannelId")]
    public ulong? DiscordChannelId { get; set; }

    /// <summary>
    ///     Gets or sets the optional Discord message template.
    /// </summary>
    [Column("DiscordMessage")]
    public string? DiscordMessage { get; set; }

    /// <summary>
    ///     Gets or sets whether this action is active.
    /// </summary>
    [Column("Enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>
    ///     Gets or sets when the action was created.
    /// </summary>
    [Column("DateAdded")]
    public DateTime? DateAdded { get; set; }

    /// <summary>
    ///     Gets or sets when the action was last changed.
    /// </summary>
    [Column("LastUpdatedAt")]
    public DateTime? LastUpdatedAt { get; set; }
}