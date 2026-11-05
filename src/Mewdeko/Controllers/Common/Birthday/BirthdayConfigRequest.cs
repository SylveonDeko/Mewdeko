namespace Mewdeko.Controllers.Common.Birthday;

/// <summary>
///     Request model for updating birthday configuration.
/// </summary>
public class BirthdayConfigRequest
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
    public string? BirthdayMessage { get; set; }

    /// <summary>
    ///     The role ID to ping for birthday announcements.
    /// </summary>
    public ulong? BirthdayPingRoleId { get; set; }

    /// <summary>
    ///     Number of days before birthday to send reminders.
    /// </summary>
    public int? BirthdayReminderDays { get; set; }

    /// <summary>
    ///     Default timezone for the guild.
    /// </summary>
    public string? DefaultTimezone { get; set; }

    /// <summary>
    ///     The hour of the day, 0 to 23 in the server timezone, birthdays are announced.
    /// </summary>
    public int? AnnouncementHour { get; set; }

    /// <summary>
    ///     The message announced for members who shared their birth year. An empty string clears it.
    /// </summary>
    public string? AgeMessage { get; set; }

    /// <summary>
    ///     Whether birth years are not allowed (0), optional (1) or required (2).
    /// </summary>
    public int? BirthYearRule { get; set; }
}