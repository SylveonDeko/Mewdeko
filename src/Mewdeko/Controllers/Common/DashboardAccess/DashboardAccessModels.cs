using Mewdeko.Database.Enums;

namespace Mewdeko.Controllers.Common.DashboardAccess;

/// <summary>
///     Request to update the owner-controlled setting that delegates access-list management to
///     Discord administrators and members with Manage Guild.
/// </summary>
public sealed class UpdateDashboardAccessSettingsRequest
{
    /// <summary>
    ///     Whether Discord administrators and Manage Guild members may manage access grants.
    /// </summary>
    public bool AdminsCanManageAccess { get; set; }
}

/// <summary>
///     A user or role selected as an access-list manager.
/// </summary>
public class DashboardAccessTargetRequest
{
    /// <summary>
    ///     Whether the target is a Discord user or role.
    /// </summary>
    public DashboardAccessTargetType TargetType { get; set; }

    /// <summary>
    ///     The Discord user or role ID.
    /// </summary>
    public ulong TargetId { get; set; }
}

/// <summary>
///     The requested level for a single dashboard section.
/// </summary>
public sealed class DashboardAccessSectionRequest
{
    /// <summary>
    ///     Bot API controller name that identifies the dashboard section.
    /// </summary>
    public string Section { get; set; } = "";

    /// <summary>
    ///     View or Manage access level.
    /// </summary>
    public DashboardAccessLevel Level { get; set; }
}

/// <summary>
///     Request to grant a user or role access to selected dashboard sections.
/// </summary>
public sealed class UpsertDashboardAccessGrantRequest : DashboardAccessTargetRequest
{
    /// <summary>
    ///     Section-level permissions to replace the target's current grant with.
    /// </summary>
    public List<DashboardAccessSectionRequest> Sections { get; set; } = [];
}

/// <summary>
///     Response for the per-guild dashboard access settings.
/// </summary>
public sealed class DashboardAccessSettingsResponse
{
    /// <summary>
    ///     Whether Discord administrators and Manage Guild members may manage access grants.
    /// </summary>
    public bool AdminsCanManageAccess { get; init; }

    /// <summary>
    ///     Whether the requesting user may manage section grants.
    /// </summary>
    public bool CanManageAccess { get; init; }

    /// <summary>
    ///     Whether the requesting user is the guild owner and may change owner-only delegation settings.
    /// </summary>
    public bool IsGuildOwner { get; init; }
}

/// <summary>
///     Dashboard access manager response.
/// </summary>
public sealed class DashboardAccessManagerResponse
{
    /// <summary>Database identifier.</summary>
    public int Id { get; init; }

    /// <summary>Target type.</summary>
    public DashboardAccessTargetType TargetType { get; init; }

    /// <summary>Discord user or role ID.</summary>
    public ulong TargetId { get; init; }

    /// <summary>User who granted this manager role.</summary>
    public ulong GrantedBy { get; init; }

    /// <summary>When the manager role was added.</summary>
    public DateTime? DateAdded { get; init; }
}

/// <summary>
///     Dashboard access grant response with section levels.
/// </summary>
public sealed class DashboardAccessGrantResponse
{
    /// <summary>Database identifier.</summary>
    public int Id { get; init; }

    /// <summary>Target type.</summary>
    public DashboardAccessTargetType TargetType { get; init; }

    /// <summary>Discord user or role ID.</summary>
    public ulong TargetId { get; init; }

    /// <summary>Per-section access levels.</summary>
    public Dictionary<string, DashboardAccessLevel> Sections { get; init; } = [];
}