using System.ComponentModel.DataAnnotations.Schema;

namespace Mewdeko.Database.Models;

/// <summary>
///     Represents the XP settings for a guild.
/// </summary>
public class XpSettings : DbEntity
{
    /// <summary>
    ///     Gets or sets the guild configuration ID.
    /// </summary>
    [ForeignKey("GuildConfigId")]
    public int GuildConfigId { get; set; }

    /// <summary>
    ///     Gets or sets the role rewards.
    /// </summary>
    public HashSet<XpRoleReward> RoleRewards { get; set; } = [];

    /// <summary>
    ///     Gets or sets the currency rewards.
    /// </summary>
    public HashSet<XpCurrencyReward> CurrencyRewards { get; set; } = [];

    /// <summary>
    ///     Gets or sets a value indicating whether XP role rewards are exclusive.
    /// </summary>
    public bool XpRoleRewardExclusive { get; set; } = false;

    /// <summary>
    ///     Gets or sets the notification message for level up.
    /// </summary>
    public string? NotifyMessage { get; set; } = "Congratulations {0}! You have reached level {1}!";

    /// <summary>
    ///     Gets or sets the exclusion list for XP rewards.
    /// </summary>
    public HashSet<ExcludedItem> ExclusionList { get; set; } = [];

    /// <summary>
    ///     Gets or sets a value indicating whether the server is excluded from XP rewards.
    /// </summary>
    public bool ServerExcluded { get; set; } = false;
}

/// <summary>
///     Specifies the type of item to exclude.
/// </summary>
public enum ExcludedItemType
{
    /// <summary>
    ///     Exclude a channel.
    /// </summary>
    Channel,

    /// <summary>
    ///     Exclude a role.
    /// </summary>
    Role
}

/// <summary>
///     Represents an XP role reward in a guild.
/// </summary>
public class XpRoleReward : DbEntity
{
    /// <summary>
    ///     Gets or sets the XP settings ID.
    /// </summary>
    [ForeignKey("XpSettingsId")]
    public int XpSettingsId { get; set; }

    /// <summary>
    ///     Gets or sets the XP settings.
    /// </summary>
    public XpSettings XpSettings { get; set; }

    /// <summary>
    ///     Gets or sets the level for the reward.
    /// </summary>
    public int Level { get; set; }

    /// <summary>
    ///     Gets or sets the role ID for the reward.
    /// </summary>
    public ulong RoleId { get; set; }

    /// <summary>
    ///     Returns the hash code for this instance.
    /// </summary>
    /// <returns>A hash code for the current object.</returns>
    public override int GetHashCode()
    {
        return Level.GetHashCode() ^ XpSettingsId.GetHashCode();
    }

    /// <summary>
    ///     Determines whether the specified object is equal to the current object.
    /// </summary>
    /// <param name="obj">The object to compare with the current object.</param>
    /// <returns>true if the specified object is equal to the current object; otherwise, false.</returns>
    public override bool Equals(object obj)
    {
        return obj is XpRoleReward xrr && xrr.Level == Level && xrr.XpSettingsId == XpSettingsId;
    }
}

/// <summary>
///     Represents an XP currency reward in a guild.
/// </summary>
public class XpCurrencyReward : DbEntity
{
    /// <summary>
    ///     Gets or sets the XP settings ID.
    /// </summary>
    public int XpSettingsId { get; set; }

    /// <summary>
    ///     Gets or sets the XP settings.
    /// </summary>
    public XpSettings XpSettings { get; set; }

    /// <summary>
    ///     Gets or sets the level for the reward.
    /// </summary>
    public int Level { get; set; }

    /// <summary>
    ///     Gets or sets the amount of the reward.
    /// </summary>
    public int Amount { get; set; }

    /// <summary>
    ///     Returns the hash code for this instance.
    /// </summary>
    /// <returns>A hash code for the current object.</returns>
    public override int GetHashCode()
    {
        return Level.GetHashCode() ^ XpSettingsId.GetHashCode();
    }

    /// <summary>
    ///     Determines whether the specified object is equal to the current object.
    /// </summary>
    /// <param name="obj">The object to compare with the current object.</param>
    /// <returns>true if the specified object is equal to the current object; otherwise, false.</returns>
    public override bool Equals(object obj)
    {
        return obj is XpCurrencyReward xrr && xrr.Level == Level && xrr.XpSettingsId == XpSettingsId;
    }
}

/// <summary>
///     Represents an excluded item in a guild.
/// </summary>
public class ExcludedItem : DbEntity
{
    /// <summary>
    ///     Gets or sets the item ID.
    /// </summary>
    public ulong ItemId { get; set; }

    /// <summary>
    ///     Gets or sets the type of the item.
    /// </summary>
    public ExcludedItemType ItemType { get; set; }

    /// <summary>
    ///     Gets or sets the XP settings ID.
    /// </summary>
    [ForeignKey("XpSettingsId")]
    public int XpSettingsId { get; set; }

    /// <summary>
    ///     Returns the hash code for this instance.
    /// </summary>
    /// <returns>A hash code for the current object.</returns>
    public override int GetHashCode()
    {
        return ItemId.GetHashCode() ^ ItemType.GetHashCode();
    }

    /// <summary>
    ///     Determines whether the specified object is equal to the current object.
    /// </summary>
    /// <param name="obj">The object to compare with the current object.</param>
    /// <returns>true if the specified object is equal to the current object; otherwise, false.</returns>
    public override bool Equals(object obj)
    {
        return obj is ExcludedItem ei && ei.ItemId == ItemId && ei.ItemType == ItemType;
    }
}