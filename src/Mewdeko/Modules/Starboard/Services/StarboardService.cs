using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Modules.Starboard.Common;
using Mewdeko.Services.Analytics;
using Mewdeko.Services.Strings;

namespace Mewdeko.Modules.Starboard.Services;

/// <summary>
///     Service responsible for managing multiple starboards in Discord servers.
/// </summary>
public class StarboardService : INService, IReadyExecutor, IUnloadableService
{
    private readonly DiscordShardedClient client;
    private readonly IAnalyticsCollector collector;
    private readonly IDataConnectionFactory dbFactory;
    private readonly EventHandler eventHandler;
    private readonly ILogger<StarboardService> logger;
    private readonly GeneratedBotStrings strings;
    private List<DataModel.Starboard> starboardConfigs = [];
    private List<StarboardPost> starboardPosts = [];
    private List<StarboardReaction> starboardReactions = [];
    private List<StarboardStats> starboardStats = [];

    /// <summary>
    ///     Initializes a new instance of the <see cref="StarboardService" /> class.
    /// </summary>
    /// <param name="client">The Discord socket client.</param>
    /// <param name="dbFactory">The database context provider.</param>
    /// <param name="eventHandler">The event handler.</param>
    /// <param name="strings">The localized strings service.</param>
    /// <param name="logger">The logger instance for structured logging.</param>
    /// <param name="collector">The analytics collector.</param>
    public StarboardService(DiscordShardedClient client, IDataConnectionFactory dbFactory,
        EventHandler eventHandler, GeneratedBotStrings strings, ILogger<StarboardService> logger,
        IAnalyticsCollector collector)
    {
        this.client = client;
        this.dbFactory = dbFactory;
        this.eventHandler = eventHandler;
        this.strings = strings;
        this.logger = logger;
        this.collector = collector;
        eventHandler.Subscribe("ReactionAdded", "StarboardService", OnReactionAddedAsync);
        eventHandler.Subscribe("MessageDeleted", "StarboardService", OnMessageDeletedAsync);
        eventHandler.Subscribe("ReactionRemoved", "StarboardService", OnReactionRemoveAsync);
        eventHandler.Subscribe("ReactionsCleared", "StarboardService", OnAllReactionsClearedAsync);
    }

    /// <inheritdoc />
    public async Task OnReadyAsync()
    {
        logger.LogInformation($"Starting {GetType()} Cache");
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        starboardPosts = await dbContext.StarboardPosts.ToListAsync();
        starboardConfigs = await dbContext.Starboards.ToListAsync();
        starboardStats = await dbContext.StarboardStats.Where(x => x.IsActive).ToListAsync();
        starboardReactions = await dbContext.StarboardReactions.ToListAsync();
        logger.LogInformation("Starboard Cache Ready");
    }

    /// <summary>
    ///     Unloads the service and unsubscribes from events.
    /// </summary>
    public Task Unload()
    {
        eventHandler.Unsubscribe("ReactionAdded", "StarboardService", OnReactionAddedAsync);
        eventHandler.Unsubscribe("MessageDeleted", "StarboardService", OnMessageDeletedAsync);
        eventHandler.Unsubscribe("ReactionRemoved", "StarboardService", OnReactionRemoveAsync);
        eventHandler.Unsubscribe("ReactionsCleared", "StarboardService", OnAllReactionsClearedAsync);
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Creates a new starboard configuration for a guild.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="channelId">The ID of the starboard channel.</param>
    /// <param name="emote">The emote to use for this starboard.</param>
    /// <param name="threshold">The number of reactions required.</param>
    /// <returns>The ID of the created starboard configuration.</returns>
    public async Task<int> CreateStarboard(IGuild guild, ulong channelId, string emote, int threshold)
    {
        // Validate emotes don't collide with existing starboards
        var emotes = ParseEmotes(emote);
        var existingStarboards = GetStarboards(guild.Id);

        foreach (var existingStarboard in existingStarboards)
        {
            var existingEmotes = ParseEmotes(existingStarboard.Emote);
            var collision = emotes.Intersect(existingEmotes).FirstOrDefault();
            if (collision != null)
                throw new InvalidOperationException(
                    $"Emote {collision} is already used by another starboard in this guild.");
        }

        await using var db = await dbFactory.CreateConnectionAsync();
        var config = new DataModel.Starboard
        {
            GuildId = guild.Id,
            StarboardChannelId = channelId,
            Emote = emote,
            Threshold = threshold,
            CheckedChannels = "",
            UseBlacklist = false,
            AllowBots = false,
            RemoveOnDelete = true,
            RemoveOnReactionsClear = true,
            RemoveOnBelowThreshold = true,
            RepostThreshold = 0
        };

        config.Id = await db.InsertWithInt32IdentityAsync(config);
        starboardConfigs.Add(config);
        return config.Id;
    }

    /// <summary>
    ///     Deletes a starboard configuration.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="starboardId">The ID of the starboard configuration.</param>
    /// <returns>True if the starboard was deleted, false otherwise.</returns>
    public async Task<bool> DeleteStarboard(IGuild guild, int starboardId)
    {
        var config = starboardConfigs.FirstOrDefault(x => x.Id == starboardId && x.GuildId == guild.Id);
        if (config == null)
            return false;

        await using var db = await dbFactory.CreateConnectionAsync();
        await db.DeleteAsync(config);

        starboardConfigs.Remove(config);
        return true;
    }

    /// <summary>
    ///     Gets all starboard configurations for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <returns>A list of starboard configurations.</returns>
    public List<DataModel.Starboard> GetStarboards(ulong guildId)
        => starboardConfigs.Where(x => x.GuildId == guildId).ToList();

    /// <summary>
    ///     Matches the author link line a starboard repost starts its container with.
    /// </summary>
    private static readonly Regex RepostAuthorRegex =
        new(@"^\[\*\*(.+?)\*\*\]\(https://discord\.com/users/(\d+)\)$", RegexOptions.Compiled);

    /// <summary>
    ///     Matches a Discord message jump link and captures its channel and message ids.
    /// </summary>
    private static readonly Regex JumpLinkRegex =
        new(@"discord(?:app)?\.com/channels/\d+/(\d+)/(\d+)", RegexOptions.Compiled);

    /// <summary>
    ///     Matches a bolded reaction count on a starboard repost stars line.
    /// </summary>
    private static readonly Regex StarsCountRegex = new(@"\*\*(\d+)\*\*", RegexOptions.Compiled);

    /// <summary>
    ///     Gets starboard highlights for a guild, resolving each post back to the original message so the
    ///     real content, author, image and channel are returned.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="limit">The maximum number of highlights to return, clamped to 1 through 25.</param>
    /// <param name="sort">
    ///     "top" orders by star count (current or peak, whichever is higher); anything else returns the most
    ///     recently starred posts first.
    /// </param>
    /// <returns>A list of starboard highlights.</returns>
    public async Task<List<StarboardHighlight>> GetRecentHighlights(ulong guildId, int limit = 5,
        string? sort = null)
    {
        var sortByTop = string.Equals(sort, "top", StringComparison.OrdinalIgnoreCase);
        limit = Math.Clamp(limit, 1, 25);

        var guildStarboards = starboardConfigs.Where(s => s.GuildId == guildId).ToList();
        if (guildStarboards.Count == 0)
            return [];

        var guild = client.GetGuild(guildId);
        if (guild == null)
            return [];

        var starboardIds = guildStarboards.Select(gs => gs.Id).ToList();
        var statsByMessage = starboardStats
            .ToList()
            .Where(s => starboardIds.Contains(s.StarboardId))
            .GroupBy(s => s.MessageId)
            .ToDictionary(g => g.Key, g => g.ToList());

        await using var dbContext = await dbFactory.CreateConnectionAsync();
        var postsQuery = dbContext.StarboardPosts.Where(sp => starboardIds.Contains(sp.StarboardConfigId));

        List<StarboardPost> candidates;
        if (sortByTop)
        {
            var allPosts = await postsQuery.ToListAsync();
            candidates = allPosts
                .OrderByDescending(p => HighlightScore(p, statsByMessage))
                .ThenByDescending(p => p.Id)
                .Take(limit * 3)
                .ToList();
        }
        else
        {
            candidates = await postsQuery
                .OrderByDescending(sp => sp.Id)
                .Take(limit * 3)
                .ToListAsync();
        }

        var highlights = new List<StarboardHighlight>();

        foreach (var post in candidates)
        {
            if (highlights.Count >= limit)
                break;

            try
            {
                var highlight = await BuildHighlight(guild, post, guildStarboards, statsByMessage);
                if (highlight != null)
                    highlights.Add(highlight);
            }
            catch (Exception ex)
            {
                logger.LogWarning("Failed to process starboard highlight for post {PostId}: {Message}", post.Id,
                    ex.Message);
            }
        }

        return sortByTop
            ? highlights.OrderByDescending(h => h.StarCount).ToList()
            : highlights;
    }

    /// <summary>
    ///     Scores a starboard post for the "top" ordering: the higher of its stored reaction count and the
    ///     peak count tracked in the starboard stats.
    /// </summary>
    private static int HighlightScore(StarboardPost post, Dictionary<ulong, List<StarboardStats>> statsByMessage)
    {
        var peak = statsByMessage.TryGetValue(post.MessageId, out var stats)
            ? stats.Where(s => s.StarboardId == post.StarboardConfigId).Select(s => s.PeakReactionCount)
                .DefaultIfEmpty(0).Max()
            : 0;
        return Math.Max(post.ReactionCount ?? 0, peak);
    }

    /// <summary>
    ///     Builds a single highlight. The original message is preferred; the starboard repost is only parsed
    ///     when the original can no longer be fetched. Returns null when neither message exists.
    /// </summary>
    private async Task<StarboardHighlight?> BuildHighlight(SocketGuild guild, StarboardPost post,
        List<DataModel.Starboard> guildStarboards, Dictionary<ulong, List<StarboardStats>> statsByMessage)
    {
        var starboardConfig = guildStarboards.FirstOrDefault(s => s.Id == post.StarboardConfigId);
        if (starboardConfig == null)
            return null;

        statsByMessage.TryGetValue(post.MessageId, out var messageStats);
        var stat = messageStats?.FirstOrDefault(s => s.StarboardId == post.StarboardConfigId)
                   ?? messageStats?.FirstOrDefault();

        var channelId = stat?.ChannelId ?? 0;
        IMessage? repost = null;

        if (channelId == 0)
        {
            repost = await TryGetMessageAsync(guild, starboardConfig.StarboardChannelId, post.PostId);
            channelId = FindJumpChannelId(repost, post.MessageId);
        }

        var original = channelId == 0 ? null : await TryGetMessageAsync(guild, channelId, post.MessageId);

        string? content;
        string authorName;
        string? authorAvatarUrl;
        string? imageUrl;

        if (original != null)
        {
            content = string.IsNullOrWhiteSpace(original.Content)
                ? original.Embeds.Select(e => e.Description).FirstOrDefault(d => !string.IsNullOrWhiteSpace(d))
                : original.Content;

            var member = guild.GetUser(original.Author.Id);
            authorName = member?.DisplayName ?? original.Author.GlobalName ?? original.Author.Username;
            authorAvatarUrl = member?.GetGuildAvatarUrl() ?? original.Author.GetDisplayAvatarUrl();
            imageUrl = FindImageUrl(original);
        }
        else
        {
            repost ??= await TryGetMessageAsync(guild, starboardConfig.StarboardChannelId, post.PostId);
            if (repost == null)
                return null;

            (content, authorName, authorAvatarUrl, imageUrl) = await ParseRepost(guild, repost);
        }

        var starboardEmotes = ParseEmotes(starboardConfig.Emote);
        var starEmote = !string.IsNullOrWhiteSpace(stat?.Emote) ? stat.Emote : starboardEmotes.FirstOrDefault();

        var starCount = post.ReactionCount ?? 0;
        if (starCount == 0)
            starCount = messageStats?.Where(s => s.StarboardId == post.StarboardConfigId)
                .Sum(s => s.ReactionCount) ?? 0;
        if (starCount == 0 && repost != null)
            starCount = ParseRepostStarCount(repost);

        return new StarboardHighlight
        {
            MessageId = post.MessageId,
            ChannelId = channelId,
            StarCount = starCount,
            Content = string.IsNullOrWhiteSpace(content) ? null : content.Trim(),
            AuthorName = authorName,
            AuthorAvatarUrl = string.IsNullOrWhiteSpace(authorAvatarUrl) ? null : authorAvatarUrl,
            ImageUrl = string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl,
            StarEmote = starEmote ?? "⭐",
            CreatedAt = original?.Timestamp.UtcDateTime
                        ?? post.DateAdded
                        ?? stat?.FirstStarredAt
                        ?? SnowflakeUtils.FromSnowflake(post.MessageId).UtcDateTime
        };
    }

    /// <summary>
    ///     Fetches a message from a guild channel, using the cache first and REST second. Returns null when the
    ///     channel is gone, is not a message channel, or the message was deleted or is not readable.
    /// </summary>
    private static async Task<IMessage?> TryGetMessageAsync(SocketGuild guild, ulong channelId, ulong messageId)
    {
        if (channelId == 0 || guild.GetChannel(channelId) is not IMessageChannel channel)
            return null;

        try
        {
            return await channel.GetMessageAsync(messageId);
        }
        catch (Discord.Net.HttpException)
        {
            return null;
        }
    }

    /// <summary>
    ///     Finds the source channel id in a starboard repost by reading its jump link, from the link button on
    ///     Components V2 reposts or from the embed on legacy reposts. Returns 0 when none is found.
    /// </summary>
    private static ulong FindJumpChannelId(IMessage? repost, ulong messageId)
    {
        if (repost == null)
            return 0;

        var sources = repost.Components
            .OfType<ActionRowComponent>()
            .SelectMany(row => row.Components.OfType<ButtonComponent>())
            .Select(button => button.Url)
            .Concat(repost.Embeds.SelectMany(e =>
                new[] { e.Url, e.Description }.Concat(e.Fields.Select(f => f.Value))))
            .Append(repost.Content);

        foreach (var source in sources)
        {
            if (string.IsNullOrEmpty(source))
                continue;

            foreach (Match match in JumpLinkRegex.Matches(source))
            {
                if (ulong.TryParse(match.Groups[2].Value, out var linkedMessage) && linkedMessage == messageId &&
                    ulong.TryParse(match.Groups[1].Value, out var linkedChannel))
                    return linkedChannel;
            }
        }

        return 0;
    }

    /// <summary>
    ///     Finds the first image on a message: an image attachment first, then an embed image or thumbnail.
    /// </summary>
    private static string? FindImageUrl(IMessage message)
    {
        var attachment = message.Attachments.FirstOrDefault(a =>
            a.ContentType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true
            || IsImageExtension(Path.GetExtension(a.Filename).ToLowerInvariant()));
        if (attachment != null)
            return attachment.ProxyUrl ?? attachment.Url;

        foreach (var embed in message.Embeds)
        {
            if (embed.Image.HasValue)
                return embed.Image.Value.Url;
            if (embed.Thumbnail.HasValue)
                return embed.Thumbnail.Value.Url;
        }

        return null;
    }

    /// <summary>
    ///     Reads content, author and image back out of a starboard repost. Handles Components V2 reposts (a
    ///     container of an author link, the content and a media gallery), legacy embed reposts and legacy plain
    ///     text reposts.
    /// </summary>
    private async Task<(string? Content, string AuthorName, string? AuthorAvatarUrl, string? ImageUrl)>
        ParseRepost(SocketGuild guild, IMessage repost)
    {
        if (repost.Embeds.Count > 0)
        {
            var embed = repost.Embeds.First();
            var embedImage = embed.Image.HasValue ? embed.Image.Value.Url :
                embed.Thumbnail.HasValue ? embed.Thumbnail.Value.Url : null;
            return (embed.Description, embed.Author?.Name ?? "Unknown User", embed.Author?.IconUrl, embedImage);
        }

        var textParts = new List<string>();
        string? imageUrl = null;
        string? authorName = null;
        ulong authorId = 0;

        var containers = repost.Components.OfType<ContainerComponent>().ToList();
        foreach (var child in containers.SelectMany(c => c.Components))
        {
            switch (child)
            {
                case TextDisplayComponent text when authorName == null && RepostAuthorRegex.IsMatch(text.Content):
                    var authorMatch = RepostAuthorRegex.Match(text.Content);
                    authorName = authorMatch.Groups[1].Value;
                    ulong.TryParse(authorMatch.Groups[2].Value, out authorId);
                    break;
                case TextDisplayComponent text:
                    if (!string.IsNullOrWhiteSpace(text.Content))
                        textParts.Add(text.Content);
                    break;
                case MediaGalleryComponent gallery when imageUrl == null:
                    imageUrl = gallery.Items.Select(item => item.Media.Url)
                        .FirstOrDefault(url => !string.IsNullOrWhiteSpace(url));
                    break;
            }
        }

        if (containers.Count == 0 && !string.IsNullOrWhiteSpace(repost.Content))
        {
            var legacyMatch = Regex.Match(repost.Content, @"\[\*\*(.+?)\*\*\]\(https://discord\.com/users/(\d+)\)");
            if (legacyMatch.Success)
            {
                authorName = legacyMatch.Groups[1].Value;
                ulong.TryParse(legacyMatch.Groups[2].Value, out authorId);
                textParts.Add(repost.Content.Replace(legacyMatch.Value, "").Trim());
            }
        }

        string? authorAvatarUrl = null;
        if (authorId != 0)
        {
            var member = guild.GetUser(authorId);
            if (member != null)
            {
                authorName = member.DisplayName;
                authorAvatarUrl = member.GetGuildAvatarUrl() ?? member.GetDisplayAvatarUrl();
            }
            else
            {
                try
                {
                    var user = await client.Rest.GetUserAsync(authorId);
                    if (user != null)
                    {
                        authorName = user.GlobalName ?? user.Username;
                        authorAvatarUrl = user.GetDisplayAvatarUrl();
                    }
                }
                catch (Discord.Net.HttpException)
                {
                    authorAvatarUrl = CDN.GetDefaultUserAvatarUrl(authorId);
                }
            }
        }

        var content = textParts.Count > 0 ? string.Join('\n', textParts) : null;
        return (content, authorName ?? "Unknown User", authorAvatarUrl, imageUrl);
    }

    /// <summary>
    ///     Sums the bolded reaction counts on a repost stars line, which is the first top level text display on
    ///     Components V2 reposts and the message content on legacy ones.
    /// </summary>
    private static int ParseRepostStarCount(IMessage repost)
    {
        var starsLine = repost.Components.OfType<TextDisplayComponent>().FirstOrDefault()?.Content
                        ?? repost.Content;
        if (string.IsNullOrWhiteSpace(starsLine))
            return 0;

        var total = StarsCountRegex.Matches(starsLine)
            .Sum(m => int.TryParse(m.Groups[1].Value, out var n) ? n : 0);
        if (total > 0)
            return total;

        var digits = new string(starsLine.Split(' ')[0].Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out var legacy) ? legacy : 0;
    }

    /// <summary>
    ///     Sets whether bots are allowed to be starred for a specific starboard.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="starboardId">The ID of the starboard configuration.</param>
    /// <param name="allowed">Whether bots are allowed.</param>
    /// <returns>True if the setting was updated, false if the starboard wasn't found.</returns>
    public async Task<bool> SetAllowBots(IGuild guild, int starboardId, bool allowed)
    {
        var config = starboardConfigs.FirstOrDefault(x => x.Id == starboardId && x.GuildId == guild.Id);
        if (config == null)
            return false;

        await using var db = await dbFactory.CreateConnectionAsync();
        config.AllowBots = allowed;
        await db.UpdateAsync(config);

        return true;
    }

    /// <summary>
    ///     Sets whether to remove starred messages when the original is deleted.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="starboardId">The ID of the starboard configuration.</param>
    /// <param name="removeOnDelete">Whether to remove on delete.</param>
    /// <returns>True if the setting was updated, false if the starboard wasn't found.</returns>
    public async Task<bool> SetRemoveOnDelete(IGuild guild, int starboardId, bool removeOnDelete)
    {
        var config = starboardConfigs.FirstOrDefault(x => x.Id == starboardId && x.GuildId == guild.Id);
        if (config == null)
            return false;

        await using var db = await dbFactory.CreateConnectionAsync();
        config.RemoveOnDelete = removeOnDelete;
        await db.UpdateAsync(config);

        return true;
    }

    /// <summary>
    ///     Sets whether to remove starred messages when reactions are cleared.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="starboardId">The ID of the starboard configuration.</param>
    /// <param name="removeOnClear">Whether to remove on clear.</param>
    /// <returns>True if the setting was updated, false if the starboard wasn't found.</returns>
    public async Task<bool> SetRemoveOnClear(IGuild guild, int starboardId, bool removeOnClear)
    {
        var config = starboardConfigs.FirstOrDefault(x => x.Id == starboardId && x.GuildId == guild.Id);
        if (config == null)
            return false;

        await using var db = await dbFactory.CreateConnectionAsync();
        config.RemoveOnReactionsClear = removeOnClear;
        await db.UpdateAsync(config);

        return true;
    }

    /// <summary>
    ///     Sets whether to remove starred messages when they fall below threshold.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="starboardId">The ID of the starboard configuration.</param>
    /// <param name="removeBelowThreshold">Whether to remove below threshold.</param>
    /// <returns>True if the setting was updated, false if the starboard wasn't found.</returns>
    public async Task<bool> SetRemoveBelowThreshold(IGuild guild, int starboardId, bool removeBelowThreshold)
    {
        var config = starboardConfigs.FirstOrDefault(x => x.Id == starboardId && x.GuildId == guild.Id);
        if (config == null)
            return false;

        await using var db = await dbFactory.CreateConnectionAsync();
        config.RemoveOnBelowThreshold = removeBelowThreshold;
        await db.UpdateAsync(config);

        return true;
    }

    /// <summary>
    ///     Sets the repost threshold for a starboard.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="starboardId">The ID of the starboard configuration.</param>
    /// <param name="threshold">The threshold value.</param>
    /// <returns>True if the setting was updated, false if the starboard wasn't found.</returns>
    public async Task<bool> SetRepostThreshold(IGuild guild, int starboardId, int threshold)
    {
        var config = starboardConfigs.FirstOrDefault(x => x.Id == starboardId && x.GuildId == guild.Id);
        if (config == null)
            return false;

        await using var db = await dbFactory.CreateConnectionAsync();
        config.RepostThreshold = threshold;
        await db.UpdateAsync(config);

        return true;
    }

    /// <summary>
    ///     Sets the star threshold for a starboard.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="starboardId">The ID of the starboard configuration.</param>
    /// <param name="threshold">The threshold value.</param>
    /// <returns>True if the setting was updated, false if the starboard wasn't found.</returns>
    public async Task<bool> SetStarThreshold(IGuild guild, int starboardId, int threshold)
    {
        var config = starboardConfigs.FirstOrDefault(x => x.Id == starboardId && x.GuildId == guild.Id);
        if (config == null)
            return false;

        await using var db = await dbFactory.CreateConnectionAsync();
        config.Threshold = threshold;
        await db.UpdateAsync(config);

        return true;
    }

    /// <summary>
    ///     Sets whether to use blacklist mode for channel checking.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="starboardId">The ID of the starboard configuration.</param>
    /// <param name="useBlacklist">Whether to use blacklist mode.</param>
    /// <returns>True if the setting was updated, false if the starboard wasn't found.</returns>
    public async Task<bool> SetUseBlacklist(IGuild guild, int starboardId, bool useBlacklist)
    {
        var config = starboardConfigs.FirstOrDefault(x => x.Id == starboardId && x.GuildId == guild.Id);
        if (config == null)
            return false;

        await using var db = await dbFactory.CreateConnectionAsync();
        config.UseBlacklist = useBlacklist;
        await db.UpdateAsync(config);

        return true;
    }

    /// <summary>
    ///     Toggles a channel in the starboard's check list.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="starboardId">The ID of the starboard configuration.</param>
    /// <param name="channelId">The channel ID to toggle.</param>
    /// <returns>A tuple containing whether the channel was added and the starboard configuration.</returns>
    public async Task<(bool WasAdded, DataModel.Starboard Config)> ToggleChannel(IGuild guild, int starboardId,
        string channelId)
    {
        var config = starboardConfigs.FirstOrDefault(x => x.Id == starboardId && x.GuildId == guild.Id);
        if (config == null)
            return (false, null);

        var channels = config.CheckedChannels.Split(" ", StringSplitOptions.RemoveEmptyEntries).ToList();

        await using var db = await dbFactory.CreateConnectionAsync();
        if (!channels.Contains(channelId))
        {
            channels.Add(channelId);
            config.CheckedChannels = string.Join(" ", channels);
            await db.UpdateAsync(config);

            return (true, config);
        }

        channels.Remove(channelId);
        config.CheckedChannels = string.Join(" ", channels);
        await db.UpdateAsync(config);

        return (false, config);
    }

    /// <summary>
    ///     Adds a new emote to an existing starboard configuration.
    /// </summary>
    public async Task<bool> AddEmoteToStarboard(IGuild guild, int starboardId, string newEmote)
    {
        var config = starboardConfigs.FirstOrDefault(x => x.Id == starboardId && x.GuildId == guild.Id);
        if (config == null)
            return false;

        // Check for collisions
        var existingStarboards = GetStarboards(guild.Id).Where(x => x.Id != starboardId);
        foreach (var existingStarboard in existingStarboards)
        {
            var existingEmotes = ParseEmotes(existingStarboard.Emote);
            if (existingEmotes.Contains(newEmote))
                throw new InvalidOperationException(
                    $"Emote {newEmote} is already used by another starboard in this guild.");
        }

        var currentEmotes = ParseEmotes(config.Emote);
        if (currentEmotes.Contains(newEmote))
            return false; // Already has this emote

        currentEmotes.Add(newEmote);
        config.Emote = string.Join("|", currentEmotes);

        await using var db = await dbFactory.CreateConnectionAsync();
        await db.UpdateAsync(config);

        return true;
    }

    /// <summary>
    ///     Removes an emote from a starboard configuration.
    /// </summary>
    public async Task<bool> RemoveEmoteFromStarboard(IGuild guild, int starboardId, string emoteToRemove)
    {
        var config = starboardConfigs.FirstOrDefault(x => x.Id == starboardId && x.GuildId == guild.Id);
        if (config == null)
            return false;

        var currentEmotes = ParseEmotes(config.Emote);
        if (!currentEmotes.Remove(emoteToRemove))
            return false; // Didn't have this emote

        if (currentEmotes.Count == 0)
            throw new InvalidOperationException(
                "Cannot remove the last emote from a starboard. Delete the starboard instead.");

        config.Emote = string.Join("|", currentEmotes);

        await using var db = await dbFactory.CreateConnectionAsync();
        await db.UpdateAsync(config);

        return true;
    }

    /// <summary>
    ///     Gets starboard statistics for a guild.
    /// </summary>
    public async Task<StarboardStatsDto?> GetStarboardStats(ulong guildId)
    {
        return await GetGuildStarboardStats(guildId);
    }

    /// <summary>
    ///     Gets starboard statistics for a specific user in a guild.
    /// </summary>
    public async Task<UserStarboardStatsDto?> GetUserStarboardStats(ulong guildId, ulong userId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var guildStarboards = starboardConfigs.Where(x => x.GuildId == guildId).Select(x => x.Id).ToList();
        if (guildStarboards.Count == 0)
            return null;

        var userStats = starboardStats.Where(x =>
            guildStarboards.Contains(x.StarboardId) && x.IsActive && x.AuthorId == userId);
        var userReactions =
            starboardReactions.Where(x => guildStarboards.Contains(x.StarboardId) && x.UserId == userId);
        var receivedReactions = starboardReactions.Where(x => guildStarboards.Contains(x.StarboardId));

        var messagesStarred = userStats.Count();
        var totalStarsReceived = userStats.Sum(x => x.PeakReactionCount);
        var totalStarsGiven = userReactions.Count();

        // Get top starred posts by this user
        var topStarredPosts = userStats.OrderByDescending(x => x.PeakReactionCount)
            .Take(3)
            .Select(x => new TopStarredPost(x.MessageId, x.PeakReactionCount, x.Emote))
            .ToList();

        // Get who this user stars the most (their "idols")
        var starredUsers = receivedReactions
            .Where(r => userReactions.Any(ur => ur.MessageId == r.MessageId))
            .Join(starboardStats.Where(s => guildStarboards.Contains(s.StarboardId)),
                r => new
                {
                    r.MessageId, r.Emote
                },
                s => new
                {
                    s.MessageId, s.Emote
                },
                (r, s) => s.AuthorId)
            .Where(authorId => authorId != userId)
            .GroupBy(authorId => authorId)
            .OrderByDescending(g => g.Count())
            .Take(3)
            .Select(g => new UserStarGiven(g.Key, g.Count()))
            .ToList();

        // Get who stars this user the most (their "fans")
        var fans = receivedReactions
            .Join(starboardStats.Where(s => guildStarboards.Contains(s.StarboardId) && s.AuthorId == userId),
                r => new
                {
                    r.MessageId, r.Emote
                },
                s => new
                {
                    s.MessageId, s.Emote
                },
                (r, s) => r.UserId)
            .Where(fanId => fanId != userId)
            .GroupBy(fanId => fanId)
            .OrderByDescending(g => g.Count())
            .Take(3)
            .Select(g => new UserStarGiven(g.Key, g.Count()))
            .ToList();

        return new UserStarboardStatsDto
        {
            MessagesStarred = messagesStarred,
            StarsReceived = totalStarsReceived,
            StarsGiven = totalStarsGiven,
            TopStarredPosts = topStarredPosts,
            MostStarredUsers = starredUsers,
            TopFans = fans
        };
    }

    /// <summary>
    ///     Gets guild-wide starboard statistics.
    /// </summary>
    private async Task<StarboardStatsDto?> GetGuildStarboardStats(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var guildStarboards = starboardConfigs.Where(x => x.GuildId == guildId).Select(x => x.Id).ToList();
        if (guildStarboards.Count == 0)
            return new StarboardStatsDto();

        var stats = starboardStats.Where(x => guildStarboards.Contains(x.StarboardId) && x.IsActive);
        var reactions = starboardReactions.Where(x => guildStarboards.Contains(x.StarboardId));

        return new StarboardStatsDto
        {
            MostStarredUser = stats
                .GroupBy(x => x.AuthorId)
                .OrderByDescending(g => g.Sum(x => x.PeakReactionCount))
                .Select(g => new UserStarStats
                {
                    UserId = g.Key, TotalStars = g.Sum(x => x.PeakReactionCount), MessageCount = g.Count()
                })
                .FirstOrDefault(),
            MostActiveChannel = stats
                .GroupBy(x => x.ChannelId)
                .OrderByDescending(g => g.Sum(x => x.PeakReactionCount))
                .Select(g => new ChannelStarStats
                {
                    ChannelId = g.Key, TotalStars = g.Sum(x => x.PeakReactionCount), MessageCount = g.Count()
                })
                .FirstOrDefault(),
            MostActiveStarrer = reactions
                .GroupBy(x => x.UserId)
                .OrderByDescending(g => g.Count())
                .Select(g => new StarrerStats
                {
                    UserId = g.Key, StarsGiven = g.Count(), UniqueEmotesUsed = g.Select(x => x.Emote).Distinct().Count()
                })
                .FirstOrDefault(),
            TotalStarredMessages = stats.Count(),
            TotalStars = stats.Sum(x => x.PeakReactionCount)
        };
    }

    /// <summary>
    ///     Parses emotes from a pipe-separated string.
    /// </summary>
    private static List<string> ParseEmotes(string emoteString)
    {
        if (string.IsNullOrWhiteSpace(emoteString))
            return new List<string>();

        return emoteString.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }

    /// <summary>
    ///     Creates a ComponentsV2 message for starboard posts with individual reaction counters and media gallery support.
    /// </summary>
    private async Task<(ComponentBuilderV2 Components, List<FileAttachment> Files)> CreateStarboardComponentsV2(
        IUserMessage message, string content,
        List<string> starboardEmotes, string authorName, bool allowBots)
    {
        var builder = new ComponentBuilderV2();

        // Handle media attachments
        var attachments = message.Attachments.ToList();
        var imageUrls = new List<string>();
        var videoUrls = new List<string>();
        var otherFiles = new List<IAttachment>();

        foreach (var attachment in attachments)
        {
            var extension = Path.GetExtension(attachment.Filename).ToLowerInvariant();

            if (IsImageExtension(extension))
            {
                imageUrls.Add(attachment.ProxyUrl);
            }
            else if (IsVideoExtension(extension))
            {
                videoUrls.Add(attachment.ProxyUrl);
            }
            else
            {
                otherFiles.Add(attachment);
            }
        }

        // Create container components
        var containerComponents = new List<IMessageComponentBuilder>();

        // Get individual reaction counts for each starboard emote
        var reactionCountsText = new List<string>();

        foreach (var emote in starboardEmotes.Select(emoteString => emoteString.ToIEmote()))
        {
            try
            {
                var reactionUsers = await message.GetReactionUsersAsync(emote, int.MaxValue).FlattenAsync();
                var count = allowBots ? reactionUsers.Count() : reactionUsers.Count(u => !u.IsBot);
                if (count > 0)
                {
                    reactionCountsText.Add($"{emote} **{count}**");
                }
            }
            catch
            {
                // Ignore if emote can't be processed
            }
        }

        // Add username as a clickable link to user profile
        containerComponents.Add(
            new TextDisplayBuilder($"[**{authorName}**](https://discord.com/users/{message.Author.Id})"));

        // Add message content if available
        if (!string.IsNullOrWhiteSpace(content))
        {
            containerComponents.Add(new TextDisplayBuilder(content));
        }

        // Add media gallery for images and videos
        var allMediaUrls = imageUrls.Concat(videoUrls).ToList();
        if (allMediaUrls.Count > 0)
        {
            var mediaItems =
                allMediaUrls.Select(url => new MediaGalleryItemProperties(new UnfurledMediaItemProperties(url)));
            containerComponents.Add(new MediaGalleryBuilder(mediaItems));
        }

        // Download non-media files to re-upload with starboard post
        var filesToUpload = new List<FileAttachment>();
        if (otherFiles.Count > 0)
        {
            using var httpClient = new HttpClient();
            // Add user agent to mimic browser requests
            httpClient.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

            foreach (var file in otherFiles.Take(3)) // Limit to 3 files to avoid size issues
            {
                try
                {
                    // Use the original URL instead of ProxyUrl for better compatibility
                    var downloadUrl = file.Url ?? file.ProxyUrl;
                    var fileData = await httpClient.GetByteArrayAsync(downloadUrl);
                    filesToUpload.Add(new FileAttachment(new MemoryStream(fileData), file.Filename));

                    // Add file component referencing the file we'll upload
                    containerComponents.Add(new FileComponentBuilder
                    {
                        File = new UnfurledMediaItemProperties($"attachment://{file.Filename}")
                    });
                }
                catch (Exception ex)
                {
                    logger.LogWarning($"Failed to download file {file.Filename} for starboard: {ex.Message}");
                    // If download fails, just show the filename as text
                    containerComponents.Add(new TextDisplayBuilder($"📎 **{file.Filename}** ({file.Size} bytes)"));
                }
            }
        }

        // Stars count display outside container
        var starsDisplay = string.Join(" ", reactionCountsText);

        // Wrap with stars count outside container, username+thumbnail section inside container
        var componentsBuilder = builder
            .WithTextDisplay(!string.IsNullOrWhiteSpace(starsDisplay) ? starsDisplay : "✨")
            .WithContainer(containerComponents)
            .WithActionRow([
                new ButtonBuilder()
                    .WithLabel("Jump to Message")
                    .WithStyle(ButtonStyle.Link)
                    .WithUrl(message.GetJumpUrl())
            ]);

        return (componentsBuilder, filesToUpload);
    }

    /// <summary>
    ///     Checks if a file extension is for an image.
    /// </summary>
    private static bool IsImageExtension(string extension)
    {
        return extension switch
        {
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" or ".bmp" or ".tiff" or ".svg" => true,
            _ => false
        };
    }

    /// <summary>
    ///     Checks if a file extension is for a video.
    /// </summary>
    private static bool IsVideoExtension(string extension)
    {
        return extension switch
        {
            ".mp4" or ".webm" or ".mov" or ".avi" or ".mkv" or ".flv" or ".wmv" or ".m4v" => true,
            _ => false
        };
    }

    /// <summary>
    ///     Tracks starboard statistics for a message.
    /// </summary>
    private async Task TrackStarboardStats(IUserMessage message, int starboardId, string emote, int reactionCount,
        ulong channelId)
    {
        // Safety check to prevent foreign key constraint violations
        if (starboardId <= 0)
        {
            logger.LogWarning("[Starboard] Attempted to track stats for invalid starboard ID: {StarboardId}",
                starboardId);
            return;
        }

        await using var db = await dbFactory.CreateConnectionAsync();

        var existing = starboardStats.FirstOrDefault(x =>
            x.MessageId == message.Id && x.StarboardId == starboardId && x.Emote == emote);

        if (existing != null)
        {
            // Update existing stats
            existing.ReactionCount = reactionCount;
            existing.PeakReactionCount = Math.Max(existing.PeakReactionCount, reactionCount);
            existing.LastUpdatedAt = DateTime.UtcNow;
            existing.IsActive = reactionCount > 0;

            await db.UpdateAsync(existing);
        }
        else if (reactionCount > 0)
        {
            // Create new stats entry
            var newStats = new StarboardStats
            {
                StarboardId = starboardId,
                MessageId = message.Id,
                ChannelId = channelId,
                AuthorId = message.Author.Id,
                Emote = emote,
                ReactionCount = reactionCount,
                PeakReactionCount = reactionCount,
                FirstStarredAt = DateTime.UtcNow,
                LastUpdatedAt = DateTime.UtcNow,
                IsActive = true
            };

            await db.InsertAsync(newStats);
            starboardStats.Add(newStats);
        }
    }

    /// <summary>
    ///     Tracks an individual star reaction.
    /// </summary>
    private async Task TrackStarboardReaction(ulong messageId, int starboardId, ulong userId, string emote)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var existing = starboardReactions.FirstOrDefault(x =>
            x.MessageId == messageId && x.UserId == userId && x.Emote == emote);

        if (existing == null)
        {
            var newReaction = new StarboardReaction
            {
                StarboardId = starboardId,
                MessageId = messageId,
                UserId = userId,
                Emote = emote,
                DateAdded = DateTime.UtcNow
            };

            await db.InsertAsync(newReaction);
            starboardReactions.Add(newReaction);
        }
    }


    private async Task AddStarboardPost(ulong messageId, ulong postId, int starboardId, int reactionCount = 0)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        var post = starboardPosts.Find(x => x.MessageId == messageId && x.StarboardConfigId == starboardId);
        if (post == null)
        {
            var toAdd = new StarboardPost
            {
                MessageId = messageId, PostId = postId, StarboardConfigId = starboardId, ReactionCount = reactionCount
            };
            starboardPosts.Add(toAdd);
            await dbContext.InsertAsync(toAdd);
            collector.Feature("starboard", starboardConfigs.Find(x => x.Id == starboardId)?.GuildId);

            return;
        }

        if (post.PostId == postId)
        {
            // Update reaction count if different
            if (post.ReactionCount != reactionCount)
            {
                post.ReactionCount = reactionCount;
                await dbContext.UpdateAsync(post);
            }

            return;
        }

        starboardPosts.Remove(post);
        post.PostId = postId;
        post.ReactionCount = reactionCount;
        await dbContext.UpdateAsync(post);
        starboardPosts.Add(post);
    }

    private async Task RemoveStarboardPost(ulong messageId, int starboardId)
    {
        var toRemove = starboardPosts.Find(x => x.MessageId == messageId && x.StarboardConfigId == starboardId);
        if (toRemove == null)
            return;

        await using var dbContext = await dbFactory.CreateConnectionAsync();
        await dbContext.DeleteAsync(toRemove);
        starboardPosts.Remove(toRemove);
    }

    private async Task OnReactionAddedAsync(Cacheable<IUserMessage, ulong> message,
        Cacheable<IMessageChannel, ulong> channel,
        SocketReaction reaction)
    {
        if (!reaction.User.IsSpecified
            || reaction.User.Value.IsBot
            || !channel.HasValue
            || channel.Value is not ITextChannel textChannel)
            return;

        var guildStarboards = GetStarboards(textChannel.GuildId);
        if (!guildStarboards.Any())
            return;

        foreach (var starboard in guildStarboards)
        {
            await HandleReactionChange(message, channel, reaction, starboard, true);
        }
    }

    private async Task OnReactionRemoveAsync(Cacheable<IUserMessage, ulong> message,
        Cacheable<IMessageChannel, ulong> channel,
        SocketReaction reaction)
    {
        if (!reaction.User.IsSpecified
            || reaction.User.Value.IsBot
            || !channel.HasValue
            || channel.Value is not ITextChannel textChannel)
            return;

        var guildStarboards = GetStarboards(textChannel.GuildId);
        if (!guildStarboards.Any())
            return;

        foreach (var starboard in guildStarboards)
        {
            await HandleReactionChange(message, channel, reaction, starboard, false);
        }
    }

    private async Task HandleReactionChange(Cacheable<IUserMessage, ulong> message,
        Cacheable<IMessageChannel, ulong> channel,
        SocketReaction reaction,
        DataModel.Starboard starboard,
        bool isAdd)
    {
        var textChannel = channel.Value as ITextChannel;

        // Check if the reaction matches any of the starboard's emotes
        var starboardEmotes = ParseEmotes(starboard.Emote);
        var matchingEmote = starboardEmotes.FirstOrDefault(e =>
        {
            var emote = e.ToIEmote();
            return emote.Name != null && Equals(reaction.Emote, emote);
        });

        if (matchingEmote == null)
            return;

        var starboardChannel = await textChannel.Guild.GetTextChannelAsync(starboard.StarboardChannelId);
        if (starboardChannel == null)
            return;

        IUserMessage newMessage;
        if (!message.HasValue)
            newMessage = await message.GetOrDownloadAsync();
        else
            newMessage = message.Value;

        if (newMessage == null)
            return;

        var gUser = await textChannel.Guild.GetUserAsync(client.CurrentUser.Id);
        var botPerms = gUser.GetPermissions(starboardChannel);

        if (!botPerms.Has(ChannelPermission.SendMessages))
            return;

        if (starboard.UseBlacklist)
        {
            if (!starboard.CheckedChannels.IsNullOrWhiteSpace() &&
                starboard.CheckedChannels.Split(" ").Contains(newMessage.Channel.Id.ToString()))
                return;
        }
        else
        {
            if (!starboard.CheckedChannels.IsNullOrWhiteSpace() &&
                !starboard.CheckedChannels.Split(" ").Contains(newMessage.Channel.Id.ToString()))
                return;
        }

        string content;
        string imageurl;

        // Get all emotes for this starboard to show individual counters
        starboardEmotes = ParseEmotes(starboard.Emote);

        if (newMessage.Author.IsBot)
        {
            if (!starboard.AllowBots)
                return;

            content = newMessage.Embeds.Count > 0
                ? newMessage.Embeds.Select(x => x.Description).FirstOrDefault()
                : newMessage.Content;
            imageurl = newMessage.Attachments.Count > 0
                ? newMessage.Attachments.FirstOrDefault().ProxyUrl
                : newMessage.Embeds?.Select(x => x.Image).FirstOrDefault()?.ProxyUrl;
        }
        else
        {
            content = newMessage.Content;
            imageurl = newMessage.Attachments?.FirstOrDefault()?.ProxyUrl;
        }

        if (content is null && imageurl is null)
            return;

        var reactionEmote = matchingEmote.ToIEmote();
        var emoteCount = await newMessage.GetReactionUsersAsync(reactionEmote, int.MaxValue).FlattenAsync();
        var count = emoteCount.Where(x => !x.IsBot);
        var enumerable = count as IUser[] ?? count.ToArray();
        var maybePost = starboardPosts.Find(x => x.MessageId == newMessage.Id && x.StarboardConfigId == starboard.Id);

        // Track stats and individual reactions
        await TrackStarboardStats(newMessage, starboard.Id, matchingEmote, enumerable.Length, textChannel.Id);
        if (isAdd && reaction.User.IsSpecified)
        {
            await TrackStarboardReaction(newMessage.Id, starboard.Id, reaction.User.Value.Id, matchingEmote);
        }

        // Calculate total stars across all emotes for this starboard
        var totalStars = 0;
        foreach (var emoteString in starboardEmotes)
        {
            var emote = emoteString.ToIEmote();
            try
            {
                var reactions = await newMessage.GetReactionUsersAsync(emote, int.MaxValue).FlattenAsync();
                var validReactions = reactions.Count(u => !u.IsBot);
                totalStars += validReactions;
            }
            catch
            {
                // Ignore if emote can't be processed
            }
        }

        if (totalStars < starboard.Threshold)
        {
            if (maybePost != null && starboard.RemoveOnBelowThreshold)
            {
                await RemoveStarboardPost(newMessage.Id, starboard.Id);
                try
                {
                    var post = await starboardChannel.GetMessageAsync(maybePost.PostId);
                    if (post != null)
                        await post.DeleteAsync();
                }
                catch
                {
                    // ignored
                }
            }

            return;
        }

        if (maybePost != null)
        {
            if (starboard.RepostThreshold > 0)
            {
                var messages = await starboardChannel.GetMessagesAsync(starboard.RepostThreshold).FlattenAsync();
                var post = messages.FirstOrDefault(x => x.Id == maybePost.PostId);

                if (post != null)
                {
                    var post2 = post as IUserMessage;
                    var eb1 = new EmbedBuilder()
                        .WithOkColor()
                        .WithAuthor(newMessage.Author)
                        .WithDescription(content)
                        .WithTimestamp(newMessage.Timestamp);

                    if (imageurl is not null)
                        eb1.WithImageUrl(imageurl);

                    var (updateComponentsV2, files) = await CreateStarboardComponentsV2(newMessage, content,
                        starboardEmotes, newMessage.Author.ToString(), starboard.AllowBots);

                    // If we have files, we need to delete and recreate the message
                    if (files.Count > 0)
                    {
                        await post2.DeleteAsync();
                        var newPost = await starboardChannel.SendFilesAsync(files,
                            components: updateComponentsV2.Build(),
                            allowedMentions: AllowedMentions.None);
                        await AddStarboardPost(newMessage.Id, newPost.Id, starboard.Id, totalStars);
                    }
                    else
                    {
                        await post2.ModifyAsync(x =>
                        {
                            x.Content = null;
                            x.Components = updateComponentsV2.Build();
                            x.AllowedMentions = AllowedMentions.None;
                        });
                    }
                }
                else
                {
                    var tryGetOldPost = await starboardChannel.GetMessageAsync(maybePost.PostId);
                    if (tryGetOldPost != null)
                    {
                        try
                        {
                            await tryGetOldPost.DeleteAsync();
                        }
                        catch
                        {
                            // ignored
                        }
                    }

                    var (newComponentsV2, files) = await CreateStarboardComponentsV2(newMessage, content,
                        starboardEmotes, newMessage.Author.ToString(), starboard.AllowBots);

                    IUserMessage msg1;
                    if (files.Count > 0)
                    {
                        msg1 = await starboardChannel.SendFilesAsync(
                            files,
                            components: newComponentsV2.Build(),
                            allowedMentions: AllowedMentions.None);
                    }
                    else
                    {
                        msg1 = await starboardChannel.SendMessageAsync(components: newComponentsV2.Build(),
                            allowedMentions: AllowedMentions.None);
                    }

                    await AddStarboardPost(message.Id, msg1.Id, starboard.Id, totalStars);
                }
            }
            else
            {
                var tryGetOldPost = await starboardChannel.GetMessageAsync(maybePost.PostId);
                if (tryGetOldPost != null)
                {
                    var toModify = tryGetOldPost as IUserMessage;
                    var eb1 = new EmbedBuilder()
                        .WithOkColor()
                        .WithAuthor(newMessage.Author)
                        .WithDescription(content)
                        .WithTimestamp(newMessage.Timestamp);

                    if (imageurl is not null)
                        eb1.WithImageUrl(imageurl);

                    var (modifyComponentsV2, files) = await CreateStarboardComponentsV2(newMessage, content,
                        starboardEmotes, newMessage.Author.ToString(), starboard.AllowBots);

                    // If we have files, we need to delete and recreate the message
                    if (files.Count > 0)
                    {
                        await toModify.DeleteAsync();
                        var newPost = await starboardChannel.SendFilesAsync(files,
                            components: modifyComponentsV2.Build(),
                            allowedMentions: AllowedMentions.None);
                        await AddStarboardPost(newMessage.Id, newPost.Id, starboard.Id, totalStars);
                    }
                    else
                    {
                        await toModify.ModifyAsync(x =>
                        {
                            x.Content = null;
                            x.Components = modifyComponentsV2.Build();
                            x.AllowedMentions = AllowedMentions.None;
                        });
                    }
                }
                else
                {
                    var (elseComponentsV2, files) = await CreateStarboardComponentsV2(newMessage, content,
                        starboardEmotes, newMessage.Author.ToString(), starboard.AllowBots);

                    IUserMessage msg1;
                    if (files.Count > 0)
                    {
                        msg1 = await starboardChannel.SendFilesAsync(
                            files,
                            components: elseComponentsV2.Build(),
                            allowedMentions: AllowedMentions.None);
                    }
                    else
                    {
                        msg1 = await starboardChannel.SendMessageAsync(components: elseComponentsV2.Build(),
                            allowedMentions: AllowedMentions.None);
                    }

                    await AddStarboardPost(message.Id, msg1.Id, starboard.Id, totalStars);
                }
            }
        }
        else
        {
            var (finalComponentsV2, files) = await CreateStarboardComponentsV2(newMessage, content, starboardEmotes,
                newMessage.Author.ToString(), starboard.AllowBots);

            IUserMessage msg;
            if (files.Count > 0)
            {
                msg = await starboardChannel.SendFilesAsync(
                    files,
                    components: finalComponentsV2.Build(),
                    allowedMentions: AllowedMentions.None);
            }
            else
            {
                msg = await starboardChannel.SendMessageAsync(components: finalComponentsV2.Build(),
                    allowedMentions: AllowedMentions.None);
            }

            await AddStarboardPost(message.Id, msg.Id, starboard.Id, totalStars);
        }
    }

    private async Task OnMessageDeletedAsync(Cacheable<IMessage, ulong> arg1, Cacheable<IMessageChannel, ulong> arg2)
    {
        if (!arg1.HasValue || !arg2.HasValue)
            return;

        var msg = arg1.Value;
        var chan = arg2.Value;
        if (chan is not ITextChannel channel)
            return;

        var permissions = (await channel.Guild.GetUserAsync(client.CurrentUser.Id)).GetPermissions(channel);
        if (!permissions.ManageMessages)
            return;

        var posts = starboardPosts.Where(x => x.MessageId == msg.Id);
        foreach (var post in posts)
        {
            var config = starboardConfigs.FirstOrDefault(x => x.Id == post.StarboardConfigId);
            if (config?.RemoveOnDelete != true)
                continue;

            var starboardChannel = await channel.Guild.GetTextChannelAsync(config.StarboardChannelId);
            if (starboardChannel == null)
                continue;

            try
            {
                var starboardMessage = await starboardChannel.GetMessageAsync(post.PostId);
                if (starboardMessage != null)
                    await starboardMessage.DeleteAsync();
            }
            catch
            {
                // ignored
            }

            await RemoveStarboardPost(msg.Id, config.Id);
        }
    }

    private async Task OnAllReactionsClearedAsync(Cacheable<IUserMessage, ulong> arg1,
        Cacheable<IMessageChannel, ulong> arg2)
    {
        if (!arg2.HasValue || arg2.Value is not ITextChannel channel)
            return;

        IUserMessage msg;
        if (!arg1.HasValue)
            msg = await arg1.GetOrDownloadAsync();
        else
            msg = arg1.Value;

        if (msg == null)
            return;

        var posts = starboardPosts.Where(x => x.MessageId == msg.Id);
        foreach (var post in posts)
        {
            var config = starboardConfigs.FirstOrDefault(x => x.Id == post.StarboardConfigId);
            if (config?.RemoveOnReactionsClear != true)
                continue;

            var starboardChannel = await channel.Guild.GetTextChannelAsync(config.StarboardChannelId);
            if (starboardChannel == null)
                continue;

            try
            {
                var starboardMessage = await starboardChannel.GetMessageAsync(post.PostId);
                if (starboardMessage != null)
                    await starboardMessage.DeleteAsync();
            }
            catch
            {
                // ignored
            }

            await RemoveStarboardPost(msg.Id, config.Id);
        }
    }
}

/// <summary>
///     Represents a starboard highlight for dashboard display
/// </summary>
public class StarboardHighlight
{
    /// <summary>
    ///     The original message ID
    /// </summary>
    public ulong MessageId { get; set; }

    /// <summary>
    ///     The channel ID where the message was posted
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     The number of stars this message has
    /// </summary>
    public int StarCount { get; set; }

    /// <summary>
    ///     The text content of the message, or null when it has no text (attachment only, or the original and
    ///     the repost carry nothing readable).
    /// </summary>
    public string? Content { get; set; }

    /// <summary>
    ///     The name of the message author
    /// </summary>
    public string AuthorName { get; set; } = string.Empty;

    /// <summary>
    ///     The avatar URL of the message author
    /// </summary>
    public string? AuthorAvatarUrl { get; set; }

    /// <summary>
    ///     The URL of any image attached to the message
    /// </summary>
    public string? ImageUrl { get; set; }

    /// <summary>
    ///     The star emote used for this starboard
    /// </summary>
    public string StarEmote { get; set; } = "⭐";

    /// <summary>
    ///     When the message was created
    /// </summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>
///     DTO for starboard statistics
/// </summary>
public class StarboardStatsDto
{
    /// <summary>
    ///     The user who gets starred the most.
    /// </summary>
    public UserStarStats? MostStarredUser { get; set; }

    /// <summary>
    ///     The channel with the most starboard activity.
    /// </summary>
    public ChannelStarStats? MostActiveChannel { get; set; }

    /// <summary>
    ///     The user who gives the most stars.
    /// </summary>
    public StarrerStats? MostActiveStarrer { get; set; }

    /// <summary>
    ///     Total number of starred messages.
    /// </summary>
    public int TotalStarredMessages { get; set; }

    /// <summary>
    ///     Total number of stars given.
    /// </summary>
    public int TotalStars { get; set; }
}

/// <summary>
///     Statistics for a user's starred messages
/// </summary>
public class UserStarStats
{
    /// <summary>
    ///     The user's Discord ID.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    ///     Total number of stars received.
    /// </summary>
    public int TotalStars { get; set; }

    /// <summary>
    ///     Number of messages that were starred.
    /// </summary>
    public int MessageCount { get; set; }
}

/// <summary>
///     Statistics for a channel's starred messages
/// </summary>
public class ChannelStarStats
{
    /// <summary>
    ///     The channel's Discord ID.
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     Total number of stars in this channel.
    /// </summary>
    public int TotalStars { get; set; }

    /// <summary>
    ///     Number of starred messages from this channel.
    /// </summary>
    public int MessageCount { get; set; }
}

/// <summary>
///     Statistics for a user who gives stars
/// </summary>
public class StarrerStats
{
    /// <summary>
    ///     The user's Discord ID.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    ///     Number of stars given by this user.
    /// </summary>
    public int StarsGiven { get; set; }

    /// <summary>
    ///     Number of different emotes used by this user.
    /// </summary>
    public int UniqueEmotesUsed { get; set; }
}