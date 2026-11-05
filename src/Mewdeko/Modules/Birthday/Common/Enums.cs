namespace Mewdeko.Modules.Birthday.Common;

/// <summary>
///     Specifies the birthday features that can be enabled for a guild.
/// </summary>
[Flags]
public enum BirthdayFeature
{
    /// <summary>
    ///     No birthday features enabled.
    /// </summary>
    None = 0,

    /// <summary>
    ///     Enable birthday announcements in the configured channel.
    /// </summary>
    Announcements = 1,

    /// <summary>
    ///     Enable temporary birthday role assignment.
    /// </summary>
    BirthdayRole = 2,

    /// <summary>
    ///     Enable birthday reminders for users.
    /// </summary>
    Reminders = 4,

    /// <summary>
    ///     Enable pinging a role when announcing birthdays.
    /// </summary>
    PingRole = 8,

    /// <summary>
    ///     Enable timezone support for accurate birthday detection.
    /// </summary>
    TimezoneSupport = 16
}

/// <summary>
///     Whether members of a guild may share their birth year.
/// </summary>
public enum BirthYearRule
{
    /// <summary>
    ///     The birth year is never shown and ages are never announced.
    /// </summary>
    NotAllowed = 0,

    /// <summary>
    ///     Members choose whether to share their birth year.
    /// </summary>
    Optional = 1,

    /// <summary>
    ///     Only members who share their birth year are announced.
    /// </summary>
    Required = 2
}