namespace Mewdeko.Common;

public class Die
{
    public Die(int count, int sides)
    {
        Sides = sides;
        Count = count;
    }

    public int Sides { get; set; }
    public int Count { get; set; }

    public override string ToString() => $"{Count}d{Sides}";
}