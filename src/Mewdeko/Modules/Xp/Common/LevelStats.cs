using Mewdeko.Modules.Xp.Services;

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
        if (xp < 0)
            xp = 0;

        TotalXp = xp;

        const int baseXp = XpService.XpRequiredLvl1;

        int required;
        var totalXp = 0;
        var lvl = 1;
        while (true)
        {
            required = (int)(baseXp + (baseXp / 4.0 * (lvl - 1)));

            if (required + totalXp > xp)
                break;

            totalXp += required;
            lvl++;
            Level = lvl - 1;
            LevelXp = xp - totalXp;
            RequiredXp = required;
        }
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