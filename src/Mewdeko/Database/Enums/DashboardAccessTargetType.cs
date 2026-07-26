namespace Mewdeko.Database.Enums;

/// <summary>
///     Whether a dashboard access grant or manager entry targets a specific user or an entire role.
/// </summary>
public enum DashboardAccessTargetType
{
    /// <summary>
    ///     The entry targets a specific Discord user.
    /// </summary>
    User = 0,

    /// <summary>
    ///     The entry targets everyone holding a specific Discord role.
    /// </summary>
    Role = 1
}