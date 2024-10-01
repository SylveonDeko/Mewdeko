using System.ComponentModel;

/// <summary>
/// </summary>
public class TicketPanel : DbEntity
{
    /// <summary>
    ///     The servers id
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    ///     The channel where the ticket panel is
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     The json for the panel
    /// </summary>
    [DefaultValue("")]
    public string? MessageJson { get; set; }

    /// <summary>
    ///     A list of buttons associated with this panel
    /// </summary>
    public List<TicketButton> Buttons { get; set; } = [];
}