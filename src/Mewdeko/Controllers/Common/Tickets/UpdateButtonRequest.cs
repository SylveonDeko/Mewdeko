namespace Mewdeko.Controllers.Common.Tickets;

/// <summary>
///     Request model for updating button settings
/// </summary>
public class UpdateButtonRequest : UpdateTicketComponentRequestBase
{
    /// <summary>
    ///     Updated button style
    /// </summary>
    public ButtonStyle? Style { get; set; }
}