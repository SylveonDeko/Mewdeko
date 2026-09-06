using DataModel;
using Discord.Net;
using Discord.Rest;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Services.Impl;
using Mewdeko.Services.Settings;
using Mewdeko.Services.Strings;

namespace Mewdeko.Modules.OwnerOnly.Services;

/// <summary>
///     Asks the owner of a server why Mewdeko was removed, right after the bot leaves or is
///     kicked, and records whatever they answer.
/// </summary>
public class GuildLeaveFeedbackService : INService, IReadyExecutor
{
    /// <summary>
    ///     How long to wait before prompting the same server again, so that a server that keeps
    ///     adding and removing the bot does not spam its owner.
    /// </summary>
    private static readonly TimeSpan PromptCooldown = TimeSpan.FromDays(30);

    /// <summary>
    ///     The reason keys offered in the select menu, in display order. The value is used as the
    ///     select menu option value, the stored reason, and the suffix of the localization key.
    /// </summary>
    public static readonly IReadOnlyList<string> ReasonKeys =
    [
        "not_needed", "confusing", "missing_features", "unreliable", "other_bot", "temporary", "testing"
    ];

    private readonly BotConfigService bss;
    private readonly DiscordShardedClient client;
    private readonly BotCredentials creds;
    private readonly IDataConnectionFactory dbFactory;
    private readonly EventHandler handler;
    private readonly ILogger<GuildLeaveFeedbackService> logger;
    private readonly GeneratedBotStrings strings;

    /// <summary>
    ///     Initializes a new instance of <see cref="GuildLeaveFeedbackService" /> and subscribes to guild removals.
    /// </summary>
    /// <param name="handler">The event handler the leave event is subscribed on.</param>
    /// <param name="client">The discord client.</param>
    /// <param name="dbFactory">The database connection factory.</param>
    /// <param name="strings">The localization strings.</param>
    /// <param name="bss">The bot config service.</param>
    /// <param name="creds">The bot credentials.</param>
    /// <param name="logger">The logger instance for structured logging.</param>
    public GuildLeaveFeedbackService(EventHandler handler, DiscordShardedClient client,
        IDataConnectionFactory dbFactory, GeneratedBotStrings strings, BotConfigService bss, BotCredentials creds,
        ILogger<GuildLeaveFeedbackService> logger)
    {
        this.handler = handler;
        this.client = client;
        this.dbFactory = dbFactory;
        this.strings = strings;
        this.bss = bss;
        this.creds = creds;
        this.logger = logger;
    }

    /// <summary>
    ///     Whether owners get a dm asking why the bot was removed.
    /// </summary>
    public bool Enabled
    {
        get
        {
            return bss.Data.LeaveFeedbackEnabled;
        }
    }

    /// <summary>
    ///     The channel configured for answers, or zero when the join/leave channel is used instead.
    /// </summary>
    public ulong ConfiguredChannelId
    {
        get
        {
            return bss.Data.LeaveFeedbackChannel;
        }
    }

    /// <summary>
    ///     The channel answers actually get posted to, after the join/leave channel fallback.
    /// </summary>
    public ulong ReportChannelId
    {
        get
        {
            return bss.Data.LeaveFeedbackChannel is not 0
                ? bss.Data.LeaveFeedbackChannel
                : creds.GuildJoinsChannelId;
        }
    }

    /// <inheritdoc />
    public Task OnReadyAsync()
    {
        handler.Subscribe("LeftGuild", "GuildLeaveFeedbackService", OnLeftGuild);
        return Task.CompletedTask;
    }

    private Task OnLeftGuild(SocketGuild guild)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await PromptOwner(guild).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to send leave feedback prompt for guild {GuildId}", guild.Id);
            }
        });
        return Task.CompletedTask;
    }

    private async Task PromptOwner(SocketGuild guild)
    {
        if (!bss.Data.LeaveFeedbackEnabled)
            return;

        var ownerId = guild.OwnerId;
        if (ownerId is 0 || ownerId == client.CurrentUser.Id)
            return;

        await using var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false);

        var cutoff = DateTime.UtcNow - PromptCooldown;
        var recentlyPrompted = await db.GuildLeaveFeedbacks
            .AnyAsync(x => x.GuildId == guild.Id && x.DateAdded > cutoff).ConfigureAwait(false);
        if (recentlyPrompted)
            return;

        var joinedAt = guild.CurrentUser?.JoinedAt?.UtcDateTime
                       ?? await db.GuildConfigs
                           .Where(x => x.GuildId == guild.Id)
                           .Select(x => x.DateAdded)
                           .FirstOrDefaultAsync().ConfigureAwait(false);

        var record = new GuildLeaveFeedback
        {
            GuildId = guild.Id,
            GuildName = guild.Name ?? string.Empty,
            MemberCount = guild.MemberCount,
            OwnerId = ownerId,
            JoinedAt = joinedAt,
            DateAdded = DateTime.UtcNow
        };

        record.Id = await db.InsertWithInt32IdentityAsync(record).ConfigureAwait(false);

        var owner = client.GetUser(ownerId) as IUser ??
                    await client.Rest.GetUserAsync(ownerId).ConfigureAwait(false);
        if (owner is null)
            return;

        var eb = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(strings.LeaveFeedbackTitle(guild.Id))
            .WithDescription(strings.LeaveFeedbackDesc(guild.Id, guild.Name))
            .WithFooter(strings.LeaveFeedbackFooter(guild.Id));

        var menu = new SelectMenuBuilder()
            .WithCustomId($"leavefb:reason:{record.Id}")
            .WithPlaceholder(strings.LeaveFeedbackPlaceholder(guild.Id))
            .WithMinValues(1)
            .WithMaxValues(1);

        foreach (var key in ReasonKeys)
            menu.AddOption(GetReasonLabel(key, guild.Id), key);

        var components = new ComponentBuilder()
            .WithSelectMenu(menu)
            .WithButton(strings.LeaveFeedbackWriteButton(guild.Id), $"leavefb:comment:{record.Id}",
                ButtonStyle.Secondary, row: 1)
            .WithButton(strings.LeaveFeedbackDismissButton(guild.Id), $"leavefb:dismiss:{record.Id}",
                ButtonStyle.Secondary, row: 1)
            .Build();

        try
        {
            await owner.SendMessageAsync(embed: eb.Build(), components: components).ConfigureAwait(false);
        }
        catch (HttpException)
        {
            await db.GuildLeaveFeedbacks.Where(x => x.Id == record.Id).DeleteAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Gets the localized label for a reason key.
    /// </summary>
    /// <param name="key">One of <see cref="ReasonKeys" />.</param>
    /// <param name="guildId">The guild the feedback is about, used for locale resolution.</param>
    /// <returns>The human readable label for the reason.</returns>
    public string GetReasonLabel(string key, ulong? guildId)
    {
        return key switch
        {
            "not_needed" => strings.LeaveFeedbackReasonNotNeeded(guildId),
            "confusing" => strings.LeaveFeedbackReasonConfusing(guildId),
            "missing_features" => strings.LeaveFeedbackReasonMissingFeatures(guildId),
            "unreliable" => strings.LeaveFeedbackReasonUnreliable(guildId),
            "other_bot" => strings.LeaveFeedbackReasonOtherBot(guildId),
            "temporary" => strings.LeaveFeedbackReasonTemporary(guildId),
            "testing" => strings.LeaveFeedbackReasonTesting(guildId),
            _ => key
        };
    }

    /// <summary>
    ///     Updates the bot wide leave feedback settings and persists them to bot.yml.
    /// </summary>
    /// <param name="enabled">Whether owners get asked why the bot was removed.</param>
    /// <param name="channelId">The channel answers get posted to, or zero for the join/leave channel.</param>
    public void UpdateSettings(bool enabled, ulong channelId)
    {
        bss.ModifyConfig(config =>
        {
            config.LeaveFeedbackEnabled = enabled;
            config.LeaveFeedbackChannel = channelId;
        });
    }

    /// <summary>
    ///     Resolves a report channel id to something readable, so the dashboard can show which
    ///     channel a bot wide id actually points at.
    /// </summary>
    /// <param name="channelId">The channel id to resolve.</param>
    /// <returns>The channel name, its guild, and whether the bot can post there.</returns>
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
            logger.LogWarning(ex, "Failed to resolve leave feedback channel {ChannelId}", channelId);
            return (null, 0, null, false);
        }
    }

    /// <summary>
    ///     Gets a page of feedback records for the dashboard, newest first.
    /// </summary>
    /// <param name="reason">Optional reason key filter.</param>
    /// <param name="status">Optional status filter: answered, dismissed, or pending.</param>
    /// <param name="search">Optional case insensitive search across guild name and comment.</param>
    /// <param name="page">The 1-based page number.</param>
    /// <param name="pageSize">The page size.</param>
    /// <returns>The records on the page and the total number matching the filters.</returns>
    public async Task<(List<GuildLeaveFeedback> Items, int Total)> GetPageAsync(string? reason, string? status,
        string? search, int page, int pageSize)
    {
        await using var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false);

        var query = db.GuildLeaveFeedbacks.AsQueryable();

        if (!string.IsNullOrWhiteSpace(reason))
            query = query.Where(x => x.Reason == reason);

        query = status?.ToLowerInvariant() switch
        {
            "answered" => query.Where(x => x.AnsweredAt != null && !x.Dismissed),
            "dismissed" => query.Where(x => x.Dismissed),
            "pending" => query.Where(x => x.AnsweredAt == null),
            "commented" => query.Where(x => x.Comment != null && x.Comment != ""),
            _ => query
        };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = $"%{search.ToLowerInvariant()}%";
            query = query.Where(x =>
                Sql.Like(x.GuildName.ToLower(), term) || Sql.Like(x.Comment!.ToLower(), term));
        }

        var total = await query.CountAsync().ConfigureAwait(false);

        if (page < 1)
            page = 1;
        pageSize = Math.Clamp(pageSize, 1, 200);

        var items = await query
            .OrderByDescending(x => x.DateAdded)
            .ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync().ConfigureAwait(false);

        return (items, total);
    }

    /// <summary>
    ///     Gets aggregate counts across all collected feedback.
    /// </summary>
    /// <returns>Totals by status and a count per reason key.</returns>
    public async Task<(int Total, int Answered, int Dismissed, int Pending, int WithComment,
        Dictionary<string, int> ByReason)> GetStatsAsync()
    {
        await using var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false);

        var all = await db.GuildLeaveFeedbacks
            .Select(x => new
            {
                x.Reason, x.Comment, x.Dismissed, x.AnsweredAt
            })
            .ToListAsync().ConfigureAwait(false);

        var byReason = all
            .Where(x => x.Reason != null)
            .GroupBy(x => x.Reason!)
            .ToDictionary(x => x.Key, x => x.Count());

        return (all.Count,
            all.Count(x => x.AnsweredAt != null && !x.Dismissed),
            all.Count(x => x.Dismissed),
            all.Count(x => x.AnsweredAt == null),
            all.Count(x => !string.IsNullOrWhiteSpace(x.Comment)),
            byReason);
    }

    /// <summary>
    ///     Deletes a feedback record, for clearing out test or junk entries from the dashboard.
    /// </summary>
    /// <param name="id">The feedback record id.</param>
    /// <returns>Whether a record was deleted.</returns>
    public async Task<bool> DeleteAsync(int id)
    {
        await using var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false);
        return await db.GuildLeaveFeedbacks.Where(x => x.Id == id).DeleteAsync().ConfigureAwait(false) > 0;
    }

    /// <summary>
    ///     Loads a feedback record, making sure it belongs to the user acting on it.
    /// </summary>
    /// <param name="id">The feedback record id from the component custom id.</param>
    /// <param name="userId">The user who pressed the component.</param>
    /// <returns>The record, or null if it does not exist or belongs to someone else.</returns>
    public async Task<GuildLeaveFeedback?> GetForUser(int id, ulong userId)
    {
        await using var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false);
        return await db.GuildLeaveFeedbacks.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == userId)
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Stores the reason the owner selected.
    /// </summary>
    /// <param name="id">The feedback record id.</param>
    /// <param name="reason">The selected reason key.</param>
    public async Task SetReason(int id, string reason)
    {
        await using var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false);
        await db.GuildLeaveFeedbacks.Where(x => x.Id == id)
            .Set(x => x.Reason, reason)
            .Set(x => x.AnsweredAt, DateTime.UtcNow)
            .UpdateAsync().ConfigureAwait(false);
    }

    /// <summary>
    ///     Stores the free text comment the owner wrote.
    /// </summary>
    /// <param name="id">The feedback record id.</param>
    /// <param name="comment">The comment text.</param>
    public async Task SetComment(int id, string comment)
    {
        await using var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false);
        await db.GuildLeaveFeedbacks.Where(x => x.Id == id)
            .Set(x => x.Comment, comment)
            .Set(x => x.AnsweredAt, DateTime.UtcNow)
            .UpdateAsync().ConfigureAwait(false);
    }

    /// <summary>
    ///     Marks the prompt as dismissed so nothing gets reported for it.
    /// </summary>
    /// <param name="id">The feedback record id.</param>
    public async Task Dismiss(int id)
    {
        await using var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false);
        await db.GuildLeaveFeedbacks.Where(x => x.Id == id)
            .Set(x => x.Dismissed, true)
            .Set(x => x.AnsweredAt, DateTime.UtcNow)
            .UpdateAsync().ConfigureAwait(false);
    }

    /// <summary>
    ///     Posts an answered feedback record to the configured report channel.
    /// </summary>
    /// <param name="id">The feedback record id.</param>
    /// <param name="responder">The user who answered, used for attribution.</param>
    public async Task Report(int id, IUser responder)
    {
        var channelId = ReportChannelId;
        if (channelId is 0)
            return;

        await using var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false);
        var record = await db.GuildLeaveFeedbacks.FirstOrDefaultAsync(x => x.Id == id).ConfigureAwait(false);
        if (record is null || record.Dismissed)
            return;

        try
        {
            if (await client.Rest.GetChannelAsync(channelId).ConfigureAwait(false) is not RestTextChannel channel)
                return;

            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithTitle("Leave Feedback")
                .AddField("Server", $"{record.GuildName} `{record.GuildId}`")
                .AddField("Members", record.MemberCount, true)
                .AddField("Owner", $"{responder} `{record.OwnerId}`", true)
                .AddField("Reason",
                    record.Reason is null ? "Not selected" : GetReasonLabel(record.Reason, record.GuildId));

            if (record.JoinedAt.HasValue)
                eb.AddField("Was In Server Since", TimestampTag.FromDateTime(record.JoinedAt.Value), true);

            if (!string.IsNullOrWhiteSpace(record.Comment))
                eb.AddField("Comment", record.Comment.TrimTo(1024));

            if (record.ReportMessageId is not 0)
            {
                await channel.ModifyMessageAsync(record.ReportMessageId, x => x.Embed = eb.Build())
                    .ConfigureAwait(false);
                return;
            }

            var msg = await channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
            await db.GuildLeaveFeedbacks.Where(x => x.Id == id)
                .Set(x => x.ReportMessageId, msg.Id)
                .UpdateAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to report leave feedback {FeedbackId}", id);
        }
    }
}