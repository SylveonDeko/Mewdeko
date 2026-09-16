namespace Mewdeko.Modules.StatRoles.Common;

/// <summary>
///     What a stat role condition measures.
/// </summary>
public enum StatRoleStat
{
    /// <summary>
    ///     Messages sent in the window.
    /// </summary>
    Messages = 0,

    /// <summary>
    ///     Minutes spent in voice in the window.
    /// </summary>
    VoiceMinutes = 1,

    /// <summary>
    ///     Net invites, all time.
    /// </summary>
    Invites = 2,

    /// <summary>
    ///     Days since the member joined the server.
    /// </summary>
    JoinedDays = 3,

    /// <summary>
    ///     Days since the account was created.
    /// </summary>
    AccountDays = 4,

    /// <summary>
    ///     Minutes spent in a game or app (one by name, or any) in the window.
    /// </summary>
    ActivityMinutes = 5
}

/// <summary>
///     How a stat role decides who qualifies.
/// </summary>
public enum StatRoleLimit
{
    /// <summary>
    ///     Members whose value is between the minimum and maximum.
    /// </summary>
    Threshold = 0,

    /// <summary>
    ///     Members ranked between the top start and top end.
    /// </summary>
    TopRank = 1,

    /// <summary>
    ///     Members whose rank falls in the top start to top end percent of ranked members.
    /// </summary>
    TopPercent = 2,

    /// <summary>
    ///     Members who met the per day minimum on at least the required number of days in the window.
    /// </summary>
    DailyStreak = 3
}

/// <summary>
///     Why a member's role changed, for notifications.
/// </summary>
public enum StatRoleAction
{
    /// <summary>
    ///     The role was granted.
    /// </summary>
    Granted = 0,

    /// <summary>
    ///     The role was removed.
    /// </summary>
    Removed = 1
}