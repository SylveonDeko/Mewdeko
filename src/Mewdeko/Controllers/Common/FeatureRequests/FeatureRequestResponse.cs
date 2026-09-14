namespace Mewdeko.Controllers.Common.FeatureRequests;

/// <summary>
///     A single feature request as returned to the dashboard.
/// </summary>
public class FeatureRequestEntryResponse
{
    /// <summary>
    ///     The request's unique id.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    ///     The submitting user.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    ///     The submitter's name at the time.
    /// </summary>
    public string UserName { get; set; } = "";

    /// <summary>
    ///     The guild the submitter was managing, if any.
    /// </summary>
    public ulong? GuildId { get; set; }

    /// <summary>
    ///     That guild's name at the time.
    /// </summary>
    public string? GuildName { get; set; }

    /// <summary>
    ///     The category key: feature, bug, or other.
    /// </summary>
    public string Category { get; set; } = "feature";

    /// <summary>
    ///     The short title.
    /// </summary>
    public string Title { get; set; } = "";

    /// <summary>
    ///     The full description.
    /// </summary>
    public string Body { get; set; } = "";

    /// <summary>
    ///     The status key: open, planned, done, or declined.
    /// </summary>
    public string Status { get; set; } = "open";

    /// <summary>
    ///     A note an owner left for the submitter.
    /// </summary>
    public string? OwnerNote { get; set; }

    /// <summary>
    ///     How many users upvoted the request.
    /// </summary>
    public int Votes { get; set; }

    /// <summary>
    ///     Whether the requesting dashboard user has upvoted it.
    /// </summary>
    public bool Voted { get; set; }

    /// <summary>
    ///     Whether the requesting dashboard user submitted it.
    /// </summary>
    public bool Mine { get; set; }

    /// <summary>
    ///     When the status or note last changed (UTC).
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    ///     When the request was submitted (UTC).
    /// </summary>
    public DateTime? DateAdded { get; set; }
}

/// <summary>
///     A page of feature requests plus the total count.
/// </summary>
public class FeatureRequestPageResponse
{
    /// <summary>
    ///     The requests on this page.
    /// </summary>
    public List<FeatureRequestEntryResponse> Items { get; set; } = [];

    /// <summary>
    ///     The total number of requests matching the filters.
    /// </summary>
    public int Total { get; set; }

    /// <summary>
    ///     The 1-based page number.
    /// </summary>
    public int Page { get; set; }

    /// <summary>
    ///     The page size.
    /// </summary>
    public int PageSize { get; set; }
}

/// <summary>
///     Counts of requests by status and category.
/// </summary>
public class FeatureRequestStatsResponse
{
    /// <summary>
    ///     The total number of requests.
    /// </summary>
    public int Total { get; set; }

    /// <summary>
    ///     Counts keyed by status.
    /// </summary>
    public Dictionary<string, int> ByStatus { get; set; } = new();

    /// <summary>
    ///     Counts keyed by category.
    /// </summary>
    public Dictionary<string, int> ByCategory { get; set; } = new();
}

/// <summary>
///     The bot wide feature request settings.
/// </summary>
public class FeatureRequestSettingsResponse
{
    /// <summary>
    ///     The configured report channel, or zero for the fallback.
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     The channel reports actually go to.
    /// </summary>
    public ulong EffectiveChannelId { get; set; }

    /// <summary>
    ///     Whether the join/leave channel fallback is in use.
    /// </summary>
    public bool UsingFallback { get; set; }

    /// <summary>
    ///     The effective channel's name, if reachable.
    /// </summary>
    public string? ChannelName { get; set; }

    /// <summary>
    ///     The effective channel's guild.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    ///     The effective channel's guild name.
    /// </summary>
    public string? GuildName { get; set; }

    /// <summary>
    ///     Whether the bot can see the effective channel.
    /// </summary>
    public bool Reachable { get; set; }
}

/// <summary>
///     The vote result after toggling.
/// </summary>
public class FeatureRequestVoteResponse
{
    /// <summary>
    ///     The new vote count.
    /// </summary>
    public int Votes { get; set; }

    /// <summary>
    ///     Whether the user now has a vote on the request.
    /// </summary>
    public bool Voted { get; set; }
}

/// <summary>
///     A new request from the dashboard.
/// </summary>
public class FeatureRequestSubmitRequest
{
    /// <summary>
    ///     The guild the user was managing, if any.
    /// </summary>
    public ulong? GuildId { get; set; }

    /// <summary>
    ///     The category key.
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    ///     The short title.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    ///     The full description.
    /// </summary>
    public string? Body { get; set; }
}

/// <summary>
///     A status change from a bot owner.
/// </summary>
public class FeatureRequestStatusRequest
{
    /// <summary>
    ///     The new status key.
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    ///     An optional note for the submitter.
    /// </summary>
    public string? Note { get; set; }
}

/// <summary>
///     A settings change from a bot owner.
/// </summary>
public class FeatureRequestSettingsRequest
{
    /// <summary>
    ///     The report channel, or zero for the fallback.
    /// </summary>
    public ulong ChannelId { get; set; }
}
