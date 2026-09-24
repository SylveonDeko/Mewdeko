using DataModel;

namespace Mewdeko.Modules.RoleMenus.Common;

/// <summary>
///     The outcome of a role menu write.
/// </summary>
public class RoleMenuResult
{
    /// <summary>
    ///     True when the write went through.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    ///     The menu as stored after the write.
    /// </summary>
    public RoleMenu? Menu { get; set; }

    /// <summary>
    ///     Why the write was refused.
    /// </summary>
    public RoleMenuError Error { get; set; }

    /// <summary>
    ///     The offending role name, emoji text, field name, or Discord failure reason.
    /// </summary>
    public string? Detail { get; set; }

    /// <summary>
    ///     Creates a successful result.
    /// </summary>
    /// <param name="menu">The stored menu.</param>
    /// <returns>The result.</returns>
    public static RoleMenuResult Ok(RoleMenu menu)
    {
        return new RoleMenuResult
        {
            Success = true, Menu = menu, Error = RoleMenuError.None
        };
    }

    /// <summary>
    ///     Creates a failed result.
    /// </summary>
    /// <param name="error">Why the write was refused.</param>
    /// <param name="detail">Optional detail for the message shown to the user.</param>
    /// <returns>The result.</returns>
    public static RoleMenuResult Fail(RoleMenuError error, string? detail = null)
    {
        return new RoleMenuResult
        {
            Success = false, Error = error, Detail = detail
        };
    }
}

/// <summary>
///     What happened when a member used a menu.
/// </summary>
public class RoleMenuOutcome
{
    /// <summary>
    ///     Roles given to the member.
    /// </summary>
    public List<ulong> Added { get; set; } = [];

    /// <summary>
    ///     Roles taken from the member.
    /// </summary>
    public List<ulong> Removed { get; set; } = [];

    /// <summary>
    ///     Localized notes about picks that were refused or trimmed.
    /// </summary>
    public List<string> Notes { get; set; } = [];

    /// <summary>
    ///     True when the role change failed unexpectedly.
    /// </summary>
    public bool Failed { get; set; }
}
