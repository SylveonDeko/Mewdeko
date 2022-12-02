namespace Mewdeko.Database.Models;

public class GlobalBans : DbEntity
{
    public ulong UserId { get; set; }
    public string Proof { get; set; }
    public string Reason { get; set; }
    public ulong AddedBy { get; set; }
    public string Type { get; set; }
}