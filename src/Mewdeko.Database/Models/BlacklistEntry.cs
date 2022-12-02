namespace Mewdeko.Database.Models;

public class BlacklistEntry : DbEntity
{
    public ulong ItemId { get; set; }
    public BlacklistType Type { get; set; }
    public string Reason { get; set; } = "No reason provided.";
}

public enum BlacklistType
{
    Server,
    Channel,
    User
}