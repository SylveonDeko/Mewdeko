namespace Mewdeko.Modules.Xp.Services;

/// <summary>
///     Represents a cache item for a user, including their guild and channel context, and XP amount.
/// </summary>
public class UserCacheItem
{
    /// <summary>
    ///     Gets or sets the user.
    /// </summary>
    public IGuildUser User { get; set; }

    /// <summary>
    ///     Gets or sets the guild to which the user belongs.
    /// </summary>
    public IGuild Guild { get; set; }

    /// <summary>
    ///     Gets or sets the channel in which the user's activity took place.
    /// </summary>
    public IMessageChannel Channel { get; set; }

    /// <summary>
    ///     Gets or sets the XP amount associated with the user's activity.
    /// </summary>
    public int XpAmount { get; set; }

    /// <summary>
    ///     Serves as the default hash function.
    /// </summary>
    /// <returns>A hash code for the current user.</returns>
    public override int GetHashCode()
    {
        return User.GetHashCode();
    }

    /// <summary>
    ///     Determines whether the specified object is equal to the current user cache item.
    /// </summary>
    /// <param name="obj">The object to compare with the current object.</param>
    /// <returns>true if the specified object is equal to the current object; otherwise, false.</returns>
    public override bool Equals(object? obj)
    {
        return obj is UserCacheItem uci && uci.User == User;
    }
}