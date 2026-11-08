namespace Mewdeko.Controllers.Common.Todo;

/// <summary>
///     Request model for adding a todo item
/// </summary>
public class AddTodoItemRequest
{
    /// <summary>
    ///     Title of the todo item
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    ///     Optional description for the todo item
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    ///     User ID adding the item
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    ///     Priority level (1=Low, 2=Medium, 3=High)
    /// </summary>
    public int Priority { get; set; } = 1;

    /// <summary>
    ///     Optional due date for the item
    /// </summary>
    public DateTime? DueDate { get; set; }

    /// <summary>
    ///     Tags to attach to the item
    /// </summary>
    public string[]? Tags { get; set; }
}