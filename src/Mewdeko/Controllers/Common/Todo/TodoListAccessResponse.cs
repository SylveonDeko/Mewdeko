namespace Mewdeko.Controllers.Common.Todo;

/// <summary>
///     What a user can do on a todo list
/// </summary>
public class TodoListAccessResponse
{
    /// <summary>
    ///     Whether the user owns the list
    /// </summary>
    public bool IsOwner { get; set; }

    /// <summary>
    ///     Can view the list
    /// </summary>
    public bool CanView { get; set; }

    /// <summary>
    ///     Can add items
    /// </summary>
    public bool CanAdd { get; set; }

    /// <summary>
    ///     Can edit other people's items
    /// </summary>
    public bool CanEdit { get; set; }

    /// <summary>
    ///     Can complete other people's items
    /// </summary>
    public bool CanComplete { get; set; }

    /// <summary>
    ///     Can delete other people's items
    /// </summary>
    public bool CanDelete { get; set; }

    /// <summary>
    ///     Can manage the list and its permissions
    /// </summary>
    public bool CanManage { get; set; }
}
