namespace Mewdeko.Database.Enums;

/// <summary>
///     Represents the type of role action to perform when a form response is approved or rejected.
/// </summary>
public enum RoleActionType
{
    /// <summary>
    ///     No role action will be performed.
    /// </summary>
    None = 0,

    /// <summary>
    ///     Add specified roles to the user.
    /// </summary>
    AddRoles = 1,

    /// <summary>
    ///     Remove specified roles from the user.
    /// </summary>
    RemoveRoles = 2
}