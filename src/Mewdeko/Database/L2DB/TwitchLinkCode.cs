#nullable enable

using LinqToDB.Mapping;

namespace DataModel;

/// <summary>
///     Represents a short-lived Twitch-to-Discord account linking code.
/// </summary>
[Table("TwitchLinkCodes")]
public class TwitchLinkCode
{
    /// <summary>
    ///     Gets or sets the database row identifier.
    /// </summary>
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    /// <summary>
    ///     Gets or sets the Discord guild ID where the code can be claimed.
    /// </summary>
    [Column("GuildId")]
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the Twitch username that generated the code.
    /// </summary>
    [Column("TwitchUsername")]
    public string TwitchUsername { get; set; } = "";

    /// <summary>
    ///     Gets or sets the claim code shown to the Twitch user.
    /// </summary>
    [Column("Code")]
    public string Code { get; set; } = "";

    /// <summary>
    ///     Gets or sets when the claim code expires.
    /// </summary>
    [Column("ExpiresAt")]
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    ///     Gets or sets when the claim code was used.
    /// </summary>
    [Column("ClaimedAt")]
    public DateTime? ClaimedAt { get; set; }

    /// <summary>
    ///     Gets or sets when the claim code was created.
    /// </summary>
    [Column("DateAdded")]
    public DateTime? DateAdded { get; set; }
}