namespace Mewdeko.Database.Models;

public class GlobalBans : DbEntity
{
    public ulong UserId { get; set; }
    public string Proof { get; set; }
    public string Reason { get; set; }
    public GBActionType RecommendedAction { get; set; }
    public string Duration { get; set; }
    public ulong AddedBy { get; set; }
    public GbType Type { get; set; }
}

[Flags]
public enum GbType
{
    None = 0,
    Scammer = 1,
    Hacked = 2,
    Pedo = 4,
    Troll = 8,
    Raider = 16,
    Transphobic = 32
}