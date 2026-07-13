namespace Mewdeko.Controllers.Common.Tickets;

/// <summary>
///     Request model for adding a select option
/// </summary>
public class AddSelectOptionRequest : AddTicketComponentRequestBase
{
    /// <summary>
    ///     Optional description for the option
    /// </summary>
    public string? Description { get; set; }
}