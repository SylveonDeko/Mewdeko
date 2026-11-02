using DataModel;
using Discord.Net;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Modules.Utility.Common;

namespace Mewdeko.Modules.Utility.Services;

/// <summary>
///     Replies to social media links with a copy that goes through an embed fixing service, and removes the reply when
///     the original message is deleted.
/// </summary>
public class LinkFixService : INService
{
    private const int TrackedReplyLimit = 5000;

    private readonly IDataConnectionFactory dbFactory;
    private readonly ILogger<LinkFixService> logger;
    private readonly ConcurrentDictionary<ulong, LinkFixConfig> configs = new();
    private readonly ConcurrentDictionary<ulong, ulong> replies = new();
    private readonly System.Collections.Concurrent.ConcurrentQueue<ulong> replyOrder = new();

    /// <summary>
    ///     Initializes a new instance of the <see cref="LinkFixService" /> class.
    /// </summary>
    /// <param name="dbFactory">The database connection factory.</param>
    /// <param name="handler">The event handler for message events.</param>
    /// <param name="logger">The logger instance.</param>
    public LinkFixService(IDataConnectionFactory dbFactory, EventHandler handler, ILogger<LinkFixService> logger)
    {
        this.dbFactory = dbFactory;
        this.logger = logger;
        handler.Subscribe("MessageReceived", "LinkFixService", OnMessageReceived);
        handler.Subscribe("MessageDeleted", "LinkFixService", OnMessageDeleted);
    }

    /// <summary>
    ///     Gets a guild's link fixing settings.
    /// </summary>
    public async Task<LinkFixConfig> GetConfigAsync(ulong guildId)
    {
        if (configs.TryGetValue(guildId, out var cached))
            return cached;

        await using var db = await dbFactory.CreateConnectionAsync();
        var row = await db.LinkFixSettings.FirstOrDefaultAsync(x => x.GuildId == guildId);
        var config = LinkFixConfig.From(row);
        configs[guildId] = config;
        return config;
    }

    /// <summary>
    ///     Turns link fixing on or off for a guild.
    /// </summary>
    public Task SetEnabledAsync(ulong guildId, bool enabled) =>
        UpdateAsync(guildId, row => row.Enabled = enabled);

    /// <summary>
    ///     Turns link fixing for one site on or off in a guild.
    /// </summary>
    public Task SetPlatformEnabledAsync(ulong guildId, LinkFixPlatform platform, bool enabled) =>
        UpdateAsync(guildId, row =>
        {
            var disabled = LinkFixConfig.SplitList(row.DisabledPlatforms);
            if (enabled)
                disabled.Remove(platform.Name);
            else
                disabled.Add(platform.Name);
            row.DisabledPlatforms = string.Join(',', disabled);
        });

    /// <summary>
    ///     Sets the fixing host a guild uses for a site, or goes back to the default when <paramref name="host" /> is
    ///     null.
    /// </summary>
    public Task SetHostAsync(ulong guildId, LinkFixPlatform platform, string? host) =>
        UpdateAsync(guildId, row =>
        {
            var hosts = LinkFixConfig.SplitPairs(row.Hosts);
            if (host is null)
                hosts.Remove(platform.Name);
            else
                hosts[platform.Name] = host.ToLowerInvariant();
            row.Hosts = string.Join(',', hosts.Select(pair => $"{pair.Key}={pair.Value}"));
        });

    private async Task UpdateAsync(ulong guildId, Action<LinkFixSetting> update)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var row = await db.LinkFixSettings.FirstOrDefaultAsync(x => x.GuildId == guildId);
        if (row is null)
        {
            row = new LinkFixSetting
            {
                GuildId = guildId, DateAdded = DateTime.UtcNow
            };
            update(row);
            await db.InsertAsync(row);
        }
        else
        {
            update(row);
            await db.UpdateAsync(row);
        }

        configs[guildId] = LinkFixConfig.From(row);
    }

    private async Task OnMessageReceived(SocketMessage args)
    {
        if (args is not SocketUserMessage message || message.Author.IsBot || message.Author.IsWebhook
            || message.Channel is not SocketTextChannel channel
            || string.IsNullOrEmpty(message.Content)
            || !message.Content.Contains("http", StringComparison.OrdinalIgnoreCase))
            return;

        var config = await GetConfigAsync(channel.Guild.Id);
        if (!config.Enabled)
            return;

        var links = LinkFixPlatforms.FixLinks(message.Content, config.HostFor);
        if (links.Count == 0)
            return;

        var permissions = channel.Guild.CurrentUser.GetPermissions(channel);
        if (!permissions.SendMessages || !permissions.ViewChannel)
            return;

        try
        {
            var reply = await message.ReplyAsync(string.Join('\n', links), allowedMentions: AllowedMentions.None);
            TrackReply(message.Id, reply.Id);

            if (permissions.ManageMessages)
                await message.ModifyAsync(properties => properties.Flags = message.Flags | MessageFlags.SuppressEmbeds);
        }
        catch (HttpException ex)
        {
            logger.LogDebug(ex, "Could not fix links in channel {ChannelId}", channel.Id);
        }
    }

    private async Task OnMessageDeleted(Cacheable<IMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel)
    {
        if (!replies.TryRemove(message.Id, out var replyId))
            return;

        try
        {
            if (await channel.GetOrDownloadAsync() is { } messageChannel)
                await messageChannel.DeleteMessageAsync(replyId);
        }
        catch (HttpException ex)
        {
            logger.LogDebug(ex, "Could not remove the link fix reply {MessageId}", replyId);
        }
    }

    private void TrackReply(ulong messageId, ulong replyId)
    {
        replies[messageId] = replyId;
        replyOrder.Enqueue(messageId);
        while (replyOrder.Count > TrackedReplyLimit && replyOrder.TryDequeue(out var oldest))
            replies.TryRemove(oldest, out _);
    }
}

/// <summary>
///     A guild's link fixing settings.
/// </summary>
/// <param name="Enabled">Whether links are fixed.</param>
/// <param name="DisabledPlatforms">The keys of the sites whose links are left alone.</param>
/// <param name="Hosts">The fixing hosts the guild chose, by site key.</param>
public sealed record LinkFixConfig(bool Enabled, HashSet<string> DisabledPlatforms, Dictionary<string, string> Hosts)
{
    /// <summary>
    ///     Gets the fixing host for a site, or null when the guild turned the site off.
    /// </summary>
    public string? HostFor(LinkFixPlatform platform) =>
        DisabledPlatforms.Contains(platform.Name) ? null : Hosts.GetValueOrDefault(platform.Name, platform.DefaultHost);

    /// <summary>
    ///     Builds the settings from a stored row, or the defaults when there is none.
    /// </summary>
    public static LinkFixConfig From(LinkFixSetting? row) =>
        new(row?.Enabled ?? false, SplitList(row?.DisabledPlatforms), SplitPairs(row?.Hosts));

    internal static HashSet<string> SplitList(string? value) =>
        (value ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    internal static Dictionary<string, string> SplitPairs(string? value) =>
        (value ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(pair => pair.Split('=', 2))
        .Where(parts => parts.Length == 2 && parts[1].Length > 0)
        .ToDictionary(parts => parts[0], parts => parts[1], StringComparer.OrdinalIgnoreCase);
}
