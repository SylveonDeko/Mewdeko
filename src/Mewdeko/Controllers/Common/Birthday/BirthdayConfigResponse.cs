namespace Mewdeko.Controllers.Common.Birthday;

/// <summary>
///     Response model for birthday configuration.
/// </summary>
public class BirthdayConfigResponse
{
    /// <summary>
    ///     The channel ID for birthday announcements.
    /// </summary>
    public ulong? BirthdayChannelId { get; set; }

    /// <summary>
    ///     The role ID to assign on birthdays.
    /// </summary>
    public ulong? BirthdayRoleId { get; set; }

    /// <summary>
    ///     The birthday message template.
    /// </summary>
    public string BirthdayMessage { get; set; } = null!;

    /// <summary>
    ///     The role ID to ping for birthday announcements.
    /// </summary>
    public ulong? BirthdayPingRoleId { get; set; }

    /// <summary>
    ///     Number of days before birthday to send reminders.
    /// </summary>
    public int BirthdayReminderDays { get; set; }

    /// <summary>
    ///     Default timezone for the guild.
    /// </summary>
    public string DefaultTimezone { get; set; } = null!;

    /// <summary>
    ///     Enabled birthday features.
    /// </summary>
    public int EnabledFeatures { get; set; }

    /// <summary>
    ///     The hour of the day, 0 to 23 in the server timezone, birthdays are announced.
    /// </summary>
    public int AnnouncementHour { get; set; }

    /// <summary>
    ///     The message announced for members who shared their birth year.
    /// </summary>
    public string? AgeMessage { get; set; }

    /// <summary>
    ///     Whether birth years are not allowed (0), optional (1) or required (2).
    /// </summary>
    public int BirthYearRule { get; set; }

    /// <summary>
    ///     Date when configuration was first created.
    /// </summary>
    public DateTime? DateAdded { get; set; }

    /// <summary>
    ///     Date when configuration was last modified.
    /// </summary>
    public DateTime? DateModified { get; set; }
}