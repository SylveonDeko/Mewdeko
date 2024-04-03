namespace Mewdeko.Modules.Permissions.Common;

/// <summary>
/// Represents the old permission cache structure for storing role-based permissions and settings.
/// </summary>
public class OldPermissionCache
{
    /// <summary>
    /// Gets or sets the role associated with these permissions.
    /// </summary>
    public string PermRole { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether verbose logging or responses should be used.
    /// </summary>
    public bool Verbose { get; set; } = true;

    /// <summary>
    /// Gets or sets the root permission node.
    /// </summary>
    public Permission RootPermission { get; set; }
}

/// <summary>
/// Represents the new permission cache structure with enhanced permission management.
/// </summary>
public class PermissionCache
{
    /// <summary>
    /// Gets or sets the role associated with these permissions.
    /// </summary>
    public string PermRole { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether verbose logging or responses should be enabled.
    /// </summary>
    public bool Verbose { get; set; } = true;

    /// <summary>
    /// Gets or sets the collection of version 2 permissions.
    /// </summary>
    /// <remarks>
    /// This property introduces a new permissions system, allowing for more granular control and organization of permissions.
    /// </remarks>
    public PermissionsCollection<Permissionv2>? Permissions { get; set; }
}