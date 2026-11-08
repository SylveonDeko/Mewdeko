namespace Mewdeko.Controllers.Common.UserSettings;

/// <summary>
///     Request model for updating user profile settings
/// </summary>
public class UserProfileRequest
{
    /// <summary>
    ///     User biography
    /// </summary>
    public string? Bio { get; set; }

    /// <summary>
    ///     Zodiac sign
    /// </summary>
    public string? ZodiacSign { get; set; }

    /// <summary>
    ///     Profile privacy level
    /// </summary>
    public int? ProfilePrivacy { get; set; }

    /// <summary>
    ///     Birthday display mode
    /// </summary>
    public int? BirthdayDisplayMode { get; set; }

    /// <summary>
    ///     Whether to opt out of greet DMs
    /// </summary>
    public bool? GreetDmsOptOut { get; set; }

    /// <summary>
    ///     Whether to stop members leaving messages while AFK
    /// </summary>
    public bool? AfkMessagesOptOut { get; set; }

    /// <summary>
    ///     Whether to opt out of stats collection
    /// </summary>
    public bool? StatsOptOut { get; set; }

    /// <summary>
    ///     User birthday
    /// </summary>
    public DateTime? Birthday { get; set; }

    /// <summary>
    ///     Birthday timezone
    /// </summary>
    public string? BirthdayTimezone { get; set; }

    /// <summary>
    ///     Whether birthday announcements are enabled
    /// </summary>
    public bool? BirthdayAnnouncementsEnabled { get; set; }

    /// <summary>
    ///     Profile color (Discord color value)
    /// </summary>
    public uint? ProfileColor { get; set; }

    /// <summary>
    ///     Profile image URL
    /// </summary>
    public string? ProfileImageUrl { get; set; }

    /// <summary>
    ///     Nintendo Switch friend code
    /// </summary>
    public string? SwitchFriendCode { get; set; }

    /// <summary>
    ///     User pronouns
    /// </summary>
    public string? Pronouns { get; set; }
}