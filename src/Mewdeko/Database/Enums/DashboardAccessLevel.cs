namespace Mewdeko.Database.Enums;

/// <summary>
///     How much a dashboard access grant allows for a given section.
/// </summary>
public enum DashboardAccessLevel
{
    /// <summary>
    ///     No access to this section.
    /// </summary>
    None = 0,

    /// <summary>
    ///     Can view the section but not make changes (GET requests only).
    /// </summary>
    View = 1,

    /// <summary>
    ///     Can view and modify the section.
    /// </summary>
    Manage = 2
}