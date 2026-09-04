namespace Mewdeko.Controllers.Common.LeaveFeedback;

/// <summary>
///     A single leave feedback record as returned to the dashboard.
/// </summary>
public class LeaveFeedbackEntryResponse
{
    /// <summary>
    ///     The record's unique id.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    ///     The guild the bot was removed from.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    ///     The guild's name at the time of removal.
    /// </summary>
    public string GuildName { get; set; } = "";

    /// <summary>
    ///     How many members the guild had at the time of removal.
    /// </summary>
    public int MemberCount { get; set; }

    /// <summary>
    ///     The owner the prompt was sent to.
    /// </summary>
    public ulong OwnerId { get; set; }

    /// <summary>
    ///     When the bot joined the guild, if known (UTC).
    /// </summary>
    public DateTime? JoinedAt { get; set; }

    /// <summary>
    ///     The reason key the owner selected, if any.
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    ///     The human readable label for <see cref="Reason" />.
    /// </summary>
    public string? ReasonLabel { get; set; }

    /// <summary>
    ///     The free text the owner wrote, if any.
    /// </summary>
    public string? Comment { get; set; }

    /// <summary>
    ///     Whether the owner dismissed the prompt without answering.
    /// </summary>
    public bool Dismissed { get; set; }

    /// <summary>
    ///     When the owner answered or dismissed the prompt (UTC).
    /// </summary>
    public DateTime? AnsweredAt { get; set; }

    /// <summary>
    ///     When the prompt was sent, which is also when the bot was removed (UTC).
    /// </summary>
    public DateTime? DateAdded { get; set; }
}

/// <summary>
///     A page of leave feedback records plus the total count for pagination.
/// </summary>
public class LeaveFeedbackPageResponse
{
    /// <summary>
    ///     The records on this page, newest first.
    /// </summary>
    public IReadOnlyList<LeaveFeedbackEntryResponse> Items { get; set; } = [];

    /// <summary>
    ///     The total number of records matching the filters across all pages.
    /// </summary>
    public int Total { get; set; }

    /// <summary>
    ///     The page number returned (1-based).
    /// </summary>
    public int Page { get; set; }

    /// <summary>
    ///     The page size used.
    /// </summary>
    public int PageSize { get; set; }
}

/// <summary>
///     One reason key and its human readable label, for building filters in the dashboard.
/// </summary>
public class LeaveFeedbackReasonResponse
{
    /// <summary>
    ///     The stored reason key.
    /// </summary>
    public string Key { get; set; } = "";

    /// <summary>
    ///     The human readable label.
    /// </summary>
    public string Label { get; set; } = "";

    /// <summary>
    ///     How many records have this reason.
    /// </summary>
    public int Count { get; set; }
}

/// <summary>
///     Where leave feedback answers get posted and whether owners are asked at all.
/// </summary>
public class LeaveFeedbackSettingsResponse
{
    /// <summary>
    ///     Whether owners get a dm asking why the bot was removed.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    ///     The configured report channel, or 0 when the join/leave channel is used instead.
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     The channel answers actually get posted to, after the fallback is applied.
    /// </summary>
    public ulong EffectiveChannelId { get; set; }

    /// <summary>
    ///     Whether the effective channel comes from the join/leave channel fallback.
    /// </summary>
    public bool UsingFallback { get; set; }

    /// <summary>
    ///     The name of the effective channel, when the bot can see it.
    /// </summary>
    public string? ChannelName { get; set; }

    /// <summary>
    ///     The guild the effective channel belongs to, when the bot can see it.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    ///     The name of the guild the effective channel belongs to, when the bot can see it.
    /// </summary>
    public string? GuildName { get; set; }

    /// <summary>
    ///     Whether the bot could resolve the effective channel and can post to it.
    /// </summary>
    public bool Reachable { get; set; }
}

/// <summary>
///     An update to the leave feedback settings.
/// </summary>
public class LeaveFeedbackSettingsRequest
{
    /// <summary>
    ///     Whether owners get a dm asking why the bot was removed.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    ///     The channel answers get posted to. Zero falls back to the join/leave channel.
    /// </summary>
    public ulong ChannelId { get; set; }
}

/// <summary>
///     Aggregate counts across all collected leave feedback.
/// </summary>
public class LeaveFeedbackStatsResponse
{
    /// <summary>
    ///     Total prompts sent.
    /// </summary>
    public int Total { get; set; }

    /// <summary>
    ///     Prompts that got a reason selected and were not dismissed.
    /// </summary>
    public int Answered { get; set; }

    /// <summary>
    ///     Prompts the owner explicitly dismissed.
    /// </summary>
    public int Dismissed { get; set; }

    /// <summary>
    ///     Prompts that were never acted on.
    /// </summary>
    public int Pending { get; set; }

    /// <summary>
    ///     Answers that included written feedback.
    /// </summary>
    public int WithComment { get; set; }

    /// <summary>
    ///     Every reason with its label and count, in display order.
    /// </summary>
    public IReadOnlyList<LeaveFeedbackReasonResponse> Reasons { get; set; } = [];
}