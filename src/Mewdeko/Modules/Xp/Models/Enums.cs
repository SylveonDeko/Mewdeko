namespace Mewdeko.Modules.Xp.Models;

/// <summary>
///     Specifies the direction of the XP template bar.
/// </summary>
public enum XpTemplateDirection
{
    /// <summary>
    ///     Up
    /// </summary>
    Up,

    /// <summary>
    ///     Down
    /// </summary>
    Down,

    /// <summary>
    ///     Left
    /// </summary>
    Left,

    /// <summary>
    ///     Right
    /// </summary>
    Right
}

/// <summary>
///     Specifies the type of XP curve to use for calculating levels.
/// </summary>
public enum XpCurveType
{
    /// <summary>
    ///     Standard curve (default).
    /// </summary>
    Standard = 0,

    /// <summary>
    ///     Linear curve (consistent level-up requirements).
    /// </summary>
    Linear = 1,

    /// <summary>
    ///     Accelerated curve (faster early levels, steeper later levels).
    /// </summary>
    Accelerated = 2,

    /// <summary>
    ///     Decelerated curve (slower early levels, more gradual later levels).
    /// </summary>
    Decelerated = 3,

    /// <summary>
    ///     Custom curve defined by formula.
    /// </summary>
    Custom = 4,

    /// <summary>
    ///     Uses the old xp curve approach before the xp rewrite.
    /// </summary>
    Legacy = 5,

    /// <summary>
    ///     MEE6's curve: each level costs 5n² + 50n + 100 XP, so imported MEE6 XP keeps its level.
    /// </summary>
    Mee6 = 6,

    /// <summary>
    ///     Lurkr's curve: each level costs 50n² - 100n + 150 XP.
    /// </summary>
    Lurkr = 7,

    /// <summary>
    ///     Amari's curve: each level costs 20n² - 40n + 55 XP.
    /// </summary>
    Amari = 8
}

/// <summary>
///     Specifies the type of item excluded from XP gain.
/// </summary>
public enum ExcludedItemType
{
    /// <summary>
    ///     A channel is excluded.
    /// </summary>
    Channel = 0,

    /// <summary>
    ///     A role is excluded.
    /// </summary>
    Role = 1,

    /// <summary>
    ///     A user is excluded.
    /// </summary>
    User = 2
}