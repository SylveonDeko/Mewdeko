#nullable enable

using LinqToDB.Mapping;

namespace DataModel;

/// <summary>
///     Represents the guild level configuration for anti-image-hash protection, which punishes users who post images
///     matching a blocklist of perceptual hashes.
/// </summary>
[Table("AntiImageHashSettings")]
public class AntiImageHashSetting
{
    /// <summary>
    ///     Gets or sets the database row identifier.
    /// </summary>
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    /// <summary>
    ///     Gets or sets the Discord guild ID these settings belong to.
    /// </summary>
    [Column("GuildId")]
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the default punishment action, as a
    ///     <see cref="Mewdeko.Modules.Administration.Common.PunishmentAction" />.
    ///     Individual blocked hashes may override this.
    /// </summary>
    [Column("Action")]
    public int Action { get; set; }

    /// <summary>
    ///     Gets or sets the default punishment duration in minutes, for actions that support one.
    /// </summary>
    [Column("PunishDuration")]
    public int PunishDuration { get; set; }

    /// <summary>
    ///     Gets or sets the role applied when the action is AddRole.
    /// </summary>
    [Column("RoleId")]
    public ulong? RoleId { get; set; }

    /// <summary>
    ///     Gets or sets the maximum hamming distance, out of 256 bits, between a posted image's PDQ hash and a blocked hash
    ///     for the image to count as a match. PDQ's standard "same image" threshold is 31.
    /// </summary>
    [Column("HashThreshold")]
    public int HashThreshold { get; set; }

    /// <summary>
    ///     Gets or sets whether a solid border is stripped from posted images before matching. This catches a blocked image
    ///     that has been wrapped in a frame to disguise it.
    /// </summary>
    [Column("CheckBorders")]
    public bool CheckBorders { get; set; }

    /// <summary>
    ///     Gets or sets whether the offending message is deleted.
    /// </summary>
    [Column("DeleteMessages")]
    public bool DeleteMessages { get; set; }

    /// <summary>
    ///     Gets or sets whether the user is DMed about the punishment.
    /// </summary>
    [Column("NotifyUser")]
    public bool NotifyUser { get; set; }

    /// <summary>
    ///     Gets or sets whether messages from bots are skipped.
    /// </summary>
    [Column("IgnoreBots")]
    public bool IgnoreBots { get; set; }

    /// <summary>
    ///     Gets or sets whether images inside embeds are checked in addition to attachments.
    /// </summary>
    [Column("CheckEmbeds")]
    public bool CheckEmbeds { get; set; }

    /// <summary>
    ///     Gets or sets the maximum image size, in megabytes, that will be downloaded and hashed.
    /// </summary>
    [Column("MaxImageSizeMb")]
    public int MaxImageSizeMb { get; set; }

    /// <summary>
    ///     Gets or sets whether the known scam images that ship with the bot are blocked alongside the guild's own list.
    /// </summary>
    [Column("UsePresetList")]
    public bool UsePresetList { get; set; }

    /// <summary>
    ///     Gets or sets the lifetime number of times a known scam image from the shipped list has been caught.
    /// </summary>
    [Column("PresetTriggers")]
    public int PresetTriggers { get; set; }

    /// <summary>
    ///     Gets or sets the lifetime number of times this protection has triggered in the guild.
    /// </summary>
    [Column("TotalTriggers")]
    public int TotalTriggers { get; set; }

    /// <summary>
    ///     Gets or sets when the protection was enabled.
    /// </summary>
    [Column("DateAdded")]
    public DateTime? DateAdded { get; set; }
}