namespace Mewdeko.Controllers.Common.Tickets;

/// <summary>
///     Request model for reordering panel buttons
/// </summary>
public class ReorderButtonsRequest
{
    /// <summary>
    ///     List of button IDs in the desired order
    /// </summary>
    public List<int> ButtonOrder { get; set; } = null!;
}