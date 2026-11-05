namespace Mewdeko.Controllers.Common.FreeGames;

/// <summary>
///     A channel that gets free game alerts, as read and as written.
/// </summary>
public class FreeGameAlertDto
{
    /// <summary>
    ///     The channel alerts are posted in.
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     The role pinged with each alert, or null for no ping.
    /// </summary>
    public ulong? RoleId { get; set; }

    /// <summary>
    ///     Whether Epic Games Store games are announced.
    /// </summary>
    public bool Epic { get; set; } = true;

    /// <summary>
    ///     Whether Steam games are announced.
    /// </summary>
    public bool Steam { get; set; } = true;

    /// <summary>
    ///     Whether GOG games are announced.
    /// </summary>
    public bool Gog { get; set; } = true;
}
