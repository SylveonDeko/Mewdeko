namespace Mewdeko.Database.Models;

/// <summary>
/// Storage of giveaway users
/// </summary>
public class GiveawayUsers : DbEntity
{
    /// <summary>
    /// The giveaways id
    /// </summary>
    public int GiveawayId { get; set; }
    /// <summary>
    /// The user who is in the giveaway
    /// </summary>
    public ulong UserId { get; set; }
}