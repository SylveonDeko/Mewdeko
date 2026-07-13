namespace Mewdeko.Controllers.Common.Tickets;

/// <summary>
///     Request model for updating select menu option settings
/// </summary>
public class UpdateSelectOptionRequest : UpdateTicketComponentRequestBase
{
    /// <summary>
    ///     Updated option description
    /// </summary>
    public string? Description { get; set; }
}