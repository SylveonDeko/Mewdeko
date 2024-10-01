using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;

namespace Mewdeko.Database.Models;

/// <summary>
///     Represents a permission.
/// </summary>
[DebuggerDisplay("{global::Mewdeko.Modules.Permissions.PermissionExtensions.GetCommand(this)}",
    Target = typeof(Permission))]
public class Permission : DbEntity
{
    /// <summary>
    ///     Gets or sets the previous permission.
    /// </summary>
    public Permission Previous { get; set; } = null;

    /// <summary>
    ///     Gets or sets the next permission.
    /// </summary>
    public Permission Next { get; set; } = null;

    /// <summary>
    ///     Gets or sets the primary target of the permission.
    /// </summary>
    public PrimaryPermissionType PrimaryTarget { get; set; }

    /// <summary>
    ///     Gets or sets the primary target ID.
    /// </summary>
    public ulong PrimaryTargetId { get; set; }

    /// <summary>
    ///     Gets or sets the secondary target of the permission.
    /// </summary>
    public SecondaryPermissionType SecondaryTarget { get; set; }

    /// <summary>
    ///     Gets or sets the name of the secondary target.
    /// </summary>
    public string? SecondaryTargetName { get; set; }

    /// <summary>
    ///     Gets or sets the state of the permission.
    /// </summary>
    public bool State { get; set; }
}

/// <summary>
///     Represents an indexed permission.
/// </summary>
[DebuggerDisplay("{PrimaryTarget}{SecondaryTarget} {SecondaryTargetName} {State} {PrimaryTargetId}")]
public class Permissionv2 : DbEntity, IIndexed
{
    /// <summary>
    ///     Gets or sets the guild configuration ID.
    /// </summary>
    [ForeignKey("GuildConfigId")]
    public int? GuildConfigId { get; set; }

    /// <summary>
    ///     Gets or sets the primary target of the permission.
    /// </summary>
    public PrimaryPermissionType PrimaryTarget { get; set; }

    /// <summary>
    ///     Gets or sets the primary target ID.
    /// </summary>
    public ulong PrimaryTargetId { get; set; }

    /// <summary>
    ///     Gets or sets the secondary target of the permission.
    /// </summary>
    public SecondaryPermissionType SecondaryTarget { get; set; }

    /// <summary>
    ///     Gets or sets the name of the secondary target.
    /// </summary>
    public string? SecondaryTargetName { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether this is a custom command.
    /// </summary>
    public bool IsCustomCommand { get; set; } = false;

    /// <summary>
    ///     Gets or sets the state of the permission.
    /// </summary>
    public bool State { get; set; }

    /// <summary>
    ///     Gets the default permission list.
    /// </summary>
    [NotMapped]
    public static Permissionv2 AllowAllPerm
    {
        get
        {
            return new Permissionv2
            {
                PrimaryTarget = PrimaryPermissionType.Server,
                PrimaryTargetId = 0,
                SecondaryTarget = SecondaryPermissionType.AllModules,
                SecondaryTargetName = "*",
                State = true,
                Index = 0
            };
        }
    }

    /// <summary>
    ///     Gets the default permission list.
    /// </summary>
    public static List<Permissionv2> GetDefaultPermlist
    {
        get
        {
            return [AllowAllPerm];
        }
    }

    /// <summary>
    ///     Gets or sets the index of the permission.
    /// </summary>
    public int Index { get; set; }
}

/// <summary>
///     Specifies the primary permission type.
/// </summary>
public enum PrimaryPermissionType
{
    /// <summary>
    ///     User-specific permission.
    /// </summary>
    User,

    /// <summary>
    ///     Channel-specific permission.
    /// </summary>
    Channel,

    /// <summary>
    ///     Role-specific permission.
    /// </summary>
    Role,

    /// <summary>
    ///     Server-wide permission.
    /// </summary>
    Server,

    /// <summary>
    ///     Category-specific permission.
    /// </summary>
    Category
}

/// <summary>
///     Specifies the secondary permission type.
/// </summary>
public enum SecondaryPermissionType
{
    /// <summary>
    ///     Module-specific permission.
    /// </summary>
    Module,

    /// <summary>
    ///     Command-specific permission.
    /// </summary>
    Command,

    /// <summary>
    ///     Permission for all modules.
    /// </summary>
    AllModules
}

/// <summary>
///     Represents an indexed entity.
/// </summary>
public interface IIndexed
{
    /// <summary>
    ///     Gets or sets the index of the entity.
    /// </summary>
    int Index { get; set; }
}