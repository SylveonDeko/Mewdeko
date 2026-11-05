namespace Mewdeko.Controllers.Common.Filter;

/// <summary>
///     Request model for the filter extras. Fields left out stay as they are.
/// </summary>
public class FilterExtrasRequest
{
    /// <summary>
    ///     Whether filtered words are also matched through spacing, symbols and look-alike characters
    /// </summary>
    public bool? NormalizeWords { get; set; }

    /// <summary>
    ///     Whether the invite filter only acts on members with no roles
    /// </summary>
    public bool? InviteNoRolesOnly { get; set; }

    /// <summary>
    ///     Whether the link filter only acts on members with no roles
    /// </summary>
    public bool? LinkNoRolesOnly { get; set; }

    /// <summary>
    ///     The domains the link filter lets through, replacing the current list
    /// </summary>
    public List<string>? AllowedDomains { get; set; }

    /// <summary>
    ///     The domains the link filter always removes, replacing the current list
    /// </summary>
    public List<string>? BlockedDomains { get; set; }
}
