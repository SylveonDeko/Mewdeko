namespace Mewdeko.Database.Models;

public class GlobalUserBalance : DbEntity
{
    public ulong UserId { get; set; }
    public long Balance { get; set; }
}

public class GuildUserBalance : DbEntity
{
    public ulong GuildId { get; set; }
    public ulong UserId { get; set; }
    public long Balance { get; set; }
}

public class TransactionHistory : DbEntity
{
    public ulong GuildId { get; set; }
    public ulong? UserId { get; set; } = 0;
    public long Amount { get; set; }
    public string Description { get; set; }
}