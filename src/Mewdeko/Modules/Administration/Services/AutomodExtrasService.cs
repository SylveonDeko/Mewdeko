using System.Threading;
using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Services.Strings;

namespace Mewdeko.Modules.Administration.Services;

/// <summary>
///     Holds the settings shared by every automod rule, protection and filter: the log and review channels, server-wide
///     exemptions, link domain lists and link rate limits.
/// </summary>
public class AutomodExtrasService : INService, IReadyExecutor
{
    private static readonly AutomodExtraSetting Defaults = new();

    private readonly ConcurrentDictionary<ulong, Compiled> cache = new();
    private readonly DiscordShardedClient client;
    private readonly IDataConnectionFactory dbFactory;
    private readonly ConcurrentDictionary<(ulong, ulong, ulong), Queue<(DateTime At, int Count)>> linkHistory = new();
    private readonly ILogger<AutomodExtrasService> logger;
    private readonly GeneratedBotStrings strings;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AutomodExtrasService" /> class.
    /// </summary>
    /// <param name="client">The Discord client.</param>
    /// <param name="dbFactory">The database connection factory.</param>
    /// <param name="strings">The generated bot strings.</param>
    /// <param name="logger">The logger instance.</param>
    public AutomodExtrasService(DiscordShardedClient client, IDataConnectionFactory dbFactory,
        GeneratedBotStrings strings, ILogger<AutomodExtrasService> logger)
    {
        this.client = client;
        this.dbFactory = dbFactory;
        this.strings = strings;
        this.logger = logger;
    }

    /// <summary>
    ///     Loads every guild's settings into the cache.
    /// </summary>
    public async Task OnReadyAsync()
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();
            var settings = await db.GetTable<AutomodExtraSetting>().ToListAsync().ConfigureAwait(false);
            var limits = (await db.GetTable<AutomodLinkLimit>().ToListAsync().ConfigureAwait(false))
                .GroupBy(x => x.GuildId).ToDictionary(x => x.Key, x => x.ToList());

            foreach (var setting in settings)
                cache[setting.GuildId] = new Compiled(setting, limits.GetValueOrDefault(setting.GuildId) ?? []);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load automod extras");
        }

        _ = Task.Run(PruneLinkHistoryAsync);
    }

    private async Task PruneLinkHistoryAsync()
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        while (await timer.WaitForNextTickAsync())
        {
            var cutoff = DateTime.UtcNow - TimeSpan.FromHours(1);
            foreach (var (key, history) in linkHistory)
            {
                lock (history)
                {
                    while (history.TryPeek(out var oldest) && oldest.At < cutoff)
                        history.Dequeue();
                    if (history.Count == 0)
                        linkHistory.TryRemove(key, out _);
                }
            }
        }
    }

    /// <summary>
    ///     Gets a guild's settings from the cache.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>The settings, or the defaults when the guild never changed any.</returns>
    public AutomodExtraSetting Get(ulong guildId)
    {
        return cache.TryGetValue(guildId, out var compiled) ? compiled.Setting : Defaults;
    }

    /// <summary>
    ///     Gets a guild's link domain lists from the cache.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>The allowed and blocked domains.</returns>
    public (IReadOnlyCollection<string> Allowed, IReadOnlyCollection<string> Blocked) GetDomains(ulong guildId)
    {
        return cache.TryGetValue(guildId, out var compiled) ? (compiled.Allowed, compiled.Blocked) : ([], []);
    }

    /// <summary>
    ///     Gets the link limit set for a channel.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="channelId">The channel ID.</param>
    /// <returns>The limit, or null when the channel has none.</returns>
    public AutomodLinkLimit? GetLinkLimit(ulong guildId, ulong channelId)
    {
        return cache.TryGetValue(guildId, out var compiled) ? compiled.Limits.GetValueOrDefault(channelId) : null;
    }

    /// <summary>
    ///     Gets every link limit set in a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    public IReadOnlyCollection<AutomodLinkLimit> GetLinkLimits(ulong guildId)
    {
        return cache.TryGetValue(guildId, out var compiled) ? compiled.Limits.Values : [];
    }

    /// <summary>
    ///     Creates or changes a guild's settings and refreshes the cache.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="change">What to change.</param>
    /// <returns>The saved settings.</returns>
    public async Task<AutomodExtraSetting> UpdateAsync(ulong guildId, Action<AutomodExtraSetting> change)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var row = await db.GetTable<AutomodExtraSetting>().FirstOrDefaultAsync(x => x.GuildId == guildId)
            .ConfigureAwait(false);
        var isNew = row is null;
        row ??= new AutomodExtraSetting
        {
            GuildId = guildId
        };

        change(row);

        if (isNew)
            row.Id = await db.InsertWithInt32IdentityAsync(row).ConfigureAwait(false);
        else
            await db.UpdateAsync(row).ConfigureAwait(false);

        var limits = cache.TryGetValue(guildId, out var old) ? old.Limits.Values.ToList() : [];
        cache[guildId] = new Compiled(row, limits);
        return row;
    }

    /// <summary>
    ///     Toggles a role or channel as exempt from every automod rule, protection and filter.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="id">The role or channel ID.</param>
    /// <param name="isRole">Whether the ID is a role.</param>
    /// <returns>True when it was added, false when it was removed.</returns>
    public async Task<bool> ToggleExemptAsync(ulong guildId, ulong id, bool isRole)
    {
        var added = false;
        await UpdateAsync(guildId, x =>
        {
            var ids = JoinRoleTimeoutService.ParseIds(isRole ? x.ExemptRoleIds : x.ExemptChannelIds);
            added = !ids.Remove(id);
            if (added) ids.Add(id);
            var joined = JoinRoleTimeoutService.JoinIds(ids);
            if (isRole) x.ExemptRoleIds = joined;
            else x.ExemptChannelIds = joined;
        }).ConfigureAwait(false);
        return added;
    }

    /// <summary>
    ///     Adds a domain to the link filter's allowed or blocked list, or removes it when it is already there.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="domain">The domain.</param>
    /// <param name="allowed">Whether the allowed list is meant rather than the blocked list.</param>
    /// <returns>True when it was added, false when it was removed.</returns>
    public async Task<bool> ToggleDomainAsync(ulong guildId, string domain, bool allowed)
    {
        var added = false;
        await UpdateAsync(guildId, x =>
        {
            var list = (allowed ? x.LinkAllowedDomains : x.LinkBlockedDomains)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
            added = !list.Remove(domain);
            if (added) list.Add(domain);
            var joined = string.Join(' ', list);
            if (allowed) x.LinkAllowedDomains = joined;
            else x.LinkBlockedDomains = joined;
        }).ConfigureAwait(false);
        return added;
    }

    /// <summary>
    ///     Cleans a domain a member typed, dropping the scheme, path and a leading www.
    /// </summary>
    /// <param name="input">The text to read.</param>
    /// <returns>The domain, or null when it does not look like one.</returns>
    public static string? NormalizeDomain(string input)
    {
        var domain = input.Trim().ToLowerInvariant();
        var scheme = domain.IndexOf("://", StringComparison.Ordinal);
        if (scheme >= 0)
            domain = domain[(scheme + 3)..];

        domain = domain.Split('/', '?', '#')[0];
        if (domain.StartsWith("www."))
            domain = domain[4..];

        var valid = domain.Length is > 3 and <= 253 && domain.Contains('.') && !domain.StartsWith('.') &&
                    !domain.EndsWith('.') && domain.All(c => char.IsLetterOrDigit(c) || c is '.' or '-');
        return valid ? domain : null;
    }

    /// <summary>
    ///     Sets how many links one member may post in a channel within a window.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="channelId">The channel ID.</param>
    /// <param name="maxLinks">The most links allowed, or 0 to remove the limit.</param>
    /// <param name="seconds">The window in seconds.</param>
    public async Task SetLinkLimitAsync(ulong guildId, ulong channelId, int maxLinks, int seconds)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var table = db.GetTable<AutomodLinkLimit>();
        var row = await table.FirstOrDefaultAsync(x => x.GuildId == guildId && x.ChannelId == channelId)
            .ConfigureAwait(false);

        if (maxLinks <= 0)
        {
            if (row is not null)
                await db.DeleteAsync(row).ConfigureAwait(false);
            row = null;
        }
        else if (row is null)
        {
            row = new AutomodLinkLimit
            {
                GuildId = guildId, ChannelId = channelId, MaxLinks = maxLinks, WindowSeconds = seconds
            };
            row.Id = await db.InsertWithInt32IdentityAsync(row).ConfigureAwait(false);
        }
        else
        {
            row.MaxLinks = maxLinks;
            row.WindowSeconds = seconds;
            await db.UpdateAsync(row).ConfigureAwait(false);
        }

        var limits = cache.TryGetValue(guildId, out var old)
            ? old.Limits.Values.Where(x => x.ChannelId != channelId).ToList()
            : [];
        if (row is not null)
            limits.Add(row);

        cache[guildId] = new Compiled(old?.Setting ?? new AutomodExtraSetting
        {
            GuildId = guildId
        }, limits);
    }

    /// <summary>
    ///     Checks whether a member is exempt from every automod rule, protection and filter in a channel.
    /// </summary>
    /// <param name="member">The member.</param>
    /// <param name="channel">The channel the message was posted in.</param>
    public bool IsExempt(IGuildUser member, IChannel channel)
    {
        if (!cache.TryGetValue(member.GuildId, out var compiled))
            return false;

        if (member.RoleIds.Any(compiled.ExemptRoles.Contains) || compiled.ExemptChannels.Contains(channel.Id))
            return true;

        return channel is SocketThreadChannel { ParentChannel: { } parent } &&
               compiled.ExemptChannels.Contains(parent.Id);
    }

    /// <summary>
    ///     Checks whether a member has a role that is exempt from every automod rule, protection and filter.
    /// </summary>
    /// <param name="member">The member.</param>
    public bool HasExemptRole(IGuildUser member)
    {
        return cache.TryGetValue(member.GuildId, out var compiled) && member.RoleIds.Any(compiled.ExemptRoles.Contains);
    }

    /// <summary>
    ///     Records links a member posted and checks them against the channel's link limit.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="channelId">The channel ID.</param>
    /// <param name="userId">The member ID.</param>
    /// <param name="links">How many links the message has.</param>
    /// <returns>True when the member went over the limit.</returns>
    public bool LinkLimitExceeded(ulong guildId, ulong channelId, ulong userId, int links)
    {
        if (links == 0 || GetLinkLimit(guildId, channelId) is not { } limit)
            return false;

        var now = DateTime.UtcNow;
        var window = TimeSpan.FromSeconds(Math.Max(1, limit.WindowSeconds));
        var history = linkHistory.GetOrAdd((guildId, channelId, userId), _ => new());
        lock (history)
        {
            while (history.TryPeek(out var oldest) && now - oldest.At > window)
                history.Dequeue();

            history.Enqueue((now, links));
            if (history.Sum(x => x.Count) <= limit.MaxLinks)
                return false;

            history.Clear();
            return true;
        }
    }

    /// <summary>
    ///     Checks whether a domain is in a list, counting subdomains of listed domains.
    /// </summary>
    /// <param name="domain">The domain.</param>
    /// <param name="list">The listed domains.</param>
    public static bool DomainListed(string domain, IEnumerable<string> list)
    {
        return list.Any(x => domain == x || domain.EndsWith($".{x}", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Posts an automod action in the log channel, if the guild has one.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The member's ID.</param>
    /// <param name="rule">The name of the rule, protection or filter.</param>
    /// <param name="content">The message content, if there is a message.</param>
    /// <param name="actions">What was done.</param>
    /// <param name="channelId">The channel of the message, if there is one.</param>
    public async Task LogAsync(ulong guildId, ulong userId, string rule, string? content, string actions,
        ulong? channelId)
    {
        if (Get(guildId).LogChannelId is not { } logId ||
            client.GetGuild(guildId)?.GetTextChannel(logId) is not { } log)
            return;

        try
        {
            var embed = new EmbedBuilder().WithErrorColor()
                .WithTitle(strings.AutomodActionTitle(guildId))
                .AddField(strings.AutomodActionMember(guildId), $"<@{userId}> ({userId})", true)
                .AddField(strings.AutomodActionRule(guildId), rule, true)
                .AddField(strings.AutomodActionActions(guildId), actions, true);

            if (channelId is not null)
                embed.AddField(strings.AutomodActionChannel(guildId), $"<#{channelId}>", true);

            embed.AddField(strings.AutomodActionMessage(guildId),
                string.IsNullOrWhiteSpace(content) ? strings.AutomodActionNoText(guildId) : content.TrimTo(1000));

            await log.SendMessageAsync(embed: embed.Build(), allowedMentions: AllowedMentions.None)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not post to the automod log in {GuildId}", guildId);
        }
    }

    private sealed class Compiled
    {
        public Compiled(AutomodExtraSetting setting, List<AutomodLinkLimit> limits)
        {
            Setting = setting;
            ExemptRoles = JoinRoleTimeoutService.ParseIds(setting.ExemptRoleIds).ToHashSet();
            ExemptChannels = JoinRoleTimeoutService.ParseIds(setting.ExemptChannelIds).ToHashSet();
            Allowed = setting.LinkAllowedDomains.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            Blocked = setting.LinkBlockedDomains.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            Limits = limits.ToDictionary(x => x.ChannelId);
        }

        public AutomodExtraSetting Setting { get; }
        public HashSet<ulong> ExemptRoles { get; }
        public HashSet<ulong> ExemptChannels { get; }
        public string[] Allowed { get; }
        public string[] Blocked { get; }
        public Dictionary<ulong, AutomodLinkLimit> Limits { get; }
    }
}
