using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CodeHollow.FeedReader;
using CodeHollow.FeedReader.Feeds;
using Discord;
using Discord.WebSocket;
using Mewdeko.Services;
using Mewdeko.Services.Database.Models;
using Mewdeko.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Modules.Searches.Services
{
    public class FeedsService : INService
    {
        private readonly DiscordSocketClient _client;
        private readonly DbService _db;

        private readonly ConcurrentDictionary<string, DateTime> _lastPosts =
            new();

        private readonly ConcurrentDictionary<string, HashSet<FeedSub>> _subs;

        public FeedsService(Mewdeko bot, DbService db, DiscordSocketClient client)
        {
            _db = db;

            using (var uow = db.GetDbContext())
            {
                var guildConfigIds = bot.AllGuildConfigs.Select(x => x.Id).ToList();
                _subs = uow._context.GuildConfigs
                    .AsQueryable()
                    .Where(x => guildConfigIds.Contains(x.Id))
                    .Include(x => x.FeedSubs)
                    .ThenInclude(x => x.GuildConfig)
                    .ToList()
                    .SelectMany(x => x.FeedSubs)
                    .GroupBy(x => x.Url.ToLower())
                    .ToDictionary(x => x.Key, x => x.ToHashSet())
                    .ToConcurrent();
            }

            _client = client;

            var _ = Task.Run(TrackFeeds);
        }

        public async Task<EmbedBuilder> TrackFeeds()
        {
            while (true)
            {
                var allSendTasks = new List<Task>(_subs.Count);
                foreach (var kvp in _subs)
                {
                    if (kvp.Value.Count == 0)
                        continue;

                    var rssUrl = kvp.Key;
                    try
                    {
                        var feed = await FeedReader.ReadAsync(rssUrl).ConfigureAwait(false);

                        var items = feed
                            .Items
                            .Select(item => (Item: item, LastUpdate: item.PublishingDate?.ToUniversalTime()
                                                                     ?? (item.SpecificItem as AtomFeedItem)?.UpdatedDate
                                                                     ?.ToUniversalTime()))
                            .Where(data => !(data.LastUpdate is null))
                            .Select(data => (data.Item, LastUpdate: (DateTime)data.LastUpdate))
                            .OrderByDescending(data => data.LastUpdate)
                            .Reverse() // start from the oldest
                            .ToList();

                        if (!_lastPosts.TryGetValue(kvp.Key, out var lastFeedUpdate))
                            lastFeedUpdate = _lastPosts[kvp.Key] =
                                items.Any() ? items[items.Count - 1].LastUpdate : DateTime.UtcNow;

                        foreach (var (feedItem, itemUpdateDate) in items)
                        {
                            if (itemUpdateDate <= lastFeedUpdate) continue;

                            var embed = new EmbedBuilder()
                                .WithFooter(rssUrl);

                            _lastPosts[kvp.Key] = itemUpdateDate;

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
                                    .FirstOrDefault(x => x.Name.LocalName == "preview");

                                if (previewElement == null)
                                    previewElement = afi.Element.Elements()
                                        .FirstOrDefault(x => x.Name.LocalName == "thumbnail");

                                if (previewElement != null)
                                {
                                    var urlAttribute = previewElement.Attribute("url");
                                    if (urlAttribute != null && !string.IsNullOrWhiteSpace(urlAttribute.Value)
                                                             && Uri.IsWellFormedUriString(urlAttribute.Value,
                                                                 UriKind.Absolute))
                                    {
                                        embed.WithImageUrl(urlAttribute.Value);
                                        gotImage = true;
                                    }
                                }
                            }


                            embed.WithTitle(title.TrimTo(256));

                            var desc = feedItem.Description?.StripHTML();
                            if (!string.IsNullOrWhiteSpace(feedItem.Description))
                                embed.WithDescription(desc.TrimTo(2048));

                            //send the created embed to all subscribed channels
                            var feedSendTasks = kvp.Value
                                .Where(x => x.GuildConfig != null)
                                .Select(x => _client.GetGuild(x.GuildConfig.GuildId)
                                    ?.GetTextChannel(x.ChannelId))
                                .Where(x => x != null)
                                .Select(x => x.EmbedAsync(embed));

                            allSendTasks.Add(Task.WhenAll(feedSendTasks));
                        }
                    }
                    catch
                    {
                    }
                }

                await Task.WhenAll(Task.WhenAll(allSendTasks), Task.Delay(10000)).ConfigureAwait(false);
            }
        }

        public List<FeedSub> GetFeeds(ulong guildId)
        {
            using (var uow = _db.GetDbContext())
            {
                return uow.GuildConfigs.ForId(guildId,
                        set => set.Include(x => x.FeedSubs)
                            .ThenInclude(x => x.GuildConfig))
                    .FeedSubs
                    .OrderBy(x => x.Id)
                    .ToList();
            }
        }

        public bool AddFeed(ulong guildId, ulong channelId, string rssFeed)
        {
            rssFeed.ThrowIfNull(nameof(rssFeed));

            var fs = new FeedSub
            {
                ChannelId = channelId,
                Url = rssFeed.Trim()
            };

            using (var uow = _db.GetDbContext())
            {
                var gc = uow.GuildConfigs.ForId(guildId,
                    set => set.Include(x => x.FeedSubs)
                        .ThenInclude(x => x.GuildConfig));

                if (gc.FeedSubs.Any(x => x.Url.ToLower() == fs.Url.ToLower()))
                    return false;
                if (gc.FeedSubs.Count >= 10) return false;

                gc.FeedSubs.Add(fs);
                uow.SaveChanges();
                //adding all, in case bot wasn't on this guild when it started
                foreach (var feed in gc.FeedSubs)
                    _subs.AddOrUpdate(feed.Url.ToLower(), new HashSet<FeedSub> { feed }, (k, old) =>
                    {
                        old.Add(feed);
                        return old;
                    });
            }

            return true;
        }

        public bool RemoveFeed(ulong guildId, int index)
        {
            if (index < 0)
                return false;

            using (var uow = _db.GetDbContext())
            {
                var items = uow.GuildConfigs.ForId(guildId, set => set.Include(x => x.FeedSubs))
                    .FeedSubs
                    .OrderBy(x => x.Id)
                    .ToList();

                if (items.Count <= index)
                    return false;
                var toRemove = items[index];
                _subs.AddOrUpdate(toRemove.Url.ToLower(), new HashSet<FeedSub>(), (key, old) =>
                {
                    old.Remove(toRemove);
                    return old;
                });
                uow._context.Remove(toRemove);
                uow.SaveChanges();
            }

            return true;
        }
    }
}