using Mewdeko.Modules.Xp.Services;

namespace Mewdeko.Modules.Xp.Extensions;

/// <summary>
///     Provides extension methods for UserXpStats.
/// </summary>
public static class Extensions
{
    /// <summary>
    ///     Calculates and returns the user's current level, the XP they have at this level, and the XP required to advance to
    ///     the next level.
    /// </summary>
    /// <param name="stats">The UserXpStats instance to calculate level data for. Represents the user's current XP stats.</param>
    /// <returns>
    ///     A tuple containing three integers:
    ///     - The first integer represents the user's current level.
    ///     - The second integer represents the XP the user has at the current level.
    ///     - The third integer represents the XP required to advance to the next level.
    /// </returns>
    /// <remarks>
    ///     This method calculates the required XP for each level using a formula based on the base XP required for level 1,
    ///     and increments the level until the total XP is less than the sum of the required XP for the next level.
    /// </remarks>
    public static (int Level, int LevelXp, int LevelRequiredXp) GetLevelData(this UserXpStats stats)
    {
        const int baseXp = XpService.XpRequiredLvl1;

        int required;
        var totalXp = 0;
        var lvl = 1;
        while (true)
        {
            required = (int)(baseXp + baseXp / 4.0 * (lvl - 1));

            if (required + totalXp > stats.Xp)
                break;

            totalXp += required;
            lvl++;
        }

        return (lvl - 1, stats.Xp - totalXp, required);
    }
}