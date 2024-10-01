/// <summary>
///     Button
/// </summary>
public class TicketButton : DbEntity
{
    /// <summary>
    ///     Id
    /// </summary>
    public int TicketPanelId { get; set; }

    /// <summary>
    /// </summary>
    public string Label { get; set; }

    /// <summary>
    /// </summary>
    public string Emoji { get; set; }

    /// <summary>
    /// </summary>
    public string OpenMessage { get; set; }
}