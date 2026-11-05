using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using DataModel;
using Humanizer;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Common.TypeReaders.Models;
using Mewdeko.Modules.Starboard.Common;
using Mewdeko.Services.Analytics;
using Mewdeko.Services.Strings;
using PermValue = Discord.PermValue;

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
        eventHandler.Subscribe("MessageReceived", "StarboardService", OnMessageReceivedAsync);
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
        eventHandler.Unsubscribe("MessageReceived", "StarboardService", OnMessageReceivedAsync);
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
        ThrowIfEmoteInUse(guild.Id, emote);

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

    private void ThrowIfEmoteInUse(ulong guildId, string emote)
    {
        var emotes = ParseEmotes(emote);
        foreach (var existingStarboard in GetStarboards(guildId))
        {
            var collision = emotes.Intersect(ParseEmotes(existingStarboard.Emote)).FirstOrDefault();
            if (collision != null)
                throw new InvalidOperationException(
                    $"Emote {collision} is already used by another starboard in this guild.");
        }
    }

    /// <summary>
    ///     Creates a starboard channel only the bot can post in, then a starboard that posts to it.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="name">The name of the channel to create.</param>
    /// <param name="emote">The emote to use for the starboard.</param>
    /// <param name="threshold">The number of reactions required.</param>
    /// <returns>The new channel and the ID of the starboard, or null when Discord refused the channel.</returns>
    public async Task<(ITextChannel Channel, int StarboardId)?> CreateStarboardWithChannel(IGuild guild, string name,
        string emote, int threshold)
    {
        ThrowIfEmoteInUse(guild.Id, emote);

        ITextChannel channel;
        try
        {
            channel = await guild.CreateTextChannelAsync(name, props =>
            {
                props.PermissionOverwrites = new List<Overwrite>
                {
                    new(guild.EveryoneRole.Id, PermissionTarget.Role, new OverwritePermissions(
                        viewChannel: PermValue.Allow,
                        readMessageHistory: PermValue.Allow,
                        addReactions: PermValue.Allow,
                        sendMessages: PermValue.Deny,
                        sendMessagesInThreads: PermValue.Deny,
                        createPublicThreads: PermValue.Deny,
                        createPrivateThreads: PermValue.Deny)),
                    new(client.CurrentUser.Id, PermissionTarget.User, new OverwritePermissions(
                        viewChannel: PermValue.Allow,
                        sendMessages: PermValue.Allow,
                        embedLinks: PermValue.Allow,
                        attachFiles: PermValue.Allow,
                        addReactions: PermValue.Allow,
                        readMessageHistory: PermValue.Allow,
                        manageMessages: PermValue.Allow))
                };
            });
        }
        catch (Discord.Net.HttpException ex)
        {
            logger.LogWarning(ex, "Could not create a starboard channel in {GuildId}", guild.Id);
            return null;
        }

        var id = await CreateStarboard(guild, channel.Id, emote, threshold);
        return (channel, id);
    }

    /// <summary>
    ///     Converts a duration to whole minutes, rounding up. No duration is 0.
    /// </summary>
    /// <param name="time">The duration, or null.</param>
    /// <returns>The minutes.</returns>
    public static int ToMinutes(TimeSpan? time)
        => time is { } span ? Math.Max(0, (int)Math.Ceiling(span.TotalMinutes)) : 0;

    /// <summary>
    ///     Reads a duration such as 2h or 1d12h as whole minutes. Empty input is 0.
    /// </summary>
    /// <param name="input">The duration text.</param>
    /// <param name="minutes">The minutes.</param>
    /// <returns>False when the text is not a duration.</returns>
    public static bool TryParseMinutes(string? input, out int minutes)
    {
        minutes = 0;
        if (string.IsNullOrWhiteSpace(input))
            return true;

        try
        {
            minutes = ToMinutes(StoopidTime.FromInput(input.Replace(" ", "").ToLowerInvariant()).Time);
            return minutes > 0;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private async Task<bool> UpdateConfigAsync(IGuild guild, int starboardId, Action<DataModel.Starboard> change)
    {
        var config = starboardConfigs.FirstOrDefault(x => x.Id == starboardId && x.GuildId == guild.Id);
        if (config == null)
            return false;

        change(config);
        await using var db = await dbFactory.CreateConnectionAsync();
        await db.UpdateAsync(config);
        return true;
    }

    /// <summary>
    ///     Turns a starboard on or off. A disabled starboard keeps its settings and posts but takes no new stars.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="starboardId">The ID of the starboard configuration.</param>
    /// <param name="enabled">Whether the starboard is on.</param>
    /// <returns>True if the setting was updated, false if the starboard wasn't found.</returns>
    public Task<bool> SetEnabled(IGuild guild, int starboardId, bool enabled)
        => UpdateConfigAsync(guild, starboardId, x => x.Enabled = enabled);

    /// <summary>
    ///     Sets which of a starboard's emotes the bot reacts with on its own posts.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="starboardId">The ID of the starboard configuration.</param>
    /// <param name="mode">None, the first emote or all emotes.</param>
    /// <returns>True if the setting was updated, false if the starboard wasn't found.</returns>
    public Task<bool> SetAutoReact(IGuild guild, int starboardId, StarboardAutoReact mode)
        => UpdateConfigAsync(guild, starboardId, x => x.AutoReact = (int)mode);

    /// <summary>
    ///     Sets whether one member's reactions with several of a starboard's emotes each count.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="starboardId">The ID of the starboard configuration.</param>
    /// <param name="enabled">Whether every reaction counts instead of one per member.</param>
    /// <returns>True if the setting was updated, false if the starboard wasn't found.</returns>
    public Task<bool> SetCountMultipleReactions(IGuild guild, int starboardId, bool enabled)
        => UpdateConfigAsync(guild, starboardId, x => x.CountMultipleReactions = enabled);

    /// <summary>
    ///     Sets whether the bot removes stars that cannot count, such as a member's own star when self stars are off.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="starboardId">The ID of the starboard configuration.</param>
    /// <param name="enabled">Whether rejected stars are removed.</param>
    /// <returns>True if the setting was updated, false if the starboard wasn't found.</returns>
    public Task<bool> SetRemoveInvalidStars(IGuild guild, int starboardId, bool enabled)
        => UpdateConfigAsync(guild, starboardId, x => x.RemoveInvalidStars = enabled);

    /// <summary>
    ///     Sets how old a message must be, and how old it can be, to be starred.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="starboardId">The ID of the starboard configuration.</param>
    /// <param name="minMinutes">The minimum age in minutes, 0 for none, or null to keep the current value.</param>
    /// <param name="maxMinutes">The maximum age in minutes, 0 for none, or null to keep the current value.</param>
    /// <returns>True if the setting was updated, false if the starboard wasn't found.</returns>
    public Task<bool> SetMessageAge(IGuild guild, int starboardId, int? minMinutes, int? maxMinutes)
        => UpdateConfigAsync(guild, starboardId, x =>
        {
            if (minMinutes.HasValue)
                x.MinMessageAgeMinutes = Math.Max(0, minMinutes.Value);
            if (maxMinutes.HasValue)
                x.MaxMessageAgeMinutes = Math.Max(0, maxMinutes.Value);
        });

    /// <summary>
    ///     Sets the least time between reposts of the same starboard post.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="starboardId">The ID of the starboard configuration.</param>
    /// <param name="minutes">The cooldown in minutes, 0 for none.</param>
    /// <returns>True if the setting was updated, false if the starboard wasn't found.</returns>
    public Task<bool> SetRepostCooldown(IGuild guild, int starboardId, int minutes)
        => UpdateConfigAsync(guild, starboardId, x => x.RepostCooldownMinutes = Math.Max(0, minutes));

    /// <summary>
    ///     Toggles a role in a starboard's allow or deny list. A role is only ever in one of the two.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="starboardId">The ID of the starboard configuration.</param>
    /// <param name="roleId">The role ID to toggle.</param>
    /// <param name="deny">True for the deny list, false for the allow list.</param>
    /// <returns>A tuple containing whether the role was added and the starboard configuration.</returns>
    public async Task<(bool WasAdded, DataModel.Starboard? Config)> ToggleStarRole(IGuild guild, int starboardId,
        ulong roleId, bool deny)
    {
        var added = false;
        DataModel.Starboard? updated = null;
        await UpdateConfigAsync(guild, starboardId, x =>
        {
            var target = ParseIds(deny ? x.DeniedRoles : x.AllowedRoles);
            var other = ParseIds(deny ? x.AllowedRoles : x.DeniedRoles);
            added = !target.Remove(roleId);
            if (added)
            {
                target.Add(roleId);
                other.Remove(roleId);
            }

            x.DeniedRoles = string.Join(" ", deny ? target : other);
            x.AllowedRoles = string.Join(" ", deny ? other : target);
            updated = x;
        });

        return (added, updated);
    }

    /// <summary>
    ///     Gets the roles in a starboard's allow or deny list.
    /// </summary>
    /// <param name="starboard">The starboard configuration.</param>
    /// <param name="deny">True for the deny list, false for the allow list.</param>
    /// <returns>The role IDs.</returns>
    public static List<ulong> GetStarRoles(DataModel.Starboard starboard, bool deny)
        => ParseIds(deny ? starboard.DeniedRoles : starboard.AllowedRoles);

    /// <summary>
    ///     Sets or clears the post design of a starboard.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="starboardId">The ID of the starboard configuration.</param>
    /// <param name="design">The embed json of the design, or null for the default layout.</param>
    /// <returns>The outcome of the change.</returns>
    public async Task<StarboardDesignResult> SetPostDesign(IGuild guild, int starboardId, string? design)
    {
        if (string.IsNullOrWhiteSpace(design))
            design = null;

        if (design != null && !StarboardPostDesign.TryRender(design, SampleData(guild, "⭐"), out _, out _,
                out _))
            return StarboardDesignResult.Invalid;

        return await UpdateConfigAsync(guild, starboardId, x => x.PostDesign = design)
            ? StarboardDesignResult.Saved
            : StarboardDesignResult.NotFound;
    }

    private static StarboardPostData SampleData(IGuild guild, string emote)
        => new(guild.Id, guild.Name, 1, 1, "Sample", "https://cdn.discordapp.com/embed/avatars/0.png",
            "Sample message", 1, "general", "https://discord.com/channels/1/1/1", 3, emote, $"{emote} **3**",
            ["https://cdn.discordapp.com/embed/avatars/0.png"], "https://cdn.discordapp.com/embed/avatars/0.png",
            DateTime.UtcNow);

    /// <summary>
    ///     Finds a message to preview a post design with: the given message, or the latest message from a member.
    /// </summary>
    /// <param name="channel">The channel to look in.</param>
    /// <param name="messageId">The ID of the message to use, or null for the latest member message.</param>
    /// <returns>The message, or null when none was found.</returns>
    public static async Task<IUserMessage?> FindSampleMessageAsync(IMessageChannel channel, ulong? messageId)
    {
        if (messageId.HasValue)
            return await channel.GetMessageAsync(messageId.Value) as IUserMessage;

        var recent = await channel.GetMessagesAsync(20).FlattenAsync();
        return recent.OfType<IUserMessage>().FirstOrDefault(x => !x.Author.IsBot
                                                                 && (!string.IsNullOrWhiteSpace(x.Content)
                                                                     || x.Attachments.Count > 0));
    }

    /// <summary>
    ///     Sends a starboard's post for a message to a channel without posting to the board or counting a star.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="starboardId">The ID of the starboard configuration.</param>
    /// <param name="target">The channel to send the preview to.</param>
    /// <param name="sample">The message to build the post from.</param>
    /// <returns>True if the preview was sent, false if the starboard wasn't found.</returns>
    public async Task<bool> SendDesignTestAsync(IGuild guild, int starboardId, ITextChannel target,
        IUserMessage sample)
    {
        var config = starboardConfigs.FirstOrDefault(x => x.Id == starboardId && x.GuildId == guild.Id);
        if (config == null)
            return false;

        var emotes = ParseEmotes(config.Emote);
        var tally = new StarTally(Math.Max(1, config.Threshold),
            emotes.Take(1).ToDictionary(x => x, _ => Math.Max(1, config.Threshold)));
        var content = sample.Embeds.Count > 0 && string.IsNullOrWhiteSpace(sample.Content)
            ? sample.Embeds.Select(x => x.Description).FirstOrDefault()
            : sample.Content;

        var payload = await BuildPostAsync(sample, content, config, tally);
        await SendPostAsync(target, payload);
        return true;
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
    ///     Describes the settings of a starboard that its details line does not cover.
    /// </summary>
    /// <param name="guildId">The ID of the guild, for the language.</param>
    /// <param name="starboard">The starboard configuration.</param>
    /// <returns>The description.</returns>
    public string DescribeExtras(ulong guildId, DataModel.Starboard starboard)
    {
        string Minutes(int minutes) => minutes > 0
            ? TimeSpan.FromMinutes(minutes).Humanize(2)
            : strings.StarboardOff(guildId);

        string Roles(string ids) => ParseIds(ids) is { Count: > 0 } list
            ? string.Join(" ", list.Select(x => $"<@&{x}>"))
            : strings.StarboardNone(guildId);

        return strings.StarboardConfigExtras(guildId, starboard.Enabled, (StarboardAutoReact)starboard.AutoReact,
            starboard.CountMultipleReactions, starboard.AllowSelfStar, starboard.RemoveInvalidStars,
            Roles(starboard.AllowedRoles), Roles(starboard.DeniedRoles), Minutes(starboard.MinMessageAgeMinutes),
            Minutes(starboard.MaxMessageAgeMinutes), Minutes(starboard.RepostCooldownMinutes),
            !string.IsNullOrWhiteSpace(starboard.PostDesign));
    }

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
    ///     Sets whether a member's own star on their message counts.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="starboardId">The ID of the starboard configuration.</param>
    /// <param name="allowed">Whether self stars count.</param>
    /// <returns>True if the setting was updated, false if the starboard wasn't found.</returns>
    public async Task<bool> SetAllowSelfStar(IGuild guild, int starboardId, bool allowed)
    {
        var config = starboardConfigs.FirstOrDefault(x => x.Id == starboardId && x.GuildId == guild.Id);
        if (config == null)
            return false;

        await using var db = await dbFactory.CreateConnectionAsync();
        config.AllowSelfStar = allowed;
        await db.UpdateAsync(config);

        return true;
    }

    /// <summary>
    ///     Sets whether messages from NSFW channels can be starred onto a board in a non-NSFW channel.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="starboardId">The ID of the starboard configuration.</param>
    /// <param name="allowed">Whether NSFW messages can be starred.</param>
    /// <returns>True if the setting was updated, false if the starboard wasn't found.</returns>
    public async Task<bool> SetAllowNsfw(IGuild guild, int starboardId, bool allowed)
    {
        var config = starboardConfigs.FirstOrDefault(x => x.Id == starboardId && x.GuildId == guild.Id);
        if (config == null)
            return false;

        await using var db = await dbFactory.CreateConnectionAsync();
        config.AllowNsfw = allowed;
        await db.UpdateAsync(config);

        return true;
    }

    /// <summary>
    ///     Locks a starboard so it stops accepting stars, or unlocks it.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="starboardId">The ID of the starboard configuration.</param>
    /// <param name="until">When the lock ends, or null to unlock.</param>
    /// <returns>True if the lock was updated, false if the starboard wasn't found.</returns>
    public async Task<bool> SetLockedUntil(IGuild guild, int starboardId, DateTime? until)
    {
        var config = starboardConfigs.FirstOrDefault(x => x.Id == starboardId && x.GuildId == guild.Id);
        if (config == null)
            return false;

        await using var db = await dbFactory.CreateConnectionAsync();
        config.LockedUntil = until;
        await db.UpdateAsync(config);

        return true;
    }

    /// <summary>
    ///     Gets the link to a starboard post, found by the original message's ID or the post's own ID.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="messageId">The original message ID or the starboard post ID.</param>
    /// <returns>The post's jump link, or null when nothing was starred under that ID.</returns>
    public string? GetPostLink(ulong guildId, ulong messageId)
    {
        var configs = GetStarboards(guildId);
        var post = starboardPosts.Find(x => (x.MessageId == messageId || x.PostId == messageId)
                                            && configs.Any(c => c.Id == x.StarboardConfigId));

        return post == null ? null : BuildPostLink(guildId, configs, post);
    }

    /// <summary>
    ///     Gets the link to a random starboard post in a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="starboardId">Limits the pick to one starboard when set.</param>
    /// <returns>The post's jump link, or null when nothing has been starred.</returns>
    public string? GetRandomPostLink(ulong guildId, int? starboardId = null)
    {
        var configs = GetStarboards(guildId);
        var posts = starboardPosts
            .Where(x => starboardId.HasValue
                ? x.StarboardConfigId == starboardId.Value
                : configs.Any(c => c.Id == x.StarboardConfigId))
            .Where(x => configs.Any(c => c.Id == x.StarboardConfigId))
            .ToList();

        return posts.Count == 0 ? null : BuildPostLink(guildId, configs, posts[Random.Shared.Next(posts.Count)]);
    }

    private static string BuildPostLink(ulong guildId, List<DataModel.Starboard> configs, StarboardPost post)
    {
        var channelId = configs.First(x => x.Id == post.StarboardConfigId).StarboardChannelId;
        return $"https://discord.com/channels/{guildId}/{channelId}/{post.PostId}";
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
    ///     Toggles a channel whose messages are posted to the starboard without needing stars.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="starboardId">The ID of the starboard configuration.</param>
    /// <param name="channelId">The channel ID to toggle.</param>
    /// <returns>A tuple containing whether the channel was added and the starboard configuration.</returns>
    public async Task<(bool WasAdded, DataModel.Starboard? Config)> ToggleAutoStarChannel(IGuild guild,
        int starboardId, ulong channelId)
    {
        var config = starboardConfigs.FirstOrDefault(x => x.Id == starboardId && x.GuildId == guild.Id);
        if (config == null)
            return (false, null);

        var channels = ParseIds(config.AutoStarChannels);
        var added = !channels.Remove(channelId);
        if (added)
            channels.Add(channelId);

        config.AutoStarChannels = string.Join(" ", channels);
        await using var db = await dbFactory.CreateConnectionAsync();
        await db.UpdateAsync(config);
        return (added, config);
    }

    /// <summary>
    ///     Gets the channels whose messages a starboard posts automatically.
    /// </summary>
    /// <param name="starboard">The starboard configuration.</param>
    /// <returns>The channel IDs.</returns>
    public static List<ulong> GetAutoStarChannels(DataModel.Starboard starboard)
    {
        return ParseIds(starboard.AutoStarChannels);
    }

    private static List<ulong> ParseIds(string? value)
    {
        return (value ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => ulong.TryParse(x, out var id) ? id : 0)
            .Where(x => x != 0)
            .Distinct()
            .ToList();
    }

    private async Task OnMessageReceivedAsync(SocketMessage msg)
    {
        if (msg is not SocketUserMessage { Channel: SocketTextChannel channel } message)
            return;

        var matching = starboardConfigs
            .Where(x => x.GuildId == channel.Guild.Id && ParseIds(x.AutoStarChannels).Contains(channel.Id))
            .ToList();
        if (matching.Count == 0)
            return;

        foreach (var starboard in matching)
        {
            try
            {
                await PostAutoStarAsync(message, channel, starboard);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Autostar post failed in {GuildId} for starboard {StarboardId}",
                    channel.Guild.Id, starboard.Id);
            }
        }
    }

    private async Task PostAutoStarAsync(SocketUserMessage message, SocketTextChannel channel,
        DataModel.Starboard starboard)
    {
        if (!starboard.Enabled)
            return;
        if (message.Author.IsBot && !starboard.AllowBots)
            return;
        if (starboard.LockedUntil.HasValue && starboard.LockedUntil.Value > DateTime.UtcNow)
            return;
        if (string.IsNullOrWhiteSpace(message.Content) && message.Attachments.Count == 0)
            return;
        if (starboardPosts.Exists(x => x.MessageId == message.Id && x.StarboardConfigId == starboard.Id))
            return;

        var starboardChannel = channel.Guild.GetTextChannel(starboard.StarboardChannelId);
        if (starboardChannel == null)
            return;
        if (!starboard.AllowNsfw && channel.IsNsfw && !starboardChannel.IsNsfw)
            return;
        if (!channel.Guild.CurrentUser.GetPermissions(starboardChannel).Has(ChannelPermission.SendMessages))
            return;

        var tally = await TallyStarsAsync(message, starboard, channel.Guild, ParseEmotes(starboard.Emote));
        var payload = await BuildPostAsync(message, message.Content, starboard, tally);
        var post = await SendPostAsync(starboardChannel, payload);

        await AddStarboardPost(message.Id, post.Id, starboard.Id);
        await AutoReactAsync(post, starboard);
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
        IUserMessage message, string? content, string starsDisplay, string authorName)
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

        var (filesToUpload, fileComponents) = await DownloadExtraFilesAsync(otherFiles);
        containerComponents.AddRange(fileComponents);

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
    ///     Downloads attachments that are neither images nor videos so they can be uploaded with a starboard post,
    ///     with the Components V2 file components that show them. A failed download becomes a line of text.
    /// </summary>
    private async Task<(List<FileAttachment> Files, List<IMessageComponentBuilder> Components)>
        DownloadExtraFilesAsync(List<IAttachment> otherFiles)
    {
        var files = new List<FileAttachment>();
        var components = new List<IMessageComponentBuilder>();
        if (otherFiles.Count == 0)
            return (files, components);

        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

        foreach (var file in otherFiles.Take(3)) // Limit to 3 files to avoid size issues
        {
            try
            {
                var fileData = await httpClient.GetByteArrayAsync(file.Url ?? file.ProxyUrl);
                files.Add(new FileAttachment(new MemoryStream(fileData), file.Filename));
                components.Add(new FileComponentBuilder
                {
                    File = new UnfurledMediaItemProperties($"attachment://{file.Filename}")
                });
            }
            catch (Exception ex)
            {
                logger.LogWarning($"Failed to download file {file.Filename} for starboard: {ex.Message}");
                components.Add(new TextDisplayBuilder($"📎 **{file.Filename}** ({file.Size} bytes)"));
            }
        }

        return (files, components);
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
                MessageId = messageId, PostId = postId, StarboardConfigId = starboardId, ReactionCount = reactionCount,
                LastPostedAt = DateTime.UtcNow
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
        post.LastPostedAt = DateTime.UtcNow;
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

    /// <summary>
    ///     Counts the stars a message has for one emote, using the reaction metadata
    ///     Discord already sends with the message. The reactor list is only fetched
    ///     when bots must be excluded and the raw count could matter: at or above
    ///     <paramref name="listWhenAtLeast" /> when given, otherwise whenever it is
    ///     non-zero. Below the threshold the bot's own reaction is subtracted from the
    ///     metadata count, since it is the only bot reaction knowable without a fetch.
    /// </summary>
    /// <param name="message">The message being counted.</param>
    /// <param name="emote">The starboard emote to count.</param>
    /// <param name="allowBots">Whether reactions from bots count.</param>
    /// <param name="listWhenAtLeast">Raw count at which the exact non-bot count is worth a fetch.</param>
    /// <returns>The number of counting reactions.</returns>
    private async Task<int> CountStarsAsync(IUserMessage message, IEmote emote, bool allowBots,
        int? listWhenAtLeast = null)
    {
        if (!message.Reactions.TryGetValue(emote, out var meta) || meta.ReactionCount <= 0)
            return 0;

        var raw = meta.ReactionCount;
        if (allowBots)
            return raw;

        if (listWhenAtLeast is { } floor && raw < floor)
            return meta.IsMe ? raw - 1 : raw;

        var users = await GetReactorsAsync(message, emote, raw);
        return users.Count(u => !u.IsBot);
    }

    private static readonly TimeSpan ReactorCacheLifetime = TimeSpan.FromSeconds(15);

    private readonly ConcurrentDictionary<(ulong MessageId, string Emote), (DateTime FetchedAt, IUser[] Users)>
        reactorCache = new();

    /// <summary>
    ///     Gets who reacted with an emote. Lists are kept for a few seconds and patched by the reaction events, so a
    ///     burst of reactions on one message fetches once. A list that no longer matches the reaction count Discord
    ///     reports is fetched again.
    /// </summary>
    private async Task<IUser[]> GetReactorsAsync(IUserMessage message, IEmote emote, int raw)
    {
        var key = (message.Id, emote.ToString() ?? "");
        if (reactorCache.TryGetValue(key, out var hit) && DateTime.UtcNow - hit.FetchedAt < ReactorCacheLifetime
                                                       && hit.Users.Length == raw)
            return hit.Users;

        var users = (await message.GetReactionUsersAsync(emote, raw).FlattenAsync()).ToArray();
        if (reactorCache.Count > 500)
        {
            foreach (var stale in reactorCache.Where(x => DateTime.UtcNow - x.Value.FetchedAt >= ReactorCacheLifetime))
                reactorCache.TryRemove(stale.Key, out _);
        }

        reactorCache[key] = (DateTime.UtcNow, users);
        return users;
    }

    /// <summary>
    ///     Applies a reaction event to a cached reactor list so it stays accurate without another fetch.
    /// </summary>
    private void PatchReactorCache(ulong messageId, IEmote emote, IUser user, bool added)
    {
        var key = (messageId, emote.ToString() ?? "");
        if (!reactorCache.TryGetValue(key, out var entry))
            return;

        var users = entry.Users.Where(x => x.Id != user.Id).ToList();
        if (added)
            users.Add(user);

        reactorCache[key] = (entry.FetchedAt, users.ToArray());
    }

    private readonly record struct StarTally(int Total, Dictionary<string, int> PerEmote);

    private sealed record PostPayload(ComponentBuilderV2? Components, string? Text, Discord.Embed[]? Embeds,
        ComponentBuilder? Buttons, List<FileAttachment> Files);

    /// <summary>
    ///     Whether a member's star can count on a starboard: bots need the bot setting, authors need self stars, and
    ///     the role lists apply to everyone else.
    /// </summary>
    private async Task<bool> CanStarAsync(DataModel.Starboard starboard, IGuild guild, ulong authorId, IUser user)
    {
        if (user.IsBot)
            return starboard.AllowBots;
        if (!starboard.AllowSelfStar && user.Id == authorId)
            return false;

        var allowed = ParseIds(starboard.AllowedRoles);
        var denied = ParseIds(starboard.DeniedRoles);
        if (allowed.Count == 0 && denied.Count == 0)
            return true;

        var member = user as IGuildUser ?? await guild.GetUserAsync(user.Id);
        if (member == null)
            return allowed.Count == 0;
        if (denied.Exists(member.RoleIds.Contains))
            return false;

        return allowed.Count == 0 || allowed.Exists(member.RoleIds.Contains);
    }

    /// <summary>
    ///     Whether a message is too new or too old to be starred on a starboard.
    /// </summary>
    private static bool IsMessageAgeRejected(DataModel.Starboard starboard, IMessage message)
    {
        var age = DateTimeOffset.UtcNow - message.Timestamp;
        return (starboard.MinMessageAgeMinutes > 0 && age < TimeSpan.FromMinutes(starboard.MinMessageAgeMinutes))
               || (starboard.MaxMessageAgeMinutes > 0 && age > TimeSpan.FromMinutes(starboard.MaxMessageAgeMinutes));
    }

    /// <summary>
    ///     Counts a message's stars on a starboard. The cheap reaction totals are used unless a rule needs to know who
    ///     reacted: self stars off, role lists, or one star per member across several emotes.
    /// </summary>
    private async Task<StarTally> TallyStarsAsync(IUserMessage message, DataModel.Starboard starboard, IGuild guild,
        List<string> emotes)
    {
        var perEmote = new Dictionary<string, int>();
        var needsReactors = !starboard.AllowSelfStar
                            || !string.IsNullOrWhiteSpace(starboard.AllowedRoles)
                            || !string.IsNullOrWhiteSpace(starboard.DeniedRoles)
                            || (emotes.Count > 1 && !starboard.CountMultipleReactions);

        if (!needsReactors)
        {
            foreach (var emote in emotes)
            {
                try
                {
                    perEmote[emote] = await CountStarsAsync(message, emote.ToIEmote(), starboard.AllowBots,
                        starboard.Threshold);
                }
                catch
                {
                    perEmote[emote] = 0;
                }
            }

            return new StarTally(perEmote.Values.Sum(), perEmote);
        }

        var total = 0;
        var seen = new HashSet<ulong>();
        foreach (var emote in emotes)
        {
            perEmote[emote] = 0;
            try
            {
                var iemote = emote.ToIEmote();
                if (!message.Reactions.TryGetValue(iemote, out var meta) || meta.ReactionCount <= 0)
                    continue;

                var reactors = await GetReactorsAsync(message, iemote, meta.ReactionCount);
                foreach (var reactor in reactors)
                {
                    if (!await CanStarAsync(starboard, guild, message.Author.Id, reactor))
                        continue;

                    perEmote[emote]++;
                    if (starboard.CountMultipleReactions || seen.Add(reactor.Id))
                        total++;
                }
            }
            catch
            {
                // Ignore if emote can't be processed
            }
        }

        return new StarTally(total, perEmote);
    }

    private static string FormatStars(List<string> emotes, Dictionary<string, int> perEmote)
    {
        return string.Join(" ", emotes
            .Where(x => perEmote.TryGetValue(x, out var count) && count > 0)
            .Select(x => $"{x.ToIEmote()} **{perEmote[x]}**"));
    }

    /// <summary>
    ///     Builds the message for a starboard post: the board's design when it has a valid one, otherwise the default
    ///     layout.
    /// </summary>
    private async Task<PostPayload> BuildPostAsync(IUserMessage message, string? content,
        DataModel.Starboard starboard, StarTally tally)
    {
        var emotes = ParseEmotes(starboard.Emote);
        var starsText = FormatStars(emotes, tally.PerEmote);

        if (!string.IsNullOrWhiteSpace(starboard.PostDesign) && message.Channel is IGuildChannel guildChannel)
        {
            var author = message.Author as IGuildUser;
            var data = new StarboardPostData(guildChannel.GuildId, guildChannel.Guild.Name, message.Id,
                message.Author.Id,
                author?.DisplayName ?? message.Author.GlobalName ?? message.Author.Username,
                author?.GetGuildAvatarUrl() ?? message.Author.GetDisplayAvatarUrl(),
                content ?? "", guildChannel.Id, guildChannel.Name, message.GetJumpUrl(), tally.Total,
                emotes.FirstOrDefault() ?? "⭐", starsText, message.Attachments.Select(x => x.Url).ToList(),
                FindImageUrl(message), message.Timestamp.UtcDateTime);

            if (StarboardPostDesign.TryRender(starboard.PostDesign, data, out var text, out var embeds,
                    out var buttons))
            {
                var others = message.Attachments.Where(x =>
                {
                    var extension = Path.GetExtension(x.Filename).ToLowerInvariant();
                    return !IsImageExtension(extension) && !IsVideoExtension(extension);
                }).ToList();
                var (extraFiles, _) = await DownloadExtraFilesAsync(others);
                return new PostPayload(null, text, embeds, buttons, extraFiles);
            }

            logger.LogWarning("Starboard {StarboardId} has a post design that no longer renders", starboard.Id);
        }

        var (components, files) = await CreateStarboardComponentsV2(message, content, starsText,
            message.Author.ToString());
        return new PostPayload(components, null, null, null, files);
    }

    private static async Task<IUserMessage> SendPostAsync(ITextChannel channel, PostPayload payload)
    {
        if (payload.Components == null && payload.Files.Count > 0)
            return await channel.SendFilesAsync(payload.Files, payload.Text, embeds: payload.Embeds,
                components: payload.Buttons?.Build(), allowedMentions: AllowedMentions.None);

        if (payload.Components == null)
            return await channel.SendMessageAsync(payload.Text, embeds: payload.Embeds,
                components: payload.Buttons?.Build(), allowedMentions: AllowedMentions.None);

        return payload.Files.Count > 0
            ? await channel.SendFilesAsync(payload.Files, components: payload.Components.Build(),
                allowedMentions: AllowedMentions.None)
            : await channel.SendMessageAsync(components: payload.Components.Build(),
                allowedMentions: AllowedMentions.None);
    }

    private static async Task AutoReactAsync(IUserMessage post, DataModel.Starboard starboard)
    {
        if (starboard.AutoReact == (int)StarboardAutoReact.None)
            return;

        var emotes = ParseEmotes(starboard.Emote);
        if (starboard.AutoReact == (int)StarboardAutoReact.First)
            emotes = emotes.Take(1).ToList();

        try
        {
            foreach (var emote in emotes)
                await post.AddReactionAsync(emote.ToIEmote());
        }
        catch (Discord.Net.HttpException)
        {
            // The bot cannot react here or the emote is gone
        }
    }

    private static bool RepostCooldownElapsed(DataModel.Starboard starboard, StarboardPost post)
    {
        if (starboard.RepostCooldownMinutes <= 0)
            return true;

        var last = post.LastPostedAt ?? post.DateAdded ?? DateTime.MinValue;
        return last.AddMinutes(starboard.RepostCooldownMinutes) <= DateTime.UtcNow;
    }

    private static async Task<IMessage?> TryGetPostMessageAsync(ITextChannel channel, ulong postId)
    {
        try
        {
            return await channel.GetMessageAsync(postId);
        }
        catch (Discord.Net.HttpException)
        {
            return null;
        }
    }

    private static async Task TryDeleteAsync(IMessage message)
    {
        try
        {
            await message.DeleteAsync();
        }
        catch (Discord.Net.HttpException)
        {
            // Already gone or not deletable
        }
    }

    /// <summary>
    ///     Posts a starred message to the starboard, or updates its existing post. A post that has fallen out of the
    ///     repost threshold is deleted and sent again unless the repost cooldown is still running.
    /// </summary>
    private async Task SendOrUpdatePostAsync(IUserMessage message, string? content, DataModel.Starboard starboard,
        ITextChannel starboardChannel, StarboardPost? existing, StarTally tally)
    {
        var payload = await BuildPostAsync(message, content, starboard, tally);

        IMessage? old = null;
        if (existing != null)
        {
            old = await TryGetPostMessageAsync(starboardChannel, existing.PostId);
            if (old != null && starboard.RepostThreshold > 0 && RepostCooldownElapsed(starboard, existing))
            {
                var recent = await starboardChannel.GetMessagesAsync(starboard.RepostThreshold).FlattenAsync();
                if (recent.All(x => x.Id != old.Id))
                {
                    await TryDeleteAsync(old);
                    old = null;
                }
            }
        }

        var oldIsComponentsV2 = old?.Flags?.HasFlag(MessageFlags.ComponentsV2) == true;
        if (old is IUserMessage toModify && payload.Files.Count == 0 &&
            oldIsComponentsV2 == (payload.Components != null))
        {
            try
            {
                await toModify.ModifyAsync(x =>
                {
                    if (payload.Components != null)
                    {
                        x.Content = null;
                        x.Components = payload.Components.Build();
                    }
                    else
                    {
                        x.Content = payload.Text;
                        x.Embeds = payload.Embeds ?? [];
                        x.Components = (payload.Buttons ?? new ComponentBuilder()).Build();
                    }

                    x.AllowedMentions = AllowedMentions.None;
                });
                await AddStarboardPost(message.Id, toModify.Id, starboard.Id, tally.Total);
                return;
            }
            catch (Discord.Net.HttpException)
            {
                // A post cannot change between the default layout and a design, so it is sent again
            }
        }

        if (old != null)
            await TryDeleteAsync(old);

        var sent = await SendPostAsync(starboardChannel, payload);
        await AddStarboardPost(message.Id, sent.Id, starboard.Id, tally.Total);
        await AutoReactAsync(sent, starboard);
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

        PatchReactorCache(message.Id, reaction.Emote, reaction.User.Value, true);
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

        PatchReactorCache(message.Id, reaction.Emote, reaction.User.Value, false);
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

        if (!starboard.Enabled)
            return;

        // Check if the reaction matches any of the starboard's emotes
        var starboardEmotes = ParseEmotes(starboard.Emote);
        var matchingEmote = starboardEmotes.FirstOrDefault(e =>
        {
            var emote = e.ToIEmote();
            return emote.Name != null && Equals(reaction.Emote, emote);
        });

        if (matchingEmote == null)
            return;

        if (starboard.LockedUntil.HasValue && starboard.LockedUntil.Value > DateTime.UtcNow)
            return;

        var starboardChannel = await textChannel.Guild.GetTextChannelAsync(starboard.StarboardChannelId);
        if (starboardChannel == null)
            return;

        if (!starboard.AllowNsfw && textChannel.IsNsfw && !starboardChannel.IsNsfw)
            return;

        IUserMessage newMessage;
        if (!message.HasValue)
            newMessage = await message.GetOrDownloadAsync();
        else
            newMessage = message.Value;

        if (newMessage == null || starboardPosts.Exists(x => x.PostId == newMessage.Id))
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

        if (isAdd && starboard.RemoveInvalidStars && reaction.User.IsSpecified
            && gUser.GetPermissions(textChannel).ManageMessages
            && ((newMessage.Author.IsBot && !starboard.AllowBots)
                || IsMessageAgeRejected(starboard, newMessage)
                || !await CanStarAsync(starboard, textChannel.Guild, newMessage.Author.Id, reaction.User.Value)))
        {
            try
            {
                await newMessage.RemoveReactionAsync(reaction.Emote, reaction.UserId);
            }
            catch (Discord.Net.HttpException)
            {
                // The reaction is already gone or cannot be removed
            }

            return;
        }

        if (IsMessageAgeRejected(starboard, newMessage))
            return;

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

        var tally = await TallyStarsAsync(newMessage, starboard, textChannel.Guild, starboardEmotes);
        var totalStars = tally.Total;
        var maybePost = starboardPosts.Find(x => x.MessageId == newMessage.Id && x.StarboardConfigId == starboard.Id);

        // Track stats and individual reactions
        await TrackStarboardStats(newMessage, starboard.Id, matchingEmote, tally.PerEmote.GetValueOrDefault(matchingEmote),
            textChannel.Id);
        if (isAdd && reaction.User.IsSpecified &&
            await CanStarAsync(starboard, textChannel.Guild, newMessage.Author.Id, reaction.User.Value))
        {
            await TrackStarboardReaction(newMessage.Id, starboard.Id, reaction.User.Value.Id, matchingEmote);
        }

        if (totalStars < starboard.Threshold)
        {
            if (maybePost != null && starboard.RemoveOnBelowThreshold &&
                !ParseIds(starboard.AutoStarChannels).Contains(textChannel.Id))
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

        await SendOrUpdatePostAsync(newMessage, content, starboard, starboardChannel, maybePost, tally);
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

        foreach (var key in reactorCache.Keys.Where(x => x.MessageId == msg.Id))
            reactorCache.TryRemove(key, out _);

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