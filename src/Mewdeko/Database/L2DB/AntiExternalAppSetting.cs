#nullable enable

using LinqToDB.Mapping;

namespace DataModel;

/// <summary>
///     Represents the guild level configuration for anti-external-app protection, which watches messages sent through apps
///     that members installed on their own account rather than on the server.
/// </summary>
[Table("AntiExternalAppSettings")]
public class AntiExternalAppSetting
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
    ///     Gets or sets the punishment action, as a <see cref="Mewdeko.Modules.Administration.Common.PunishmentAction" />.
    /// </summary>
    [Column("Action")]
    public int Action { get; set; }

    /// <summary>
    ///     Gets or sets the punishment duration in minutes, for actions that support one.
    /// </summary>
    [Column("PunishDuration")]
    public int PunishDuration { get; set; }

    /// <summary>
    ///     Gets or sets the role applied when the action is AddRole.
    /// </summary>
    [Column("RoleId")]
    public ulong? RoleId { get; set; }

    /// <summary>
    ///     Gets or sets how many users and roles one app message may mention before it counts as a violation. Zero turns
    ///     the mention check off. A ping of everyone or here always counts.
    /// </summary>
    [Column("MentionThreshold")]
    public int MentionThreshold { get; set; }

    /// <summary>
    ///     Gets or sets whether an app message carrying a Discord invite link counts as a violation.
    /// </summary>
    [Column("BlockInvites")]
    public bool BlockInvites { get; set; }

    /// <summary>
    ///     Gets or sets how many app messages one member may trigger within <see cref="TimeWindowSeconds" />. Zero turns
    ///     the rate check off.
    /// </summary>
    [Column("MaxMessages")]
    public int MaxMessages { get; set; }

    /// <summary>
    ///     Gets or sets the length of the window for <see cref="MaxMessages" />, in seconds.
    /// </summary>
    [Column("TimeWindowSeconds")]
    public int TimeWindowSeconds { get; set; }

    /// <summary>
    ///     Gets or sets whether the offending app message is deleted.
    /// </summary>
    [Column("DeleteMessages")]
    public bool DeleteMessages { get; set; }

    /// <summary>
    ///     Gets or sets whether the member who ran the app is told by DM.
    /// </summary>
    [Column("NotifyUser")]
    public bool NotifyUser { get; set; }

    /// <summary>
    ///     Gets or sets the lifetime number of times this protection has triggered.
    /// </summary>
    [Column("TotalTriggers")]
    public int TotalTriggers { get; set; }

    /// <summary>
    ///     Gets or sets when these settings were first created.
    /// </summary>
    [Column("DateAdded")]
    public DateTime DateAdded { get; set; }
}
