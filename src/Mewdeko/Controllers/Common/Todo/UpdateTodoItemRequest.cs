namespace Mewdeko.Controllers.Common.Todo;

/// <summary>
///     Request model for updating a todo item
/// </summary>
public class UpdateTodoItemRequest
{
    /// <summary>
    ///     New title for the item
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    ///     New description for the item
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    ///     Optional new priority level (1=Low, 2=Medium, 3=High, 4=Critical). Values outside 1-4 are clamped.
    ///     When null, the existing priority is left unchanged.
    /// </summary>
    public int? Priority { get; set; }

    /// <summary>
    ///     Optional new due date for the item. When null, the existing due date is left unchanged; use the
    ///     dedicated due date endpoint to clear it.
    /// </summary>
    public DateTime? DueDate { get; set; }

    /// <summary>
    ///     User ID making the update
    /// </summary>
    public ulong UserId { get; set; }
}