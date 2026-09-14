namespace Mewdeko.Services.Analytics;

/// <summary>
///     One command or interaction execution handed to the collector.
/// </summary>
/// <param name="Kind">text, slash, user_ctx, msg_ctx, button, select, modal, autocomplete or trigger.</param>
/// <param name="Module">The module name, or null.</param>
/// <param name="Command">The command name.</param>
/// <param name="Ok">Whether it succeeded.</param>
/// <param name="ErrorClass">The error class when it failed.</param>
/// <param name="ErrorMessage">The error message when it failed.</param>
/// <param name="DurationMs">Execution time in milliseconds.</param>
/// <param name="AckMs">Time to first acknowledgement for interactions.</param>
/// <param name="GuildId">The guild, or null in direct messages.</param>
/// <param name="GuildSize">Member count of the guild.</param>
/// <param name="Shard">The shard the guild lives on.</param>
/// <param name="Language">The locale the command ran under.</param>
/// <param name="OptedOut">Whether the guild or user opted out of stats, which drops the raw row.</param>
public sealed record CommandSample(
    string Kind,
    string? Module,
    string Command,
    bool Ok,
    string? ErrorClass,
    string? ErrorMessage,
    long DurationMs,
    long? AckMs,
    ulong? GuildId,
    int? GuildSize,
    int? Shard,
    string? Language,
    bool OptedOut);

/// <summary>
///     One dashboard request reported by the dashboard beacon.
/// </summary>
/// <param name="At">When the request finished, UTC.</param>
/// <param name="Route">The route template.</param>
/// <param name="Method">The HTTP method.</param>
/// <param name="Status">The response status code.</param>
/// <param name="DurationMs">Server time to respond.</param>
/// <param name="VisitorHash">Daily salted visitor hash.</param>
/// <param name="Locale">The visitor's locale.</param>
/// <param name="Device">desktop, mobile, tablet or bot.</param>
/// <param name="GuildSize">Member count of the guild being viewed.</param>
/// <param name="IsOwner">Whether the visitor is a bot owner.</param>
public sealed record PageViewSample(
    DateTime At,
    string Route,
    string Method,
    int Status,
    int DurationMs,
    string? VisitorHash,
    string? Locale,
    string? Device,
    int? GuildSize,
    bool IsOwner);