namespace Mewdeko.Modules.Utility.Common;

/// <summary>
/// Defines the types of lists that can be applied to stream roles, such as whitelists or blacklists.
/// </summary>
public enum StreamRoleListType
{
    /// <summary>
    /// A whitelist specifies allowed entities.
    /// </summary>
    Whitelist,

    /// <summary>
    /// A blacklist specifies disallowed entities.
    /// </summary>
    Blacklist
}