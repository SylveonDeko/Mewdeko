namespace Mewdeko.Database.Enums;

/// <summary>
///     Represents the action taken when a form response is approved or rejected.
/// </summary>
public enum WorkflowAction
{
    /// <summary>
    ///     No action was taken.
    /// </summary>
    None = 0,

    /// <summary>
    ///     The user was unbanned from the guild (ban appeal approved).
    /// </summary>
    Unbanned = 1,

    /// <summary>
    ///     An invite link was sent to the user (join application approved).
    /// </summary>
    InviteSent = 2,

    /// <summary>
    ///     Roles were pre-assigned for the user in RoleStates (join application approved).
    /// </summary>
    RolesPreassigned = 3
}