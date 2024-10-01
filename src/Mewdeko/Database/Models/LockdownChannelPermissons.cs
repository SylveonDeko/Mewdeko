using System.ComponentModel.DataAnnotations;

namespace Mewdeko.Database.Models;

/// <summary>
///     Represents the original permission overrides for a specific role in a channel during lockdown.
/// </summary>
public class LockdownChannelPermissions
{
    /// <summary>
    ///     Gets or sets the unique identifier for this permission entry.
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    ///     Gets or sets the guild ID associated with this permission entry.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the channel ID associated with this permission entry.
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     Gets or sets the role ID associated with this permission entry.
    /// </summary>
    public ulong TargetId { get; set; }

    /// <summary>
    ///     The target type for the permission (user/role)
    /// </summary>
    public PermissionTarget TargetType { get; set; }

    /// <summary>
    ///     Raw allow perms
    /// </summary>
    public ulong AllowPermissions { get; set; }

    /// <summary>
    ///     Raw deny perms
    /// </summary>
    public ulong DenyPermissions { get; set; }
}