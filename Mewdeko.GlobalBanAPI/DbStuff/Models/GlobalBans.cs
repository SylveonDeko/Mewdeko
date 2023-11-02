namespace Mewdeko.GlobalBanAPI.DbStuff.Models;

public class GlobalBans : DbEntity
{
    public ulong UserId { get; set; }
    public string Proof { get; set; }
    public string Reason { get; set; }
    public GbActionType RecommendedAction { get; set; }
    public bool IsApproved { get; set; } = false;
    public bool IsAppealable { get; set; } = true;
    public ulong AddedBy { get; set; }
    public ulong ApprovedBy { get; set; }
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

public enum GbActionType
{
    Kick,
    Ban,
    Timeout,
    None
}