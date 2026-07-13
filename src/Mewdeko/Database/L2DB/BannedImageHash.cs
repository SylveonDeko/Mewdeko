#nullable enable

using LinqToDB.Mapping;

namespace DataModel;

/// <summary>
///     Represents a single blocked image, stored as a 64 bit perceptual hash rendered as 16 hex characters.
/// </summary>
[Table("BannedImageHashes")]
public class BannedImageHash
{
    /// <summary>
    ///     Gets or sets the database row identifier.
    /// </summary>
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    /// <summary>
    ///     Gets or sets the Discord guild ID this blocked hash belongs to.
    /// </summary>
    [Column("GuildId")]
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the PDQ hash of the blocked image's full frame, as 64 hex characters.
    /// </summary>
    [Column("Hash")]
    public string Hash { get; set; } = "";

    /// <summary>
    ///     Gets or sets the mirrored and cropped hashes of the same image, space separated. These are what let a flipped or
    ///     cropped re-upload still match. Empty when the entry was added from a bare hash rather than from an image.
    /// </summary>
    [Column("Variants")]
    public string? Variants { get; set; }

    /// <summary>
    ///     Gets or sets the PDQ quality score of the image, from 0 to 100. Below 50 the image is too flat for its hash to
    ///     discriminate reliably.
    /// </summary>
    [Column("Quality")]
    public int Quality { get; set; }

    /// <summary>
    ///     Gets or sets an optional label, for example "mrbeast crypto giveaway".
    /// </summary>
    [Column("Name")]
    public string? Name { get; set; }

    /// <summary>
    ///     Gets or sets the URL the hash was generated from, kept so the dashboard can preview the blocked image.
    /// </summary>
    [Column("SourceUrl")]
    public string? SourceUrl { get; set; }

    /// <summary>
    ///     Gets or sets the punishment action for this specific image, overriding the guild default when set.
    /// </summary>
    [Column("Action")]
    public int? Action { get; set; }

    /// <summary>
    ///     Gets or sets the punishment duration in minutes for this specific image, overriding the guild default when set.
    /// </summary>
    [Column("PunishDuration")]
    public int? PunishDuration { get; set; }

    /// <summary>
    ///     Gets or sets the role applied for this specific image when its action is AddRole.
    /// </summary>
    [Column("RoleId")]
    public ulong? RoleId { get; set; }

    /// <summary>
    ///     Gets or sets how many times a posted image has matched this hash.
    /// </summary>
    [Column("HitCount")]
    public int HitCount { get; set; }

    /// <summary>
    ///     Gets or sets the last time a posted image matched this hash.
    /// </summary>
    [Column("LastTriggeredAt")]
    public DateTime? LastTriggeredAt { get; set; }

    /// <summary>
    ///     Gets or sets the Discord user ID that added the hash.
    /// </summary>
    [Column("AddedBy")]
    public ulong? AddedBy { get; set; }

    /// <summary>
    ///     Gets or sets when the hash was added.
    /// </summary>
    [Column("DateAdded")]
    public DateTime? DateAdded { get; set; }
}