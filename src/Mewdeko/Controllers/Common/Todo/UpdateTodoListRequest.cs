namespace Mewdeko.Controllers.Common.Todo;

/// <summary>
///     Request model for updating a todo list
/// </summary>
public class UpdateTodoListRequest
{
    /// <summary>
    ///     The new name (null to keep the current name)
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    ///     The new description (null or empty to clear it)
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    ///     The new hex color (null to keep the current color)
    /// </summary>
    public string? Color { get; set; }

    /// <summary>
    ///     Whether everyone who can see the server can view the list (null to keep the current setting)
    /// </summary>
    public bool? IsPublic { get; set; }

    /// <summary>
    ///     User ID making the change
    /// </summary>
    public ulong UserId { get; set; }
}
