namespace Mewdeko.Modules.Xp.Common;

/// <summary>
/// Represents the level statistics of a user, calculating level based on total XP.
/// </summary>
public class LevelStats
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LevelStats"/> class, calculating the level from total XP.
    /// </summary>
    /// <param name="xp">The total XP.</param>
    public LevelStats(int xp)
    {
        // Constructor implementation.
    }

    /// <summary>
    /// Gets the calculated level.
    /// </summary>
    public int Level { get; }

    /// <summary>
    /// Gets the XP within the current level.
    /// </summary>
    public int LevelXp { get; }

    /// <summary>
    /// Gets the required XP to reach the next level.
    /// </summary>
    public int RequiredXp { get; }

    /// <summary>
    /// Gets the total XP.
    /// </summary>
    public int TotalXp { get; }
}