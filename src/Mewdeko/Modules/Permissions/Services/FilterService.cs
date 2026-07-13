using System.Globalization;
using System.Text.RegularExpressions;
using DataModel;
using Discord.Net;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Common.Configs;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Common.PubSub;
using Mewdeko.Modules.Administration.Services;
using Mewdeko.Modules.Moderation.Services;
using Mewdeko.Services.Strings;

namespace Mewdeko.Modules.Permissions.Services;

/// <summary>
///     Provides services for filtering messages in guilds based on predefined rules, including word filters, link filters,
///     and invite filters.
/// </summary>
public class FilterService : IEarlyBehavior, INService
{
    private readonly AdministrationService ass;

    private readonly DiscordShardedClient client;
    private readonly BotConfig config;
    private readonly CultureInfo? cultureInfo = new("en-US");
    private readonly IDataConnectionFactory dbFactory;
    private readonly GuildSettingsService gss;
    private readonly ILogger<FilterService> logger;

    // Cache for compiled regex patterns to avoid repeated compilation
    private readonly ConcurrentDictionary<string, Regex> regexCache = new();
    private readonly UserPunishService userPunServ;

    /// <summary>
    ///     Initializes a new instance of the FilterService with necessary dependencies for filtering operations.
    /// </summary>
    /// <remarks>
    ///     On initialization, this service loads filtering configurations from the database and subscribes to necessary events
    ///     for real-time monitoring and filtering of messages across all guilds the bot is part of.
    /// </remarks>
    /// <param name="client">The Discord client instance.</param>
    /// <param name="dbFactory">The database connection factory.</param>
    /// <param name="pubSub">The pubSub parameter.</param>
    /// <param name="upun2">The upun2 service.</param>
    /// <param name="strng">The strng parameter.</param>
    /// <param name="ass">The ass service.</param>
    /// <param name="gss">The gss service.</param>
    /// <param name="eventHandler">The event handler service.</param>
    /// <param name="config">The bot configuration settings.</param>
    /// <param name="logger">The logger instance for structured logging.</param>
    public FilterService(DiscordShardedClient client, IDataConnectionFactory dbFactory, IPubSub pubSub,
        UserPunishService upun2, GeneratedBotStrings strng, AdministrationService ass,
        GuildSettingsService gss, EventHandler eventHandler, BotConfig config, ILogger<FilterService> logger)
    {
        this.dbFactory = dbFactory;
        this.client = client;
        userPunServ = upun2;
        Strings = strng;
        this.ass = ass;
        this.gss = gss;
        this.config = config;
        this.logger = logger;

        eventHandler.Subscribe("MessageUpdated", "FilterService",
            (Cacheable<IMessage, ulong> _, SocketMessage newMsg, ISocketMessageChannel channel) =>
            {
                var guild = (channel as ITextChannel)?.Guild;

                if (guild == null || newMsg is not IUserMessage usrMsg)
                    return Task.CompletedTask;

                return RunBehavior(null, guild, usrMsg);
            });
    }

    /// <summary>
    ///     Stores localized strings for bot messages.
    /// </summary>
    private GeneratedBotStrings Strings { get; }


    /// <summary>
    ///     Specifies the execution priority of this behavior in the pipeline.
    /// </summary>
    public int Priority
    {
        get
        {
            return -50;
        }
    }

    /// <summary>
    ///     Indicates the type of behavior this service represents.
    /// </summary>
    public ModuleBehaviorType BehaviorType
    {
        get
        {
            return ModuleBehaviorType.Blocker;
        }
    }

    /// <summary>
    ///     Orchestrates various filters, applying them to messages based on guild-specific configurations and global blacklist
    ///     settings.
    /// </summary>
    /// <param name="socketClient">The Discord client for interacting with the API.</param>
    /// <param name="guild">The guild where the message was posted.</param>
    /// <param name="msg">The user message to be checked against the filters.</param>
    /// <returns>A task that resolves to true if the message was acted upon due to a filter match; otherwise, false.</returns>
    public async Task<bool> RunBehavior(DiscordShardedClient socketClient, IGuild guild, IUserMessage msg)
    {
        return msg.Author is IGuildUser gu && !gu.RoleIds.Contains(await ass.GetStaffRole(guild.Id)) &&
               !gu.GuildPermissions.Administrator && (await FilterInvites(guild, msg).ConfigureAwait(false)
                                                      || await FilterWords(guild, msg).ConfigureAwait(false)
                                                      || await FilterLinks(guild, msg).ConfigureAwait(false)
                                                      || await FilterBannedWords(guild, msg).ConfigureAwait(false));
    }


    /// <summary>
    ///     Adds a word to the blacklist for a specified guild.
    /// </summary>
    /// <param name="id">The word to add to the blacklist.</param>
    /// <param name="id2">The ID of the guild for which the word is blacklisted.</param>
    public async Task WordBlacklist(string id, ulong id2)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var item = new AutoBanWord
        {
            Word = id, GuildId = id2
        };

        await db.InsertAsync(item);
    }

    /// <summary>
    ///     Removes a word from the blacklist for a specified guild.
    /// </summary>
    /// <param name="id">The word to remove from the blacklist.</param>
    /// <param name="id2">The ID of the guild from which the word is removed.</param>
    public async Task UnBlacklist(string id, ulong id2)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        await db.AutoBanWords
            .Where(bi => bi.Word == id && bi.GuildId == id2)
            .DeleteAsync();
    }

    /// <summary>
    ///     Retrieves the set of filtered words for a specific channel within a guild.
    /// </summary>
    /// <param name="channelId">The ID of the channel.</param>
    /// <param name="guildId">The ID of the guild.</param>
    /// <returns>A set of filtered words for the channel, or null if no filters are set.</returns>
    public async Task<bool> FilterChannel(ulong channelId, ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        return await db.FilterWordsChannelIds
            .AnyAsync(x => x.GuildId == guildId && x.ChannelId == channelId);
    }

    /// <summary>
    ///     Retrieves the number of warnings a guild has set for invite violations.
    /// </summary>
    /// <param name="id">The ID of the guild.</param>
    /// <returns>The number of warnings set for invite violations in the guild.</returns>
    public async Task<int> GetInvWarn(ulong id)
    {
        return (await gss.GetGuildConfig(id)).Invwarn;
    }

    /// <summary>
    ///     Sets the number of warnings for invite violations in a guild.
    /// </summary>
    /// <param name="guild">The guild for which to set the warning count.</param>
    /// <param name="yesnt">The warning count to set.</param>
    public async Task InvWarn(IGuild guild, string yesnt)
    {
        var yesno = -1;
        yesno = yesnt switch
        {
            "y" => 1,
            "n" => 0,
            _ => yesno
        };

        var gc = await gss.GetGuildConfig(guild.Id);
        gc.Invwarn = yesno;
        await gss.UpdateGuildConfig(guild.Id, gc);
    }

    /// <summary>
    ///     Retrieves the number of warnings a guild has set for filtered word violations.
    /// </summary>
    /// <param name="id">The ID of the guild.</param>
    /// <returns>The number of warnings set for filtered word violations in the guild.</returns>
    public async Task<int> GetFw(ulong id)
    {
        return (await gss.GetGuildConfig(id)).Fwarn;
    }

    /// <summary>
    ///     Sets the number of warnings for filtered word violations in a guild.
    /// </summary>
    /// <param name="guild">The guild for which to set the warning count.</param>
    /// <param name="yesnt">The warning count to set.</param>
    public async Task SetFwarn(IGuild guild, string yesnt)
    {
        var yesno = -1;
        yesno = yesnt switch
        {
            "y" => 1,
            "n" => 0,
            _ => yesno
        };

        var gc = await gss.GetGuildConfig(guild.Id);
        gc.Fwarn = yesno;
        await gss.UpdateGuildConfig(guild.Id, gc);
    }

    /// <summary>
    ///     Clears all filtered words for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild for which to clear filtered words.</param>
    public async Task ClearFilteredWords(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        // Delete all filtered words for this guild
        await db.FilteredWords
            .Where(x => x.GuildId == guildId)
            .DeleteAsync();

        // Delete all filter channel IDs for this guild
        await db.FilterWordsChannelIds
            .Where(x => x.GuildId == guildId)
            .DeleteAsync();

        // Update FilterWords flag in GuildConfig
        var guildConfig = await db.GuildConfigs
            .FirstOrDefaultAsync(gc => gc.GuildId == guildId);

        if (guildConfig != null)
        {
            guildConfig.FilterWords = false;
            await db.UpdateAsync(guildConfig);
        }
    }

    /// <summary>
    ///     Retrieves the set of filtered words for an entire server.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <returns>A set of filtered words for the server, or null if no filters are set.</returns>
    public async Task<HashSet<string>?> FilteredWordsForServer(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        // Get filtered words for this guild
        var words = await db.FilteredWords
            .Where(x => x.GuildId == guildId)
            .Select(x => x.Word)
            .ToListAsync();

        return words.Count > 0 ? words.ToHashSet() : [];
    }


    /// <summary>
    ///     Filters messages containing banned words and takes appropriate action.
    /// </summary>
    /// <param name="guild">The guild in which the message was posted.</param>
    /// <param name="msg">The message to check for banned words.</param>
    /// <returns>True if the message contained banned words and was acted upon; otherwise, false.</returns>
    private async Task<bool> FilterBannedWords(IGuild? guild, IUserMessage? msg)
    {
        if (guild is null || msg is null)
            return false;

        var bannedWords = await GetBannedWordsForServer(guild.Id);
        if (bannedWords.Count == 0)
            return false;

        var lowerContent = msg.Content.ToLower();
        foreach (var word in bannedWords)
        {
            var match = await IsWordBanned(word, lowerContent, guild.Id);
            if (match.banned)
            {
                return await HandleBannedWord(msg, guild, word, match.matchedText);
            }
        }

        return false;
    }

    /// <summary>
    ///     Filters messages containing specified words and takes appropriate action.
    /// </summary>
    /// <param name="guild">The guild in which the message was posted.</param>
    /// <param name="usrMsg">The message to check for specified words.</param>
    /// <returns>True if the message contained specified words and was acted upon; otherwise, false.</returns>
    public async Task<bool> FilterWords(IGuild? guild, IUserMessage? usrMsg)
    {
        if (guild is null || usrMsg?.Author is null)
            return false;

        var channelId = usrMsg.Channel.Id;
        var guildId = guild.Id;

        if (!await ShouldFilterChannel(channelId, guildId))
            return false;

        var filteredWords = await GetFilteredWordsForServer(guildId);
        if (filteredWords.Count == 0)
            return false;

        var lowerContent = usrMsg.Content.ToLower();
        foreach (var word in filteredWords)
        {
            if (await IsWordMatched(word, lowerContent, guildId))
            {
                return await HandleFilteredWord(usrMsg, guild, word);
            }
        }

        return false;
    }

    private async Task<bool> ShouldFilterChannel(ulong channelId, ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        return await db.FilterWordsChannelIds
            .AnyAsync(x => x.GuildId == guildId && x.ChannelId == channelId);
    }

    private async Task<HashSet<string>> GetFilteredWordsForServer(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var words = await db.FilteredWords
            .Where(x => x.GuildId == guildId)
            .Select(x => x.Word)
            .ToListAsync();

        return words.ToHashSet();
    }

    private async Task<HashSet<string>> GetBannedWordsForServer(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var words = await db.AutoBanWords
            .Where(x => x.GuildId == guildId)
            .Select(x => x.Word)
            .ToListAsync()
            .ConfigureAwait(false);

        return words.ToHashSet();
    }

    private async Task<(bool banned, string matchedText)> IsWordBanned(string word, string content, ulong guildId)
    {
        try
        {
            var regex = regexCache.GetOrAdd(word, static pattern =>
                new Regex(pattern, RegexOptions.Compiled, TimeSpan.FromMilliseconds(250)));
            var match = regex.Match(content);
            return (match.Success, match.Value);
        }
        catch (ArgumentException)
        {
            // Remove from cache if invalid
            regexCache.TryRemove(word, out _);
            await RemoveInvalidBannedRegex(word, guildId);
            return (false, string.Empty);
        }
    }

    private async Task RemoveInvalidBannedRegex(string word, ulong guildId)
    {
        logger.LogError("Invalid regex, removing.: {Word}", word);
        await using var db = await dbFactory.CreateConnectionAsync();

        await db.AutoBanWords
            .Where(bi => bi.Word == word && bi.GuildId == guildId)
            .DeleteAsync();
    }

    private async Task<bool> HandleBannedWord(IUserMessage msg, IGuild guild, string word, string matchedText)
    {
        try
        {
            await msg.DeleteAsync().ConfigureAwait(false);
            var defaultMessage = Strings.BanDm(guild.Id, Format.Bold(guild.Name),
                Strings.AutobanWordDetected(guild.Id, word));

            try
            {
                var embed = await userPunServ.GetBanUserDmEmbed(client, guild as SocketGuild,
                    await guild.GetUserAsync(client.CurrentUser.Id).ConfigureAwait(false),
                    msg.Author as IGuildUser,
                    defaultMessage,
                    $"Banned for saying autoban word {matchedText}",
                    null).ConfigureAwait(false);

                await (await msg.Author.CreateDMChannelAsync().ConfigureAwait(false))
                    .SendMessageAsync(embed.Item2, embeds: embed.Item1, components: embed.Item3.Build())
                    .ConfigureAwait(false);
            }
            catch
            {
                // DM failed, continue with ban anyway
            }

            await guild.AddBanAsync(msg.Author, options: new RequestOptions
            {
                AuditLogReason = Strings.AutobanReason(guild.Id, matchedText)
            }).ConfigureAwait(false);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, Strings.AutobanError(guild.Id, msg.Channel.Name));
            return false;
        }
    }

    private async Task<bool> IsWordMatched(string word, string content, ulong guildId)
    {
        try
        {
            // Create cache key that includes case sensitivity info
            var cacheKey = $"{word}|ignorecase";
            var regex = regexCache.GetOrAdd(cacheKey, static key =>
            {
                var pattern = key.Split('|')[0];
                return new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase,
                    TimeSpan.FromMilliseconds(250));
            });
            return regex.IsMatch(content);
        }
        catch (ArgumentException)
        {
            // Remove from cache if invalid
            regexCache.TryRemove($"{word}|ignorecase", out _);
            await RemoveInvalidRegex(word, guildId);
            return false;
        }
    }

    private async Task RemoveInvalidRegex(string word, ulong guildId)
    {
        logger.LogError("Invalid regex, removing: {Word}", word);
        await using var db = await dbFactory.CreateConnectionAsync();

        await db.FilteredWords
            .Where(fw => fw.GuildId == guildId &&
                         fw.Word.Trim().Equals(word, StringComparison.InvariantCultureIgnoreCase))
            .DeleteAsync();
    }

    private async Task<bool> HandleFilteredWord(IUserMessage usrMsg, IGuild guild, string word)
    {
        try
        {
            await usrMsg.DeleteAsync();
            if (await GetFw(guild.Id) == 0) return true;
            await userPunServ.Warn(guild, usrMsg.Author.Id, client.CurrentUser, "Warned for Filtered Word");
            var user = await usrMsg.Author.CreateDMChannelAsync();
            await user.SendErrorAsync(Strings.FilteredWordWarning(guild.Id, Format.Code(word)), config);
            return true;
        }
        catch (HttpException ex)
        {
            logger.LogWarning(ex, Strings.FilterError(guild.Id, usrMsg.Channel.Id));
            return false;
        }
    }

    /// <summary>
    ///     Filters messages containing invites and takes appropriate action.
    /// </summary>
    /// <param name="guild">The guild in which the message was posted.</param>
    /// <param name="usrMsg">The message to check for invites.</param>
    /// <returns>True if the message contained invites and was acted upon; otherwise, false.</returns>
    private async Task<bool> FilterInvites(IGuild? guild, IUserMessage? usrMsg)
    {
        if (guild is null || usrMsg?.Author is null)
            return false;

        await using var db = await dbFactory.CreateConnectionAsync();

        // Get guild configuration
        var guildConfig = await db.GuildConfigs
            .FirstOrDefaultAsync(gc => gc.GuildId == guild.Id);

        if (guildConfig is not { FilterInvites: true })
            return false;

        // Check if there are any channel IDs to filter or if the current channel is in the list
        var channelFilters = await db.FilterInvitesChannelIds
            .Where(x => x.GuildId == guild.Id)
            .ToListAsync();

        var shouldFilter = guildConfig.FilterInvites &&
                           (channelFilters.Count == 0 ||
                            channelFilters.Any(x => x.ChannelId == usrMsg.Channel.Id)) &&
                           usrMsg.Content.IsDiscordInvite();

        if (!shouldFilter)
            return false;

        try
        {
            await usrMsg.DeleteAsync();

            if (await GetInvWarn(guild.Id) == 0) return true;
            await userPunServ.Warn(guild, usrMsg.Author.Id, client.CurrentUser, Strings.InviteWarnReason(guild.Id));

            var userDmChannel = await usrMsg.Author.CreateDMChannelAsync();
            await userDmChannel.SendErrorAsync(Strings.InviteWarning(guild.Id), config);

            return true;
        }
        catch (HttpException ex)
        {
            logger.LogWarning(ex, Strings.InviteFilterError(guild.Id, usrMsg.Channel.Id));
            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, Strings.InviteFilterUnexpected(guild.Id, usrMsg.Channel.Id));
            return false;
        }
    }

    /// <summary>
    ///     Filters links from messages based on guild configuration.
    /// </summary>
    /// <param name="guild">The guild where the message was sent.</param>
    /// <param name="usrMsg">The user message to check for links.</param>
    /// <returns>True if a link was filtered, false otherwise.</returns>
    private async Task<bool> FilterLinks(IGuild? guild, IUserMessage? usrMsg)
    {
        if (guild is null || usrMsg is null)
            return false;

        await using var db = await dbFactory.CreateConnectionAsync();

        // Get guild configuration
        var guildConfig = await db.GuildConfigs
            .FirstOrDefaultAsync(gc => gc.GuildId == guild.Id);

        if (guildConfig == null)
            return false;

        // Check if channel is configured for link filtering
        var hasChannelFilter = await db.FilterLinksChannelIds
            .AnyAsync(x => x.GuildId == guild.Id && x.ChannelId == usrMsg.Channel.Id);

        // Check if links should be filtered based on configuration
        var shouldFilter = (hasChannelFilter || guildConfig.FilterLinks) &&
                           usrMsg.Content.TryGetUrlPath(out _);

        if (!shouldFilter)
            return false;

        try
        {
            await usrMsg.DeleteAsync();
            return true;
        }
        catch (HttpException ex)
        {
            logger.LogWarning(ex, Strings.LinkFilterError(guild.Id, usrMsg.Channel.Id));
            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, Strings.LinkFilterUnexpected(guild.Id, usrMsg.Channel.Id));
            return false;
        }
    }
}