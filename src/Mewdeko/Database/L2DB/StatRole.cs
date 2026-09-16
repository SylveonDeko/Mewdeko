using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

/// <summary>
///     A role that is granted and taken away on a schedule based on member activity.
/// </summary>
[Table("StatRoles")]
public class StatRole
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    [Column("GuildId")]
    public ulong GuildId { get; set; }

    /// <summary>
    ///     The role that is granted and removed.
    /// </summary>
    [Column("RoleId")]
    public ulong RoleId { get; set; }

    /// <summary>
    ///     A display name for lists.
    /// </summary>
    [Column("Name")]
    public string Name { get; set; } = "";

    [Column("Enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>
    ///     The <see cref="Mewdeko.Modules.StatRoles.Common.StatRoleStat" /> the condition measures.
    /// </summary>
    [Column("StatType")]
    public int StatType { get; set; }

    /// <summary>
    ///     The <see cref="Mewdeko.Modules.StatRoles.Common.StatRoleLimit" /> that decides who qualifies.
    /// </summary>
    [Column("LimitType")]
    public int LimitType { get; set; }

    /// <summary>
    ///     The least a member must have to qualify (threshold), or the per day requirement (daily streak).
    /// </summary>
    [Column("Minimum")]
    public long Minimum { get; set; } = 1;

    /// <summary>
    ///     The most a member may have and still qualify, or null for no ceiling.
    /// </summary>
    [Column("Maximum")]
    public long? Maximum { get; set; }

    /// <summary>
    ///     The window in days the stat is measured over, or 0 for all time.
    /// </summary>
    [Column("LookbackDays")]
    public int LookbackDays { get; set; }

    /// <summary>
    ///     The best rank or percentile that qualifies, inclusive.
    /// </summary>
    [Column("TopStart")]
    public int TopStart { get; set; } = 1;

    /// <summary>
    ///     The worst rank or percentile that qualifies, inclusive.
    /// </summary>
    [Column("TopEnd")]
    public int TopEnd { get; set; } = 10;

    /// <summary>
    ///     How many days inside the window must meet the per day requirement (daily streak).
    /// </summary>
    [Column("RequiredDays")]
    public int RequiredDays { get; set; } = 1;

    /// <summary>
    ///     When true the role is never removed once earned.
    /// </summary>
    [Column("Permanent")]
    public bool Permanent { get; set; }

    /// <summary>
    ///     When true members who do NOT meet the condition get the role, for inactivity roles.
    /// </summary>
    [Column("Invert")]
    public bool Invert { get; set; }

    [Column("ApplyToBots")]
    public bool ApplyToBots { get; set; }

    /// <summary>
    ///     Stat roles sharing a group only keep the one with the highest minimum a member qualifies for.
    /// </summary>
    [Column("GroupName")]
    public string? GroupName { get; set; }

    /// <summary>
    ///     JSON array of channel IDs the stat is limited to, or null for every channel.
    /// </summary>
    [Column("ChannelFilter")]
    public string? ChannelFilter { get; set; }

    /// <summary>
    ///     For the activity stat: the activity name to measure, or null for time in any activity.
    /// </summary>
    [Column("ActivityName")]
    public string? ActivityName { get; set; }

    /// <summary>
    ///     JSON array of role IDs; when set a member needs at least one to be considered.
    /// </summary>
    [Column("RoleWhitelist")]
    public string? RoleWhitelist { get; set; }

    /// <summary>
    ///     JSON array of role IDs; members holding any are never considered.
    /// </summary>
    [Column("RoleBlacklist")]
    public string? RoleBlacklist { get; set; }

    /// <summary>
    ///     JSON array of user IDs the stat role never touches.
    /// </summary>
    [Column("IgnoredUsers")]
    public string? IgnoredUsers { get; set; }

    /// <summary>
    ///     A channel that receives a message when the role is granted or removed, or null.
    /// </summary>
    [Column("NotifyChannelId")]
    public ulong? NotifyChannelId { get; set; }

    /// <summary>
    ///     Whether the member is sent a DM when the role is granted or removed.
    /// </summary>
    [Column("NotifyDm")]
    public bool NotifyDm { get; set; }

    /// <summary>
    ///     The notification template, supporting %user%, %role%, %action%, %value% and server placeholders.
    /// </summary>
    [Column("NotifyMessage")]
    public string? NotifyMessage { get; set; }

    /// <summary>
    ///     How often the role is evaluated, in minutes.
    /// </summary>
    [Column("IntervalMinutes")]
    public int IntervalMinutes { get; set; } = 180;

    [Column("LastRunAt")]
    public DateTime? LastRunAt { get; set; }

    [Column("DateAdded")]
    public DateTime? DateAdded { get; set; }
}