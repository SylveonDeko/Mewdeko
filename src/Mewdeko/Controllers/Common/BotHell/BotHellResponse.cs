namespace Mewdeko.Controllers.Common.BotHell;

/// <summary>
///     One server as evaluated against the bot hell thresholds.
/// </summary>
public class BotHellEntryResponse
{
    /// <summary>
    ///     The server id.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    ///     The server name.
    /// </summary>
    public string GuildName { get; set; } = "";

    /// <summary>
    ///     The server icon url, if it has one.
    /// </summary>
    public string? IconUrl { get; set; }

    /// <summary>
    ///     The server owner's id.
    /// </summary>
    public ulong OwnerId { get; set; }

    /// <summary>
    ///     The total member count.
    /// </summary>
    public int Total { get; set; }

    /// <summary>
    ///     How many cached members are humans.
    /// </summary>
    public int Humans { get; set; }

    /// <summary>
    ///     How many cached members are bots.
    /// </summary>
    public int Bots { get; set; }

    /// <summary>
    ///     The percentage of members that are bots.
    /// </summary>
    public int Percent { get; set; }

    /// <summary>
    ///     Whether the server met a threshold.
    /// </summary>
    public bool IsBotHell { get; set; }

    /// <summary>
    ///     Whether the bot count threshold was met.
    /// </summary>
    public bool ByCount { get; set; }

    /// <summary>
    ///     Whether the bot percentage threshold was met.
    /// </summary>
    public bool ByPercent { get; set; }

    /// <summary>
    ///     Whether the member list was fully downloaded when counted. Partial caches undercount bots.
    /// </summary>
    public bool Complete { get; set; }

    /// <summary>
    ///     When the bot joined the server, if known.
    /// </summary>
    public DateTime? JoinedAt { get; set; }
}

/// <summary>
///     The full server listing plus the thresholds it was evaluated with.
/// </summary>
public class BotHellListResponse
{
    /// <summary>
    ///     Every server the bot is in, flagged ones first.
    /// </summary>
    public IReadOnlyList<BotHellEntryResponse> Items { get; set; } = [];

    /// <summary>
    ///     How many servers are flagged.
    /// </summary>
    public int Flagged { get; set; }

    /// <summary>
    ///     The thresholds used for the evaluation.
    /// </summary>
    public BotHellSettingsResponse Settings { get; set; } = new();
}

/// <summary>
///     The bot wide bot hell settings, with the effective report channel resolved.
/// </summary>
public class BotHellSettingsResponse
{
    /// <summary>
    ///     Servers smaller than this are never flagged.
    /// </summary>
    public int MinMembers { get; set; }

    /// <summary>
    ///     Bots at or above this flag the server, zero disables the check.
    /// </summary>
    public int BotCount { get; set; }

    /// <summary>
    ///     Bot percentage at or above this flags the server, zero disables the check.
    /// </summary>
    public int BotPercent { get; set; }

    /// <summary>
    ///     Whether flagged servers are left automatically on join.
    /// </summary>
    public bool AutoLeave { get; set; }

    /// <summary>
    ///     The configured report channel, zero when the join/leave channel is used.
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     The channel reports actually go to after the fallback.
    /// </summary>
    public ulong EffectiveChannelId { get; set; }

    /// <summary>
    ///     Whether the effective channel came from the join/leave fallback.
    /// </summary>
    public bool UsingFallback { get; set; }

    /// <summary>
    ///     The effective channel's name, if resolvable.
    /// </summary>
    public string? ChannelName { get; set; }

    /// <summary>
    ///     The guild the effective channel is in.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    ///     The name of the guild the effective channel is in.
    /// </summary>
    public string? GuildName { get; set; }

    /// <summary>
    ///     Whether the bot can post to the effective channel.
    /// </summary>
    public bool Reachable { get; set; }
}

/// <summary>
///     The settings the dashboard sends when saving.
/// </summary>
public class BotHellSettingsRequest
{
    /// <summary>
    ///     Servers smaller than this are never flagged.
    /// </summary>
    public int MinMembers { get; set; }

    /// <summary>
    ///     Bots at or above this flag the server, zero disables the check.
    /// </summary>
    public int BotCount { get; set; }

    /// <summary>
    ///     Bot percentage at or above this flags the server, zero disables the check.
    /// </summary>
    public int BotPercent { get; set; }

    /// <summary>
    ///     Whether flagged servers are left automatically on join.
    /// </summary>
    public bool AutoLeave { get; set; }

    /// <summary>
    ///     The report channel, zero for the join/leave channel.
    /// </summary>
    public ulong ChannelId { get; set; }
}

/// <summary>
///     The servers a bulk action should apply to.
/// </summary>
public class BotHellBulkRequest
{
    /// <summary>
    ///     The server ids.
    /// </summary>
    public List<ulong> GuildIds { get; set; } = [];
}

/// <summary>
///     The outcome of a bulk leave.
/// </summary>
public class BotHellLeaveResponse
{
    /// <summary>
    ///     The servers that were left.
    /// </summary>
    public List<ulong> Left { get; set; } = [];

    /// <summary>
    ///     The servers that could not be left, with the reason.
    /// </summary>
    public Dictionary<ulong, string> Failed { get; set; } = [];
}
