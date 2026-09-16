namespace Mewdeko.Modules.StatRoles.Common;

/// <summary>
///     One member's outcome in a stat role evaluation.
/// </summary>
/// <param name="UserId">The member.</param>
/// <param name="Value">The measured value, in the stat's unit.</param>
/// <param name="Rank">The member's rank among ranked members, or null when they have no value.</param>
public record StatRoleMember(ulong UserId, long Value, int? Rank);

/// <summary>
///     The result of evaluating (and optionally applying) one stat role.
/// </summary>
/// <param name="StatRoleId">The stat role.</param>
/// <param name="RoleId">The Discord role.</param>
/// <param name="Qualifying">Everyone who meets the condition after filters and grouping.</param>
/// <param name="ToGrant">Qualifying members who do not have the role yet.</param>
/// <param name="ToRemove">Members holding the role who no longer qualify. Empty for permanent roles.</param>
/// <param name="Granted">How many grants succeeded, when applied.</param>
/// <param name="Removed">How many removals succeeded, when applied.</param>
/// <param name="Failed">How many role edits failed, when applied.</param>
public record StatRoleRunResult(
    int StatRoleId,
    ulong RoleId,
    IReadOnlyList<StatRoleMember> Qualifying,
    IReadOnlyList<StatRoleMember> ToGrant,
    IReadOnlyList<StatRoleMember> ToRemove,
    int Granted,
    int Removed,
    int Failed);