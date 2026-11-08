namespace Mewdeko.Database.Enums;

/// <summary>
///     Which card a design is drawn on.
/// </summary>
public enum CardDesignTarget
{
    /// <summary>
    ///     The XP rank card.
    /// </summary>
    Rank = 0,

    /// <summary>
    ///     The achievement card.
    /// </summary>
    Achievement = 1
}

/// <summary>
///     How members get a design.
/// </summary>
public enum CardDesignAvailability
{
    /// <summary>
    ///     Anyone can equip it.
    /// </summary>
    Free = 0,

    /// <summary>
    ///     It is bought with a shop item.
    /// </summary>
    Shop = 1,

    /// <summary>
    ///     It is granted for reaching a level, an achievement or a role.
    /// </summary>
    Unlock = 2
}

/// <summary>
///     What grants an unlock design.
/// </summary>
public enum CardDesignUnlockType
{
    /// <summary>
    ///     Reaching an XP level.
    /// </summary>
    Level = 0,

    /// <summary>
    ///     Unlocking an achievement.
    /// </summary>
    Achievement = 1,

    /// <summary>
    ///     Holding a role.
    /// </summary>
    Role = 2
}

/// <summary>
///     How a member came to own a design.
/// </summary>
public enum CardDesignSource
{
    /// <summary>
    ///     Bought from the shop.
    /// </summary>
    Purchase = 0,

    /// <summary>
    ///     Reached the unlock level.
    /// </summary>
    Level = 1,

    /// <summary>
    ///     Unlocked the unlock achievement.
    /// </summary>
    Achievement = 2,

    /// <summary>
    ///     Holds the unlock role.
    /// </summary>
    Role = 3
}

/// <summary>
///     Where a member design stands with the moderators.
/// </summary>
public enum CardDesignReview
{
    /// <summary>
    ///     Cleared to render everything it holds.
    /// </summary>
    Approved = 0,

    /// <summary>
    ///     Its uploaded background waits for a moderator and renders with the fallback until then.
    /// </summary>
    Pending = 1,

    /// <summary>
    ///     A moderator turned its uploaded background down.
    /// </summary>
    Rejected = 2
}

/// <summary>
///     Who may make their own design.
/// </summary>
public enum MemberDesignWho
{
    /// <summary>
    ///     Every member.
    /// </summary>
    Everyone = 0,

    /// <summary>
    ///     Members holding any of the chosen roles.
    /// </summary>
    Roles = 1,

    /// <summary>
    ///     Members at or above an XP level.
    /// </summary>
    Level = 2
}
