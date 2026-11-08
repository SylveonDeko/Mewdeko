namespace Mewdeko.Controllers.Common.Patreon;

/// <summary>
///     Request model for linking a Patreon supporter to a Discord member
/// </summary>
public class PatreonSupporterLinkRequest
{
    /// <summary>
    ///     Patreon user ID of the supporter
    /// </summary>
    public string PatreonUserId { get; set; } = null!;

    /// <summary>
    ///     Discord user ID to link, 0 to unlink
    /// </summary>
    public ulong DiscordUserId { get; set; }
}
