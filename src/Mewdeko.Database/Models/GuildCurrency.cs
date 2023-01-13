namespace Mewdeko.Database.Models;

public class GuildCurrency
{
    public ulong GuildId { get; set; }
    public ulong UserId { get; set; }
    public int Amount { get; set; }
}