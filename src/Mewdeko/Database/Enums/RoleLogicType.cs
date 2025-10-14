namespace Mewdeko.Database.Enums;

/// <summary>
///     Represents how multiple roles should be evaluated in role-based conditionals.
/// </summary>
public enum RoleLogicType
{
    /// <summary>
    ///     User must have any one of the specified roles.
    /// </summary>
    Any,

    /// <summary>
    ///     User must have all of the specified roles.
    /// </summary>
    All,

    /// <summary>
    ///     User must have none of the specified roles.
    /// </summary>
    None
}