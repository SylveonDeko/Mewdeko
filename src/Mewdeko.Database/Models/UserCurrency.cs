namespace Mewdeko.Database.Models;

public class UserCurrency : DbEntity
{
    public ulong UserId { get; set; }
    public ulong CurrencyAmount { get; set; }
    public ulong? GuildId { get; set; } = 0;
}