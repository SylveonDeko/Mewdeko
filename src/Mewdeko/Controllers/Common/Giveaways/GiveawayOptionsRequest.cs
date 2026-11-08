namespace Mewdeko.Controllers.Common.Giveaways;

/// <summary>
///     The settings of a running giveaway or draft that can still change.
/// </summary>
public class GiveawayOptionsRequest
{
    /// <summary>
    ///     Space separated <c>roleId:extraEntries</c> pairs, or empty for none.
    /// </summary>
    public string? ExtraEntryRoles { get; set; }

    /// <summary>
    ///     The automatic prize: 0 none, 1 XP, 2 coins.
    /// </summary>
    public int PrizeType { get; set; }

    /// <summary>
    ///     How much each winner gets.
    /// </summary>
    public long PrizeAmount { get; set; }
}
