using DataModel;
using Discord.Rest;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Services.Analytics;
using Mewdeko.Services.Impl;
using Mewdeko.Services.Settings;

namespace Mewdeko.Modules.OwnerOnly.Services;

/// <summary>
///     Stores feature requests submitted from the dashboard, posts them to a report channel for
///     the bot owners, and tracks upvotes and status changes.
/// </summary>
public class FeatureRequestService : INService
{
    /// <summary>
    ///     The category keys accepted from the dashboard.
    /// </summary>
    public static readonly IReadOnlyList<string> Categories = ["feature", "bug", "other"];

    /// <summary>
    ///     The status keys an owner can set.
    /// </summary>
    public static readonly IReadOnlyList<string> Statuses = ["open", "planned", "done", "declined"];

    private const int MaxTitleLength = 120;
    private const int MaxBodyLength = 2000;
    private const int MaxNoteLength = 1000;
    private const int SubmissionsPerWindow = 3;
    private static readonly TimeSpan SubmissionWindow = TimeSpan.FromHours(1);

    private readonly BotConfigService bss;
    private readonly DiscordShardedClient client;
    private readonly IAnalyticsCollector collector;
    private readonly BotCredentials creds;
    private readonly IDataConnectionFactory dbFactory;
    private readonly ILogger<FeatureRequestService> logger;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<ulong, Queue<DateTime>> recentSubmissions =
        new();

    /// <summary>
    ///     Initializes a new instance of <see cref="FeatureRequestService" />.
    /// </summary>
    /// <param name="client">The discord client.</param>
    /// <param name="dbFactory">The database connection factory.</param>
    /// <param name="bss">The bot config service.</param>
    /// <param name="creds">The bot credentials.</param>
    /// <param name="collector">The analytics collector.</param>
    /// <param name="logger">The logger.</param>
    public FeatureRequestService(DiscordShardedClient client, IDataConnectionFactory dbFactory,
        BotConfigService bss, BotCredentials creds, IAnalyticsCollector collector,
        ILogger<FeatureRequestService> logger)
    {
        this.client = client;
        this.dbFactory = dbFactory;
        this.bss = bss;
        this.creds = creds;
        this.collector = collector;
        this.logger = logger;
    }

    /// <summary>
    ///     The channel configured for reports, or zero when the join/leave channel is used instead.
    /// </summary>
    public ulong ConfiguredChannelId
    {
        get
        {
            return bss.Data.FeatureRequestChannel;
        }
    }

    /// <summary>
    ///     The channel reports actually get posted to, after the join/leave channel fallback.
    /// </summary>
    public ulong ReportChannelId
    {
        get
        {
            var configured = bss.Data.FeatureRequestChannel;
            return configured is not 0 ? configured : creds.GuildJoinsChannelId;
        }
    }

    /// <summary>
    ///     Validates and stores a new request, then posts it to the report channel.
    /// </summary>
    /// <param name="userId">The submitting user.</param>
    /// <param name="guildId">The guild they were managing, if any.</param>
    /// <param name="category">The category key.</param>
    /// <param name="title">The short title.</param>
    /// <param name="body">The full description.</param>
    /// <returns>The stored request, or an error message the dashboard can show.</returns>
    public async Task<(FeatureRequest? Request, string? Error)> SubmitAsync(ulong userId, ulong? guildId,
        string? category, string? title, string? body)
    {
        title = title?.Trim() ?? string.Empty;
        body = body?.Trim() ?? string.Empty;
        category = category?.Trim().ToLowerInvariant() ?? "feature";

        if (title.Length is < 3 or > MaxTitleLength)
            return (null, $"The title needs to be between 3 and {MaxTitleLength} characters.");
        if (body.Length is < 10 or > MaxBodyLength)
            return (null, $"The description needs to be between 10 and {MaxBodyLength} characters.");
        if (!Categories.Contains(category))
            return (null, "Unknown category.");
        if (!AllowSubmission(userId))
            return (null, $"You can submit {SubmissionsPerWindow} requests per hour. Try again later.");

        var user = client.GetUser(userId);
        var guild = guildId is { } id ? client.GetGuild(id) : null;

        var request = new FeatureRequest
        {
            UserId = userId,
            UserName = user?.ToString() ?? userId.ToString(),
            GuildId = guildId,
            GuildName = guild?.Name,
            Category = category,
            Title = title,
            Body = body,
            Status = "open",
            DateAdded = DateTime.UtcNow
        };

        await using var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false);
        request.Id = await db.InsertWithInt32IdentityAsync(request).ConfigureAwait(false);

        collector.Counter("feature_request.count", 1, ("category", category));

        _ = Task.Run(() => ReportAsync(request.Id));
        return (request, null);
    }

    /// <summary>
    ///     Returns a page of requests, newest or most voted first.
    /// </summary>
    /// <param name="status">Optional status filter.</param>
    /// <param name="category">Optional category filter.</param>
    /// <param name="search">Optional case insensitive search across title, body, and submitter.</param>
    /// <param name="sort">votes or newest.</param>
    /// <param name="page">The 1-based page number.</param>
    /// <param name="pageSize">The page size.</param>
    /// <param name="viewerId">The viewer, so their own votes can be flagged.</param>
    /// <returns>The page, the total, and the ids the viewer has voted for.</returns>
    public async Task<(List<FeatureRequest> Items, int Total, HashSet<int> VotedIds)> GetPageAsync(string? status,
        string? category, string? search, string? sort, int page, int pageSize, ulong viewerId)
    {
        await using var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false);

        var query = db.FeatureRequests.AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(x => x.Status == status);
        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(x => x.Category == category);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = $"%{search.ToLowerInvariant()}%";
            query = query.Where(x =>
                Sql.Like(x.Title.ToLower(), term) || Sql.Like(x.Body.ToLower(), term) ||
                Sql.Like(x.UserName.ToLower(), term));
        }

        var total = await query.CountAsync().ConfigureAwait(false);

        if (page < 1)
            page = 1;
        pageSize = Math.Clamp(pageSize, 1, 200);

        var ordered = sort == "newest"
            ? query.OrderByDescending(x => x.DateAdded).ThenByDescending(x => x.Id)
            : query.OrderByDescending(x => x.Votes).ThenByDescending(x => x.DateAdded).ThenByDescending(x => x.Id);

        var items = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync().ConfigureAwait(false);

        var ids = items.Select(x => x.Id).ToList();
        var voted = ids.Count == 0
            ? []
            : await db.FeatureRequestVotes
                .Where(x => x.UserId == viewerId && ids.Contains(x.RequestId))
                .Select(x => x.RequestId)
                .ToListAsync().ConfigureAwait(false);

        return (items, total, voted.ToHashSet());
    }

    /// <summary>
    ///     Returns everything one user has submitted, newest first, along with which of those
    ///     requests the same user has upvoted (a user can vote on their own request).
    /// </summary>
    /// <param name="userId">The submitter.</param>
    public async Task<(List<FeatureRequest> Items, HashSet<int> VotedIds)> GetMineAsync(ulong userId)
    {
        await using var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false);
        var items = await db.FeatureRequests
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.DateAdded)
            .ToListAsync().ConfigureAwait(false);

        var ids = items.Select(x => x.Id).ToList();
        var voted = ids.Count == 0
            ? []
            : await db.FeatureRequestVotes
                .Where(x => x.UserId == userId && ids.Contains(x.RequestId))
                .Select(x => x.RequestId)
                .ToListAsync().ConfigureAwait(false);

        return (items, voted.ToHashSet());
    }

    /// <summary>
    ///     Adds or removes the user's upvote.
    /// </summary>
    /// <param name="id">The request id.</param>
    /// <param name="userId">The voting user.</param>
    /// <returns>The new vote count and whether the user now has a vote on it, or null if the request is missing.</returns>
    public async Task<(int Votes, bool Voted)?> ToggleVoteAsync(int id, ulong userId)
    {
        await using var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false);

        if (!await db.FeatureRequests.AnyAsync(x => x.Id == id).ConfigureAwait(false))
            return null;

        var removed = await db.FeatureRequestVotes
            .Where(x => x.RequestId == id && x.UserId == userId)
            .DeleteAsync().ConfigureAwait(false);

        var voted = false;
        if (removed == 0)
        {
            await db.InsertAsync(new FeatureRequestVote
            {
                RequestId = id, UserId = userId, DateAdded = DateTime.UtcNow
            }).ConfigureAwait(false);
            voted = true;
        }

        var votes = await db.FeatureRequestVotes.CountAsync(x => x.RequestId == id).ConfigureAwait(false);
        await db.FeatureRequests.Where(x => x.Id == id)
            .Set(x => x.Votes, votes)
            .UpdateAsync().ConfigureAwait(false);

        return (votes, voted);
    }

    /// <summary>
    ///     Sets the status and owner note on a request and updates the report message.
    /// </summary>
    /// <param name="id">The request id.</param>
    /// <param name="status">The new status key.</param>
    /// <param name="note">An optional note for the submitter.</param>
    /// <returns>The updated request, or an error message.</returns>
    public async Task<(FeatureRequest? Request, string? Error)> SetStatusAsync(int id, string? status, string? note)
    {
        status = status?.Trim().ToLowerInvariant() ?? string.Empty;
        note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();

        if (!Statuses.Contains(status))
            return (null, "Unknown status.");
        if (note is { Length: > MaxNoteLength })
            return (null, $"The note can be at most {MaxNoteLength} characters.");

        await using var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false);
        var changed = await db.FeatureRequests.Where(x => x.Id == id)
            .Set(x => x.Status, status)
            .Set(x => x.OwnerNote, note)
            .Set(x => x.UpdatedAt, DateTime.UtcNow)
            .UpdateAsync().ConfigureAwait(false);

        if (changed == 0)
            return (null, "That request no longer exists.");

        var request = await db.FeatureRequests.FirstAsync(x => x.Id == id).ConfigureAwait(false);
        _ = Task.Run(() => ReportAsync(id));
        return (request, null);
    }

    /// <summary>
    ///     Deletes a request and its votes.
    /// </summary>
    /// <param name="id">The request id.</param>
    /// <returns>Whether a request was deleted.</returns>
    public async Task<bool> DeleteAsync(int id)
    {
        await using var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false);
        return await db.FeatureRequests.Where(x => x.Id == id).DeleteAsync().ConfigureAwait(false) > 0;
    }

    /// <summary>
    ///     Counts requests by status and by category.
    /// </summary>
    public async Task<(int Total, Dictionary<string, int> ByStatus, Dictionary<string, int> ByCategory)>
        GetStatsAsync()
    {
        await using var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false);

        var all = await db.FeatureRequests
            .Select(x => new
            {
                x.Status, x.Category
            })
            .ToListAsync().ConfigureAwait(false);

        return (all.Count,
            all.GroupBy(x => x.Status).ToDictionary(x => x.Key, x => x.Count()),
            all.GroupBy(x => x.Category).ToDictionary(x => x.Key, x => x.Count()));
    }

    /// <summary>
    ///     Updates the report channel.
    /// </summary>
    /// <param name="channelId">The channel id, or zero for the join/leave channel fallback.</param>
    public void UpdateSettings(ulong channelId)
    {
        bss.ModifyConfig(config => config.FeatureRequestChannel = channelId);
    }

    /// <summary>
    ///     Resolves a report channel id to something readable for the dashboard.
    /// </summary>
    /// <param name="channelId">The channel id.</param>
    public async Task<(string? ChannelName, ulong GuildId, string? GuildName, bool Reachable)> DescribeChannelAsync(
        ulong channelId)
    {
        if (channelId is 0)
            return (null, 0, null, false);

        try
        {
            if (await client.Rest.GetChannelAsync(channelId).ConfigureAwait(false) is not RestTextChannel channel)
                return (null, 0, null, false);

            return (channel.Name, channel.GuildId, client.GetGuild(channel.GuildId)?.Name, true);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to resolve feature request channel {ChannelId}", channelId);
            return (null, 0, null, false);
        }
    }

    private bool AllowSubmission(ulong userId)
    {
        var now = DateTime.UtcNow;
        var queue = recentSubmissions.GetOrAdd(userId, static _ => new Queue<DateTime>());
        lock (queue)
        {
            while (queue.Count > 0 && now - queue.Peek() > SubmissionWindow)
                queue.Dequeue();
            if (queue.Count >= SubmissionsPerWindow)
                return false;
            queue.Enqueue(now);
            return true;
        }
    }

    private async Task ReportAsync(int id)
    {
        var channelId = ReportChannelId;
        if (channelId is 0)
            return;

        try
        {
            await using var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false);
            var record = await db.FeatureRequests.FirstOrDefaultAsync(x => x.Id == id).ConfigureAwait(false);
            if (record is null)
                return;

            if (await client.Rest.GetChannelAsync(channelId).ConfigureAwait(false) is not RestTextChannel channel)
                return;

            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithTitle($"Feature Request #{record.Id}: {record.Title.TrimTo(200)}")
                .WithDescription(record.Body.TrimTo(2048))
                .AddField("Category", record.Category, true)
                .AddField("Status", record.Status, true)
                .AddField("Votes", record.Votes, true)
                .AddField("Submitted By", $"{record.UserName} `{record.UserId}`", true);

            if (record.GuildId is { } guildId)
                eb.AddField("From Server", $"{record.GuildName ?? "Unknown"} `{guildId}`", true);
            if (!string.IsNullOrWhiteSpace(record.OwnerNote))
                eb.AddField("Owner Note", record.OwnerNote.TrimTo(1024));
            if (record.DateAdded is { } added)
                eb.WithTimestamp(DateTime.SpecifyKind(added, DateTimeKind.Utc));

            if (record.ReportMessageId is not 0)
            {
                await channel.ModifyMessageAsync(record.ReportMessageId, x => x.Embed = eb.Build())
                    .ConfigureAwait(false);
                return;
            }

            var msg = await channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
            await db.FeatureRequests.Where(x => x.Id == id)
                .Set(x => x.ReportMessageId, msg.Id)
                .UpdateAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to report feature request {RequestId}", id);
        }
    }
}
