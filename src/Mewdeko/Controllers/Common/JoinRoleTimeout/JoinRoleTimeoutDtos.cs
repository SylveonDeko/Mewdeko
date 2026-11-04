using System.Text.Json.Serialization;
using DataModel;

namespace Mewdeko.Controllers.Common.JoinRoleTimeout;

/// <summary>
///     A guild's join role timeout settings, as read and as written.
/// </summary>
public class JoinRoleTimeoutSettingsDto
{
    /// <summary>
    ///     Whether the timeout is on.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    ///     Minutes a member has after joining to get a required role, from 1 to 43200.
    /// </summary>
    public int TimeoutMinutes { get; set; } = 1440;

    /// <summary>
    ///     The roles that count as verified. Any one of them is enough.
    /// </summary>
    public List<ulong> RequiredRoleIds { get; set; } = [];

    /// <summary>
    ///     The roles whose members are never acted on.
    /// </summary>
    public List<ulong> ExemptRoleIds { get; set; } = [];

    /// <summary>
    ///     Kick, Ban, AddRole, RemoveRoles or Timeout.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public JoinRoleTimeoutAction Action { get; set; }

    /// <summary>
    ///     The role given by AddRole.
    /// </summary>
    public ulong? ActionRoleId { get; set; }

    /// <summary>
    ///     Minutes before the timeout that a warning is sent, or null for no warning.
    /// </summary>
    public int? WarnBeforeMinutes { get; set; }

    /// <summary>
    ///     The warning message, or null for the default.
    /// </summary>
    public string? WarnMessage { get; set; }

    /// <summary>
    ///     The message sent when the action is taken, or null for the default.
    /// </summary>
    public string? ActionMessage { get; set; }

    /// <summary>
    ///     The channel actions are logged to.
    /// </summary>
    public ulong? LogChannelId { get; set; }

    /// <summary>
    ///     Whether current members without a required role start counting down when the timeout is turned on.
    /// </summary>
    public bool IncludeExistingMembers { get; set; }
}

/// <summary>
///     A member counting down toward the timeout.
/// </summary>
public class JoinRoleTimeoutPendingDto
{
    /// <summary>
    ///     The member's user ID.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    ///     When the countdown started.
    /// </summary>
    public DateTime JoinedAt { get; set; }

    /// <summary>
    ///     When the action will be taken.
    /// </summary>
    public DateTime DueAt { get; set; }

    /// <summary>
    ///     When the warning was sent, or null if it has not been.
    /// </summary>
    public DateTime? WarnedAt { get; set; }
}

/// <summary>
///     How many members are counting down and which are due next.
/// </summary>
public class JoinRoleTimeoutPendingResponse
{
    /// <summary>
    ///     How many members are counting down.
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    ///     The members due soonest, soonest first.
    /// </summary>
    public List<JoinRoleTimeoutPendingDto> Next { get; set; } = [];
}
