namespace Mewdeko.Database.Models;

public class Warning2 : DbEntity
{
    public ulong GuildId { get; set; }
    public ulong UserId { get; set; }
    public string Reason { get; set; }
    public long Forgiven { get; set; }
    public string ForgivenBy { get; set; }
    public string Moderator { get; set; }
}