namespace Mewdeko.Modules.Moderation.Common;

/// <summary>
///     One moderation action that bans, and therefore has a message purge attached to it.
/// </summary>
/// <param name="Key">The stable identifier stored in the database and typed by users.</param>
/// <param name="DisplayName">The name shown in command output and on the dashboard.</param>
/// <param name="DefaultDays">The purge used when neither the action nor the guild has one configured.</param>
public sealed record BanPruneAction(string Key, string DisplayName, int DefaultDays)
{
    /// <summary>
    ///     A regular ban issued through the ban command.
    /// </summary>
    public static readonly BanPruneAction Ban = new("ban", "Ban", 7);

    /// <summary>
    ///     A ban by user id, where the target need not be in the server.
    /// </summary>
    public static readonly BanPruneAction HackBan = new("hackban", "Ban by ID", 7);

    /// <summary>
    ///     A ban that lifts itself after a set duration.
    /// </summary>
    public static readonly BanPruneAction TempBan = new("tempban", "Temporary ban", 0);

    /// <summary>
    ///     A ban immediately followed by an unban, used to purge a member's messages.
    /// </summary>
    public static readonly BanPruneAction SoftBan = new("softban", "Softban", 7);

    /// <summary>
    ///     A bulk ban issued through the mass ban command.
    /// </summary>
    public static readonly BanPruneAction MassBan = new("massban", "Mass ban", 7);

    /// <summary>
    ///     Banning everyone sharing an avatar hash.
    /// </summary>
    public static readonly BanPruneAction BanByHash = new("banbyhash", "Ban by avatar hash", 0);

    /// <summary>
    ///     Banning every member holding a role.
    /// </summary>
    public static readonly BanPruneAction BanInRole = new("baninrole", "Ban in role", 0);

    /// <summary>
    ///     Banning every account created or joined under an age threshold.
    /// </summary>
    public static readonly BanPruneAction BanUnder = new("banunder", "Ban under age", 0);

    /// <summary>
    ///     A ban handed out automatically once a member passes the warn threshold.
    /// </summary>
    public static readonly BanPruneAction WarnPunishment = new("warnpunish", "Warn punishment", 0);

    /// <summary>
    ///     A ban triggered by a member gaining an auto-ban role.
    /// </summary>
    public static readonly BanPruneAction AutoBanRole = new("autobanrole", "Auto-ban role", 0);

    /// <summary>
    ///     A ban triggered by the role monitor catching a permission violation.
    /// </summary>
    public static readonly BanPruneAction RoleMonitor = new("rolemonitor", "Role monitor", 0);

    /// <summary>
    ///     A ban triggered by a word, link, or invite filter.
    /// </summary>
    public static readonly BanPruneAction Filter = new("filter", "Filter", 0);

    /// <summary>
    ///     A ban issued while the server is locked down.
    /// </summary>
    public static readonly BanPruneAction Lockdown = new("lockdown", "Lockdown", 0);

    /// <summary>
    ///     A ban issued from the dashboard or the mobile app.
    /// </summary>
    public static readonly BanPruneAction Dashboard = new("dashboard", "Dashboard", 0);

    /// <summary>
    ///     Every configurable action, in the order they are listed to users.
    /// </summary>
    public static readonly IReadOnlyList<BanPruneAction> All =
    [
        Ban, HackBan, TempBan, SoftBan, MassBan, BanByHash, BanInRole, BanUnder,
        WarnPunishment, AutoBanRole, RoleMonitor, Filter, Lockdown, Dashboard
    ];

    private static readonly Dictionary<string, BanPruneAction> ByKey =
        All.ToDictionary(x => x.Key, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     Looks up an action by its key.
    /// </summary>
    /// <param name="key">The key to resolve, case insensitive.</param>
    /// <returns>The matching action, or null when the key is unknown.</returns>
    public static BanPruneAction? FromKey(string? key)
    {
        return key is not null && ByKey.TryGetValue(key, out var action) ? action : null;
    }
}