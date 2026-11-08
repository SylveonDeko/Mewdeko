namespace Mewdeko.Controllers.Common.Todo;

/// <summary>
///     Request model for granting permissions
/// </summary>
public class GrantPermissionRequest
{
    /// <summary>
    ///     User ID to grant permissions to
    /// </summary>
    public ulong TargetUserId { get; set; }

    /// <summary>
    ///     User ID granting the permissions
    /// </summary>
    public ulong RequestingUserId { get; set; }

    /// <summary>
    ///     Can view the list
    /// </summary>
    public bool CanView { get; set; }

    /// <summary>
    ///     Can add items to the list
    /// </summary>
    public bool CanAdd { get; set; }

    /// <summary>
    ///     Can edit other people's items in the list
    /// </summary>
    public bool CanEdit { get; set; }

    /// <summary>
    ///     Can complete other people's items in the list
    /// </summary>
    public bool CanComplete { get; set; }

    /// <summary>
    ///     Can delete other people's items in the list
    /// </summary>
    public bool CanDelete { get; set; }

    /// <summary>
    ///     Can manage the list and permissions
    /// </summary>
    public bool CanManage { get; set; }
}