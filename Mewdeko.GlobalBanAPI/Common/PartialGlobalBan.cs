using Mewdeko.GlobalBanAPI.DbStuff.Models;

namespace Mewdeko.GlobalBanAPI.Common;

public class PartialGlobalBan
{
    public ulong UserId { get; set; }
    public ulong AddedBy { get; set; }
    public string Reason { get; set; }
    public string Proof { get; set; }
    public GbActionType RecommendedAction { get; set; }
    public GbType Type { get; set; }
}