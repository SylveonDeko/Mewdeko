namespace Mewdeko.Database.Enums;

/// <summary>
///     The kind of dashboard activity recorded in a <see cref="DataModel.DashboardAuditLog" /> entry.
/// </summary>
public enum AuditAction
{
    /// <summary>
    ///     The user read data (an HTTP GET request).
    /// </summary>
    View = 0,

    /// <summary>
    ///     The user created a resource (a POST that adds something).
    /// </summary>
    Create = 1,

    /// <summary>
    ///     The user modified an existing resource (POST/PUT/PATCH).
    /// </summary>
    Update = 2,

    /// <summary>
    ///     The user removed a resource (an HTTP DELETE request).
    /// </summary>
    Delete = 3,

    /// <summary>
    ///     The user accessed the dashboard itself (session start / login).
    /// </summary>
    Access = 4
}
