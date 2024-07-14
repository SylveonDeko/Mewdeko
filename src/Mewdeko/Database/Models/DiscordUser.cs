#nullable enable
namespace Mewdeko.Database.Models;

/// <summary>
/// Represents a Discord user in the database.
/// </summary>
public class DiscordUser : DbEntity
{
    /// <summary>
    /// Specifies the display mode for birthdays.
    /// </summary>
    public enum BirthdayDisplayModeEnum
    {
        /// <summary>
        /// Default display mode.
        /// </summary>
        Default,
        /// <summary>
        /// Display only the month.
        /// </summary>
        MonthOnly,
        /// <summary>
        /// Display only the year.
        /// </summary>
        YearOnly,
        /// <summary>
        /// Display both the month and date.
        /// </summary>
        MonthAndDate,
        /// <summary>
        /// Birthday display is disabled.
        /// </summary>
        Disabled
    }

    /// <summary>
    /// Specifies the privacy level for user profiles.
    /// </summary>
    public enum ProfilePrivacyEnum
    {
        /// <summary>
        /// Marks the profile as viewable by everyone.
        /// </summary>
        Public = 0,
        /// <summary>
        /// Makes it so only the user who owns the profile can view the profile
        /// </summary>
        Private = 1
    }

    /// <summary>
    /// Gets or sets the Discord user ID.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    /// Gets or sets the username.
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Gets or sets the discriminator.
    /// </summary>
    public string? Discriminator { get; set; }

    /// <summary>
    /// Gets or sets the avatar ID.
    /// </summary>
    public string? AvatarId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user is a club admin.
    /// </summary>
    public bool IsClubAdmin { get; set; } = false;

    /// <summary>
    /// Gets or sets the total XP of the user.
    /// </summary>
    public int TotalXp { get; set; }

    /// <summary>
    /// Gets or sets the date and time of the last level up.
    /// </summary>
    public DateTime? LastLevelUp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the XP notification location preference.
    /// </summary>
    public XpNotificationLocation NotifyOnLevelUp { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user is a dragon.
    /// </summary>
    public bool IsDragon { get; set; } = false;

    /// <summary>
    /// Gets or sets the user's pronouns.
    /// </summary>
    public string? Pronouns { get; set; }

    /// <summary>
    /// Gets or sets the reason for cleared pronouns.
    /// </summary>
    public string? PronounsClearedReason { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether pronouns are disabled.
    /// </summary>
    public bool PronounsDisabled { get; set; } = false;

    /// <summary>
    /// Gets or sets the user's bio.
    /// </summary>
    public string? Bio { get; set; }

    /// <summary>
    /// Gets or sets the URL of the user's profile image.
    /// </summary>
    public string? ProfileImageUrl { get; set; }

    /// <summary>
    /// Gets or sets the user's zodiac sign.
    /// </summary>
    public string? ZodiacSign { get; set; }

    /// <summary>
    /// Gets or sets the privacy setting for the user's profile.
    /// </summary>
    public ProfilePrivacyEnum ProfilePrivacy { get; set; } = ProfilePrivacyEnum.Public;

    /// <summary>
    /// Gets or sets the color of the user's profile.
    /// </summary>
    public uint? ProfileColor { get; set; } = 0;

    /// <summary>
    /// Gets or sets the user's birthday.
    /// </summary>
    public DateTime? Birthday { get; set; }

    /// <summary>
    /// Gets or sets the user's Nintendo Switch friend code.
    /// </summary>
    public string? SwitchFriendCode { get; set; } = null;

    /// <summary>
    /// Gets or sets the display mode for the user's birthday.
    /// </summary>
    public BirthdayDisplayModeEnum BirthdayDisplayMode { get; set; } = BirthdayDisplayModeEnum.Default;

    /// <summary>
    /// Gets or sets a value indicating whether the user has opted out of stats tracking.
    /// </summary>
    public bool StatsOptOut { get; set; } = false;

    /// <summary>
    /// Returns the hash code for this instance.
    /// </summary>
    /// <returns>A hash code for the current object.</returns>
    public override int GetHashCode() => UserId.GetHashCode();

    /// <summary>
    /// Returns a string? that represents the current object.
    /// </summary>
    /// <returns>A string? that represents the current object.</returns>
    public override string ToString() => $"{Username}#{Discriminator}";
}