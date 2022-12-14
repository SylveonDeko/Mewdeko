using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;

namespace Mewdeko.Database.Models;

[DebuggerDisplay("{global::Mewdeko.Modules.Permissions.PermissionExtensions.GetCommand(this)}",
    Target = typeof(Permission))]
public class Permission : DbEntity
{
    public Permission Previous { get; set; } = null;
    public Permission Next { get; set; } = null;

    public PrimaryPermissionType PrimaryTarget { get; set; }
    public ulong PrimaryTargetId { get; set; }

    public SecondaryPermissionType SecondaryTarget { get; set; }
    public string SecondaryTargetName { get; set; }

    public bool State { get; set; }
}

public interface IIndexed
{
    int Index { get; set; }
}

[DebuggerDisplay("{PrimaryTarget}{SecondaryTarget} {SecondaryTargetName} {State} {PrimaryTargetId}")]
public class Permissionv2 : DbEntity, IIndexed
{
    public int? GuildConfigId { get; set; }

    public PrimaryPermissionType PrimaryTarget { get; set; }
    public ulong PrimaryTargetId { get; set; }

    public SecondaryPermissionType SecondaryTarget { get; set; }
    public string SecondaryTargetName { get; set; }

    public bool IsCustomCommand { get; set; }

    public bool State { get; set; }

    [NotMapped]
    public static Permissionv2 AllowAllPerm => new()
    {
        PrimaryTarget = PrimaryPermissionType.Server,
        PrimaryTargetId = 0,
        SecondaryTarget = SecondaryPermissionType.AllModules,
        SecondaryTargetName = "*",
        State = true,
        Index = 0
    };

    public static List<Permissionv2> GetDefaultPermlist =>
        new()
        {
            AllowAllPerm
        };

    public int Index { get; set; }
}

public enum PrimaryPermissionType
{
    User,
    Channel,
    Role,
    Server,
    Category
}

public enum SecondaryPermissionType
{
    Module,
    Command,
    AllModules
}