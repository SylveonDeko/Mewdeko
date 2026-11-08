namespace Mewdeko.Controllers.Common.Todo;

/// <summary>
///     Request model for moving a todo item
/// </summary>
public class ReorderTodoItemRequest
{
    /// <summary>
    ///     The new 1-based position
    /// </summary>
    public int Position { get; set; }

    /// <summary>
    ///     User ID making the change
    /// </summary>
    public ulong UserId { get; set; }
}
