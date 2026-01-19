using CodeHollow.FeedReader;
using CodeHollow.FeedReader.Feeds;
using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Embed = Discord.Embed;

namespace Mewdeko.Modules.Searches.Services;

/// <summary>
///     Service for tracking RSS feeds and sending updates to subscribed channels.
/// </summary>
public class FeedsService : INService
{
    private readonly DiscordShardedClient client;
    private readonly IDataConnectionFactory dbFactory;

    private readonly ConcurrentDictionary<string, DateTime> lastPosts =
        new();

    private readonly ConcurrentDictionary<string, HashSet<FeedSub>> subs;

    /// <summary>
    ///     Initializes a new instance of the <see cref="FeedsService" /> class.
    /// </summary>
    /// <param name="dbFactory">The database service.</param>
    /// <param name="client">The Discord client.</param>
    /// <param name="bot">The bot instance.</param>
    public FeedsService(IDataConnectionFactory dbFactory, DiscordShardedClient client, Mewdeko bot)
    {
        this.dbFactory = dbFactory;
        subs = new ConcurrentDictionary<string, HashSet<FeedSub>>();

        this.client = client;

        _ = TrackFeeds(client.Guilds.Select(x => x.Id));
    }

    /// <summary>
    ///     Tracks RSS feeds for updates and sends notifications to subscribed channels.
    /// </summary>
    /// <returns>An asynchronous task representing the operation.</returns>
    private async Task<EmbedBuilder> TrackFeeds(IEnumerable<ulong> serverIds)
    {
        // ReSharper disable once AsyncVoidLambda
        foreach (var serverId in serverIds)
        {
            await Task.CompletedTask;
            var feeds = await GetFeeds(serverId);
            foreach (var feed in feeds)
            {
                if (feed == null) continue;

                subs.AddOrUpdate(feed.Url.ToLower(), [feed], (_, old) =>
                {
                    old.Add(feed);
                    return old;
                });
            }
        }

        while (true)
        {
            var allSendTasks = new List<Task>(subs.Count);
            foreach (var (rssUrl, value) in subs)
            {
                if (value.Count == 0)
                    continue;

                try
                {
                    var feed = await FeedReader.ReadAsync(rssUrl, default, true,
                        "Mozilla/5.0 (compatible; MewdekoBot/1.0; +https://mewdeko.tech)").ConfigureAwait(false);

                    var items = feed
                        .Items
                        .Select(item => (Item: item, LastUpdate: item.PublishingDate?.ToUniversalTime()
                                                                 ?? (item.SpecificItem as AtomFeedItem)?.UpdatedDate
                                                                 ?.ToUniversalTime()))
                        .Where(data => data.LastUpdate is not null)
                        .Select(data => (data.Item, LastUpdate: (DateTime)data.LastUpdate))
                        .OrderByDescending(data => data.LastUpdate)
                        .Reverse() // start from the oldest
                        .ToList();

                    if (!lastPosts.TryGetValue(rssUrl, out var lastFeedUpdate))
                        lastFeedUpdate = lastPosts[rssUrl] =
                            items.Any() ? items[^1].LastUpdate : DateTime.UtcNow;

                    foreach (var (feedItem, itemUpdateDate) in items)
                    {
                        var repbuilder = new ReplacementBuilder()
                            .WithOverride("%title%", () => feedItem.Title ?? "Unkown")
                            .WithOverride("%author%", () => feedItem.Author ?? "Unknown")
                            .WithOverride("%content%", () => feedItem.Description?.StripHtml())
                            .WithOverride("%image_url%", () =>
                            {
                                if (feedItem.SpecificItem is AtomFeedItem atomFeedItem)
                                {
                                    var previewElement = atomFeedItem.Element.Elements()
                                        .FirstOrDefault(x => x.Name.LocalName == "preview") ?? atomFeedItem.Element
                                        .Elements()
                                        .FirstOrDefault(x => x.Name.LocalName == "thumbnail");

                                    var urlAttribute = previewElement?.Attribute("url");
                                    if (urlAttribute != null && !string.IsNullOrWhiteSpace(urlAttribute.Value)
                                                             && Uri.IsWellFormedUriString(urlAttribute.Value,
                                                                 UriKind.Absolute))
                                    {
                                        return urlAttribute.Value;
                                    }
                                }

                                if (feedItem.SpecificItem is not MediaRssFeedItem mediaRssFeedItem
                                    || !(mediaRssFeedItem.Enclosure?.MediaType?.StartsWith("image/") ?? false))
                                    return feed.ImageUrl;
                                var imgUrl = mediaRssFeedItem.Enclosure.Url;
                                if (!string.IsNullOrWhiteSpace(imgUrl) &&
                                    Uri.IsWellFormedUriString(imgUrl, UriKind.Absolute))
                                {
                                    return imgUrl;
                                }

                                return feed.ImageUrl;
                            })
                            .WithOverride("%categories%", () => string.Join(", ", feedItem.Categories))
                            .WithOverride("%timestamp%",
                                () => TimestampTag.FromDateTime(feedItem.PublishingDate.Value,
                                    TimestampTagStyles.LongDateTime).ToString())
                            .WithOverride("%url%", () => feedItem.Link ?? feedItem.SpecificItem.Link)
                            .WithOverride("%feedurl%", () => rssUrl)
                            .Build();

                        if (itemUpdateDate <= lastFeedUpdate) continue;
                        var embed = new EmbedBuilder()
                            .WithFooter(rssUrl);

                        lastPosts[rssUrl] = itemUpdateDate;

                        var link = feedItem.SpecificItem.Link;
                        if (!string.IsNullOrWhiteSpace(link) && Uri.IsWellFormedUriString(link, UriKind.Absolute))
                            embed.WithUrl(link);

                        var title = string.IsNullOrWhiteSpace(feedItem.Title)
                            ? "-"
                            : feedItem.Title;

                        var gotImage = false;
                        if (feedItem.SpecificItem is MediaRssFeedItem mrfi &&
                            (mrfi.Enclosure?.MediaType?.StartsWith("image/") ?? false))
                        {
                            var imgUrl = mrfi.Enclosure.Url;
                            if (!string.IsNullOrWhiteSpace(imgUrl) &&
                                Uri.IsWellFormedUriString(imgUrl, UriKind.Absolute))
                            {
                                embed.WithImageUrl(imgUrl);
                                gotImage = true;
                            }
                        }

                        if (!gotImage && feedItem.SpecificItem is AtomFeedItem afi)
                        {
                            var previewElement = afi.Element.Elements()
                                .FirstOrDefault(x => x.Name.LocalName == "preview") ?? afi.Element.Elements()
                                .FirstOrDefault(x => x.Name.LocalName == "thumbnail");

                            var urlAttribute = previewElement?.Attribute("url");
                            if (urlAttribute != null && !string.IsNullOrWhiteSpace(urlAttribute.Value)
                                                     && Uri.IsWellFormedUriString(urlAttribute.Value,
                                                         UriKind.Absolute))
                            {
                                embed.WithImageUrl(urlAttribute.Value);
                            }
                        }


                        embed.WithTitle(title.TrimTo(256));

                        var desc = feedItem.Description?.StripHtml();
                        if (!string.IsNullOrWhiteSpace(feedItem.Description))
                            embed.WithDescription(desc.TrimTo(2048));

                        //send the created embed to all subscribed channels
                        foreach (var feed1 in value)
                        {
                            var channel = client.GetGuild(feed1.GuildId).GetTextChannel(feed1.ChannelId);
                            if (channel is null)
                                continue;
                            var (builder, content, componentBuilder) =
                                await GetFeedEmbed(repbuilder.Replace(feed1.Message), channel.Guild.Id);
                            if (feed1.Message is "-" or null)
                                allSendTasks.Add(channel.EmbedAsync(embed));
                            else
                                allSendTasks.Add(channel.SendMessageAsync(content ?? "", embeds: builder ?? null,
                                    components: componentBuilder?.Build()));
                        }
                    }
                }
                catch
                {
                    //ignored
                }
            }

            await Task.WhenAll(Task.WhenAll(allSendTasks), Task.Delay(10000)).ConfigureAwait(false);
        }
    }


    /// <summary>
    ///     Tests an RSS feed subscription by sending an update to the specified channel.
    /// </summary>
    /// <param name="sub">The feed subscription to test.</param>
    /// <param name="channel">The channel to send the test update to.</param>
    /// <returns>An asynchronous task representing the operation.</returns>
    public async Task TestRss(FeedSub sub, ITextChannel channel)
    {
        var feed = await FeedReader.ReadAsync(sub.Url, default, true,
            "Mozilla/5.0 (compatible; MewdekoBot/1.0; +https://mewdeko.tech)");
        var (feedItem, _) = feed.Items
            .Select(item => (Item: item,
                LastUpdate: item.PublishingDate?.ToUniversalTime() ??
                            (item.SpecificItem as AtomFeedItem)?.UpdatedDate?.ToUniversalTime()))
            .Where(data => data.LastUpdate is not null)
            .Select(data => (data.Item, LastUpdate: (DateTime)data.LastUpdate)).LastOrDefault();

        var repbuilder = new ReplacementBuilder()
            .WithOverride("%title%", () => feedItem.Title ?? "Unkown")
            .WithOverride("%author%", () => feedItem.Author ?? "Unknown")
            .WithOverride("%content%", () => feedItem.Description?.StripHtml())
            .WithOverride("%image_url%", () =>
            {
                if (feedItem.SpecificItem is AtomFeedItem atomFeedItem)
                {
                    var previewElement =
                        atomFeedItem.Element.Elements().FirstOrDefault(x => x.Name.LocalName == "preview") ??
                        atomFeedItem.Element.Elements().FirstOrDefault(x => x.Name.LocalName == "thumbnail");
                    var urlAttribute = previewElement?.Attribute("url");
                    if (urlAttribute != null
                        && !string.IsNullOrWhiteSpace(urlAttribute.Value)
                        && Uri.IsWellFormedUriString(urlAttribute.Value, UriKind.Absolute))
                        return urlAttribute.Value;
                }

                if (feedItem.SpecificItem is not MediaRssFeedItem mediaRssFeedItem ||
                    !(mediaRssFeedItem.Enclosure?.MediaType?.StartsWith("image/") ?? false))
                    return feed.ImageUrl;
                var imgUrl = mediaRssFeedItem.Enclosure.Url;
                if (!string.IsNullOrWhiteSpace(imgUrl) && Uri.IsWellFormedUriString(imgUrl, UriKind.Absolute))
                    return imgUrl;

                return feed.ImageUrl;
            })
            .WithOverride("%categories%", () => string.Join(", ", feedItem.Categories))
            .WithOverride("%timestamp%",
                () => TimestampTag.FromDateTime(feedItem.PublishingDate.Value, TimestampTagStyles.LongDateTime)
                    .ToString())
            .WithOverride("%url%", () => feedItem.Link ?? feedItem.SpecificItem.Link)
            .WithOverride("%feedurl%", () => sub.Url).Build();
        var embed = new EmbedBuilder().WithFooter(sub.Url);
        var link = feedItem.SpecificItem.Link;
        if (!string.IsNullOrWhiteSpace(link) && Uri.IsWellFormedUriString(link, UriKind.Absolute)) embed.WithUrl(link);
        var title = string.IsNullOrWhiteSpace(feedItem.Title) ? "-" : feedItem.Title;
        var gotImage = false;
        if (feedItem.SpecificItem is MediaRssFeedItem mrfi &&
            (mrfi.Enclosure?.MediaType?.StartsWith("image/") ?? false))
        {
            var imgUrl = mrfi.Enclosure.Url;
            if (!string.IsNullOrWhiteSpace(imgUrl) && Uri.IsWellFormedUriString(imgUrl, UriKind.Absolute))
            {
                embed.WithImageUrl(imgUrl);
                gotImage = true;
            }
        }

        if (!gotImage && feedItem.SpecificItem is AtomFeedItem afi)
        {
            var previewElement = afi.Element.Elements().FirstOrDefault(x => x.Name.LocalName == "preview") ??
                                 afi.Element.Elements().FirstOrDefault(x => x.Name.LocalName == "thumbnail");
            var urlAttribute = previewElement?.Attribute("url");
            if (urlAttribute != null && !string.IsNullOrWhiteSpace(urlAttribute.Value) &&
                Uri.IsWellFormedUriString(urlAttribute.Value, UriKind.Absolute))
            {
                embed.WithImageUrl(urlAttribute.Value);
            }
        }

        embed.WithTitle(title.TrimTo(256));
        var desc = feedItem.Description?.StripHtml();
        if (!string.IsNullOrWhiteSpace(feedItem.Description)) embed.WithDescription(desc.TrimTo(2048));
        var (builder, content, componentBuilder) = await GetFeedEmbed(repbuilder.Replace(sub.Message), channel.GuildId);
        if (sub.Message is "-" or null) await channel.EmbedAsync(embed);
        else
            await channel.SendMessageAsync(content ?? "", embeds: builder ?? null,
                components: componentBuilder?.Build());
    }

    private Task<(Embed[] builder, string content, ComponentBuilder componentBuilder)> GetFeedEmbed(string message,
        ulong guildId)
    {
        return SmartEmbed.TryParse(message, guildId, out var embed, out var content, out var components)
            ? Task.FromResult((embed, content, components))
            : Task.FromResult<(Embed[], string, ComponentBuilder)>(([], message, null));
    }


    /// <summary>
    ///     Retrieves all feed subscriptions for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <returns>A list of feed subscriptions.</returns>
    public async Task<List<FeedSub?>> GetFeeds(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        // Direct query with GuildId filter
        return await db.FeedSubs
            .Where(fs => fs.GuildId == guildId)
            .OrderBy(x => x.Id)
            .ToListAsync();
    }

    /// <summary>
    ///     Adds a new RSS feed subscription to a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="channelId">The ID of the channel to receive updates.</param>
    /// <param name="rssFeed">The URL of the RSS feed.</param>
    /// <returns><c>true</c> if the feed subscription was successfully added; otherwise, <c>false</c>.</returns>
    public async Task<bool> AddFeed(ulong guildId, ulong channelId, string rssFeed)
    {
        rssFeed.ThrowIfNull(nameof(rssFeed));

        await using var db = await dbFactory.CreateConnectionAsync();

        // Check if feed already exists for this guild
        var trimmedFeed = rssFeed.Trim().ToLower();
        var exists = await db.FeedSubs
            .AnyAsync(x => x.GuildId == guildId && x.Url.ToLower() == trimmedFeed);

        if (exists)
            return false;

        // Check feed count limit
        var feedCount = await db.FeedSubs
            .CountAsync(x => x.GuildId == guildId);

        if (feedCount >= 10)
            return false;

        // Create and add new feed with GuildId
        var fs = new FeedSub
        {
            GuildId = guildId, ChannelId = channelId, Url = rssFeed.Trim()
        };

        await db.InsertAsync(fs);

        // Get all feeds for this guild to update subscriptions
        var guildFeeds = await db.FeedSubs
            .Where(x => x.GuildId == guildId)
            .ToListAsync();

        // Update subscriptions
        foreach (var feed in guildFeeds)
            subs.AddOrUpdate(feed.Url.ToLower(), [feed], (_, old) =>
            {
                old.Add(feed);
                return old;
            });

        return true;
    }


    /// <summary>
    ///     Adds or updates the message template for a feed subscription.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="index">The index of the feed subscription.</param>
    /// <param name="message">The message template to set.</param>
    /// <returns><c>true</c> if the message template was successfully updated; otherwise, <c>false</c>.</returns>
    public async Task<bool> AddFeedMessage(ulong guildId, int index, string message)
    {
        if (index < 0)
            return false;

        await using var db = await dbFactory.CreateConnectionAsync();

        // Direct query with GuildId filter
        var items = await db.FeedSubs
            .Where(fs => fs.GuildId == guildId)
            .OrderBy(x => x.Id)
            .ToListAsync();

        if (items.Count <= index)
            return false;

        var toupdate = items[index];

        subs.AddOrUpdate(toupdate.Url.ToLower(), [], (_, old) =>
        {
            old.Remove(toupdate);
            return old;
        });

        toupdate.Message = message;
        await db.UpdateAsync(toupdate);

        subs.AddOrUpdate(toupdate.Url.ToLower(), [], (_, old) =>
        {
            old.Add(toupdate);
            return old;
        });

        return true;
    }

    /// <summary>
    ///     Removes a feed subscription from a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="index">The index of the feed subscription to remove.</param>
    /// <returns><c>true</c> if the feed subscription was successfully removed; otherwise, <c>false</c>.</returns>
    public async Task<bool> RemoveFeed(ulong guildId, int index)
    {
        if (index < 0)
            return false;

        await using var db = await dbFactory.CreateConnectionAsync();

        // Direct query with GuildId filter
        var items = await db.FeedSubs
            .Where(fs => fs.GuildId == guildId)
            .OrderBy(x => x.Id)
            .ToListAsync();

        if (items.Count <= index)
            return false;

        var toRemove = items[index];

        subs.AddOrUpdate(toRemove.Url.ToLower(), [], (_, old) =>
        {
            old.Remove(toRemove);
            return old;
        });

        await db.FeedSubs
            .Where(x => x.Id == toRemove.Id)
            .DeleteAsync();

        return true;
    }
}