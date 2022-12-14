namespace Mewdeko.Common;

public class RollResult
{
    public Dictionary<Die, List<int>>? Results { get; set; }
    public int Total { get; set; }
    public bool InacurateTotal { get; set; }
    public override string ToString() => $"Total: **{Total.ToString()}**";

    public RollResult() => Results = new Dictionary<Die, List<int>>();
}