namespace Mewdeko.Common;

/// <summary>
///     Represents a single die used in tabletop gaming.
/// </summary>
public class Die
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="Die" /> class with the specified count and sides.
    /// </summary>
    /// <param name="count">The number of dice.</param>
    /// <param name="sides">The number of sides on each die.</param>
    public Die(int count, int sides)
    {
        Sides = sides;
        Count = count;
    }

    /// <summary>
    ///     Gets or sets the number of sides on each die.
    /// </summary>
    public int Sides { get; set; }

    /// <summary>
    ///     Gets or sets the number of dice.
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    ///     Returns a string representation of the die in the format "CountdSides" (e.g., "2d6" for two six-sided dice).
    /// </summary>
    /// <returns>A string representing the die.</returns>
    public override string ToString()
    {
        return $"{Count}d{Sides}";
    }
}