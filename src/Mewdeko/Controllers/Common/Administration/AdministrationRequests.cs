using Mewdeko.Modules.Administration.Common;

namespace Mewdeko.Controllers.Common.Administration;

/// <summary>
///     Request model for setting channel state in delete message on command
/// </summary>
public class SetChannelStateRequest
{
    /// <summary>
    ///     The ID of the channel to set state for
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     The state to set (enable, disable, inherit)
    /// </summary>
    public string State { get; set; } = "";
}

/// <summary>
///     Request model for voice channel role management
/// </summary>
public class VoiceChannelRoleRequest
{
    /// <summary>
    ///     The ID of the voice channel
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     The ID of the role to assign when users join the voice channel
    /// </summary>
    public ulong RoleId { get; set; }
}

/// <summary>
///     Request model for setting self-assignable role group
/// </summary>
public class SetGroupRequest
{
    /// <summary>
    ///     The group number for the self-assignable roles
    /// </summary>
    public int Group { get; set; }

    /// <summary>
    ///     The display name for the group
    /// </summary>
    public string? Name { get; set; }
}

/// <summary>
///     Request model for adding reaction roles
/// </summary>
public class AddReactionRolesRequest
{
    /// <summary>
    ///     The ID of the message to add reaction roles to (null for new message)
    /// </summary>
    public ulong? MessageId { get; set; }

    /// <summary>
    ///     Whether the reaction roles are mutually exclusive
    /// </summary>
    public bool Exclusive { get; set; }

    /// <summary>
    ///     The list of reaction roles to add
    /// </summary>
    public List<ReactionRoleData> Roles { get; set; } = new();
}

/// <summary>
///     Reaction role data for requests
/// </summary>
public class ReactionRoleData
{
    /// <summary>
    ///     The name or Unicode representation of the emote
    /// </summary>
    public string EmoteName { get; set; } = "";

    /// <summary>
    ///     The ID of the role to assign when the reaction is added
    /// </summary>
    public ulong RoleId { get; set; }
}

/// <summary>
///     Request model for setting guild timezone
/// </summary>
public class SetTimezoneRequest
{
    /// <summary>
    ///     The timezone ID (e.g., "America/New_York", "UTC")
    /// </summary>
    public string TimezoneId { get; set; } = "";
}

/// <summary>
///     Request model for permission overrides
/// </summary>
public class PermissionOverrideRequest
{
    /// <summary>
    ///     The command name to override permissions for
    /// </summary>
    public string Command { get; set; } = "";

    /// <summary>
    ///     The required permission level (Discord permission name)
    /// </summary>
    public string Permission { get; set; } = "";
}

/// <summary>
///     Request model for toggling game voice channel
/// </summary>
public class ToggleGameVoiceChannelRequest
{
    /// <summary>
    ///     The ID of the voice channel to toggle as game voice channel
    /// </summary>
    public ulong ChannelId { get; set; }
}

/// <summary>
///     Request model for server recovery setup
/// </summary>
public class ServerRecoveryRequest
{
    /// <summary>
    ///     The recovery key for server restoration
    /// </summary>
    public string RecoveryKey { get; set; } = "";

    /// <summary>
    ///     The two-factor authentication key for additional security
    /// </summary>
    public string TwoFactorKey { get; set; } = "";
}

/// <summary>
///     Request model for setting ban message
/// </summary>
public class SetBanMessageRequest
{
    /// <summary>
    ///     The message to send to users when they are banned
    /// </summary>
    public string Message { get; set; } = "";
}

/// <summary>
///     Request model for mass ban operation
/// </summary>
public class MassBanRequest
{
    /// <summary>
    ///     The list of user IDs to ban
    /// </summary>
    public List<ulong> UserIds { get; set; } = new();

    /// <summary>
    ///     The reason for the mass ban operation
    /// </summary>
    public string? Reason { get; set; }
}

/// <summary>
///     Request model for mass rename operation
/// </summary>
public class MassRenameRequest
{
    /// <summary>
    ///     The naming pattern for mass rename (use {username} placeholder)
    /// </summary>
    public string Pattern { get; set; } = "";
}

/// <summary>
///     Request model for pruning users
/// </summary>
public class PruneRequest
{
    /// <summary>
    ///     The number of days of inactivity to prune users for
    /// </summary>
    public int Days { get; set; }

    /// <summary>
    ///     The reason for the prune operation
    /// </summary>
    public string? Reason { get; set; }
}

/// <summary>
///     Request model for pruning messages to a specific message
/// </summary>
public class PruneToMessageRequest
{
    /// <summary>
    ///     The ID of the channel to prune messages from
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     The ID of the message to prune up to (exclusive)
    /// </summary>
    public ulong MessageId { get; set; }
}

/// <summary>
///     Request model for starting anti-raid protection
/// </summary>
public class AntiRaidRequest
{
    /// <summary>
    ///     The number of users joining within the time window to trigger protection
    /// </summary>
    public int UserThreshold { get; set; }

    /// <summary>
    ///     The time window in seconds for monitoring user joins
    /// </summary>
    public int Seconds { get; set; }

    /// <summary>
    ///     The action to take when raid is detected (0=None, 1=Warn, 2=Mute, 3=Kick, 4=Ban, 5=Softban)
    /// </summary>
    public PunishmentAction Action { get; set; }

    /// <summary>
    ///     The duration in minutes for the punishment (0 for permanent)
    /// </summary>
    public int MinutesDuration { get; set; }
}

/// <summary>
///     Request model for starting anti-spam protection
/// </summary>
public class AntiSpamRequest
{
    /// <summary>
    ///     The number of messages within the time window to trigger protection
    /// </summary>
    public int MessageCount { get; set; }

    /// <summary>
    ///     The action to take when spam is detected (0=None, 1=Warn, 2=Mute, 3=Kick, 4=Ban, 5=Softban)
    /// </summary>
    public PunishmentAction Action { get; set; }

    /// <summary>
    ///     The duration in minutes for the punishment (0 for permanent)
    /// </summary>
    public int PunishDurationMinutes { get; set; }

    /// <summary>
    ///     The ID of the role to assign instead of other punishment (0 for none)
    /// </summary>
    public ulong RoleId { get; set; }
}

/// <summary>
///     Request model for adding self-assignable role with group
/// </summary>
public class AddSelfAssignableRoleRequest
{
    /// <summary>
    ///     The group number for the self-assignable role (0 for no group)
    /// </summary>
    public int Group { get; set; }
}