using System.ComponentModel.DataAnnotations.Schema;

namespace Mewdeko.Database.Models;

/// <summary>
///     Represents the stream role settings for a guild.
/// </summary>
public class StreamRoleSettings : DbEntity
{
    /// <summary>
    ///     Gets or sets the guild configuration ID.
    /// </summary>
    [ForeignKey("GuildConfigId")]
    public int GuildConfigId { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether the feature is enabled in the guild.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    ///     Gets or sets the role ID to give to the users in the role 'FromRole' when they start streaming.
    /// </summary>
    public ulong AddRoleId { get; set; }

    /// <summary>
    ///     Gets or sets the role ID whose users are eligible to get the 'AddRole'.
    /// </summary>
    public ulong FromRoleId { get; set; }

    /// <summary>
    ///     Gets or sets the keyword for the streaming status.
    /// </summary>
    public string? Keyword { get; set; }

    /// <summary>
    ///     Gets or sets the collection of whitelisted users' IDs.
    /// </summary>
    public HashSet<StreamRoleWhitelistedUser> Whitelist { get; set; } = [];

    /// <summary>
    ///     Gets or sets the collection of blacklisted users' IDs.
    /// </summary>
    public HashSet<StreamRoleBlacklistedUser> Blacklist { get; set; } = [];
}

/// <summary>
///     Represents a blacklisted user for stream role settings.
/// </summary>
public class StreamRoleBlacklistedUser : DbEntity
{
    /// <summary>
    ///     Gets or sets the user ID.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    ///     Gets or sets the username.
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    ///     Gets or sets the stream role settings ID.
    /// </summary>
    [ForeignKey("StreamRoleSettingsId")]
    public int StreamRoleSettingsId { get; set; }

    /// <summary>
    ///     Determines whether the specified object is equal to the current object.
    /// </summary>
    /// <param name="obj">The object to compare with the current object.</param>
    /// <returns>true if the specified object is equal to the current object; otherwise, false.</returns>
    public override bool Equals(object obj)
    {
        if (obj is not StreamRoleBlacklistedUser x)
            return false;

        return x.UserId == UserId;
    }

    /// <summary>
    ///     Returns the hash code for this instance.
    /// </summary>
    /// <returns>A hash code for the current object.</returns>
    public override int GetHashCode()
    {
        return UserId.GetHashCode();
    }
}

/// <summary>
///     Represents a whitelisted user for stream role settings.
/// </summary>
public class StreamRoleWhitelistedUser : DbEntity
{
    /// <summary>
    ///     Gets or sets the stream role settings ID.
    /// </summary>
    [ForeignKey("StreamRoleSettingsId")]
    public int StreamRoleSettingsId { get; set; }

    /// <summary>
    ///     Gets or sets the user ID.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    ///     Gets or sets the username.
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    ///     Determines whether the specified object is equal to the current object.
    /// </summary>
    /// <param name="obj">The object to compare with the current object.</param>
    /// <returns>true if the specified object is equal to the current object; otherwise, false.</returns>
    public override bool Equals(object obj)
    {
        return obj is StreamRoleWhitelistedUser x && x.UserId == UserId;
    }

    /// <summary>
    ///     Returns the hash code for this instance.
    /// </summary>
    /// <returns>A hash code for the current object.</returns>
    public override int GetHashCode()
    {
        return UserId.GetHashCode();
    }
}