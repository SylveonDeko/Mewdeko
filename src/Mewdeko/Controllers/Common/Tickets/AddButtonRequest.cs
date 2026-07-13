namespace Mewdeko.Controllers.Common.Tickets;

/// <summary>
///     Request model for adding a button to a panel
/// </summary>
public class AddButtonRequest : AddTicketComponentRequestBase
{
    /// <summary>
    ///     The button style
    /// </summary>
    public ButtonStyle Style { get; set; } = ButtonStyle.Primary;
}