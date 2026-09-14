namespace Mewdeko.Modules.Xp.Models;

/// <summary>
///     Defines the type of XP competition.
/// </summary>
public enum XpCompetitionType
{
    /// <summary>
    ///     Competition based on who gains the most XP during the competition period.
    /// </summary>
    MostGained,

    /// <summary>
    ///     Competition based on who reaches a specific target level first.
    /// </summary>
    ReachLevel,

    /// <summary>
    ///     Competition based on who has the highest total XP at the end of the competition.
    /// </summary>
    HighestTotal
}

/// <summary>
///     Defines the kind of reward attached to a competition placement.
/// </summary>
public enum XpCompetitionRewardType
{
    /// <summary>
    ///     A role granted to the placing user.
    /// </summary>
    Role,

    /// <summary>
    ///     An amount of XP granted to the placing user.
    /// </summary>
    Xp,

    /// <summary>
    ///     An amount of currency granted to the placing user.
    /// </summary>
    Currency
}