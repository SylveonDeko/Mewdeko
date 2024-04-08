using System.Globalization;
using System.Text.RegularExpressions;
using Discord.Net;
using Mewdeko.Common.Collections;
using Mewdeko.Common.Configs;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Common.PubSub;
using Mewdeko.Modules.Administration.Services;
using Mewdeko.Modules.Moderation.Services;
using Mewdeko.Services.strings;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Mewdeko.Modules.Permissions.Services;

/// <summary>
/// Provides services for filtering messages in guilds based on predefined rules, including word filters, link filters, and invite filters.
/// </summary>
public class FilterService : IEarlyBehavior, INService
{
    private readonly CultureInfo? cultureInfo = new("en-US");
    private readonly DbService db;
    private readonly IPubSub pubSub;
    private readonly BotConfig config;

    private readonly TypedKey<HashSet<AutoBanEntry>> blPubKey = new("autobanword.reload");
    private readonly DiscordSocketClient client;

    /// <summary>
    /// HashSet of banned words for auto-banning.
    /// </summary>
    public HashSet<AutoBanEntry> Blacklist;

    private readonly AdministrationService ass;
    private readonly UserPunishService upun;
    private readonly GuildSettingsService gss;

    /// <summary>
    /// Initializes a new instance of the FilterService with necessary dependencies for filtering operations.
    /// </summary>
    /// <remarks>
    /// On initialization, this service loads filtering configurations from the database and subscribes to necessary events
    /// for real-time monitoring and filtering of messages across all guilds the bot is part of.
    /// </remarks>
    public FilterService(DiscordSocketClient client, DbService db, IPubSub pubSub,
        UserPunishService upun2, IBotStrings strng, AdministrationService ass,
        GuildSettingsService gss, EventHandler eventHandler, Mewdeko bot, BotConfig config)
    {
        this.db = db;
        this.client = client;
        this.pubSub = pubSub;
        upun = upun2;
        Strings = strng;
        Reload(false);
        this.pubSub.Sub(blPubKey, OnReload);
        this.ass = ass;
        this.gss = gss;
        this.config = config;
        var allgc = bot.AllGuildConfigs;

        InviteFilteringServers =
            new ConcurrentHashSet<ulong>(allgc.Where(gc => gc.FilterInvites != 0).Select(gc => gc.GuildId));
        InviteFilteringChannels =
            new ConcurrentHashSet<ulong>(allgc.SelectMany(gc =>
                gc.FilterInvitesChannelIds.Select(fci => fci.ChannelId)));

        LinkFilteringServers =
            new ConcurrentHashSet<ulong>(allgc.Where(gc => gc.FilterLinks != 0).Select(gc => gc.GuildId));
        LinkFilteringChannels =
            new ConcurrentHashSet<ulong>(allgc.SelectMany(gc =>
                gc.FilterLinksChannelIds.Select(fci => fci.ChannelId)));

        var dict = allgc.ToDictionary(gc => gc.GuildId,
            gc => new ConcurrentHashSet<string>(gc.FilteredWords.Select(fw => fw.Word)));

        ServerFilteredWords = new ConcurrentDictionary<ulong, ConcurrentHashSet<string>>(dict);

        var serverFiltering = allgc.Where(gc => gc.FilterWords != 0);
        WordFilteringServers = new ConcurrentHashSet<ulong>(serverFiltering.Select(gc => gc.GuildId));
        WordFilteringChannels =
            new ConcurrentHashSet<ulong>(allgc.SelectMany(gc =>
                gc.FilterWordsChannelIds.Select(fwci => fwci.ChannelId)));

        eventHandler.MessageUpdated += (_, newMsg, channel) =>
        {
            var guild = (channel as ITextChannel)?.Guild;

            if (guild == null || newMsg is not IUserMessage usrMsg)
                return Task.CompletedTask;

            return RunBehavior(null, guild, usrMsg);
        };
    }

    /// <summary>
    /// Stores localized strings for bot messages.
    /// </summary>
    public IBotStrings Strings { get; set; }

    /// <summary>
    /// A collection of channel IDs that have invite filtering enabled.
    /// </summary>
    public ConcurrentHashSet<ulong> InviteFilteringChannels { get; }

    /// <summary>
    /// A collection of channel IDs that have invite filtering enabled.
    /// </summary>
    public ConcurrentHashSet<ulong> InviteFilteringServers { get; }

    //serverid, filteredwords
    /// <summary>
    /// Manages the collection of word filters across different guilds, organized by guild ID.
    /// </summary>
    public ConcurrentDictionary<ulong, ConcurrentHashSet<string>> ServerFilteredWords { get; }

    /// <summary>
    /// A collection of channel IDs that have word filtering enabled.
    /// </summary>
    public ConcurrentHashSet<ulong> WordFilteringChannels { get; }

    /// <summary>
    /// A collection of server IDs that have word filtering enabled.
    /// </summary>
    public ConcurrentHashSet<ulong> WordFilteringServers { get; }

    /// <summary>
    /// A collection of channel IDs that have link filtering enabled.
    /// </summary>
    public ConcurrentHashSet<ulong> LinkFilteringChannels { get; }

    /// <summary>
    /// A collection of server IDs that have link filtering enabled.
    /// </summary>
    public ConcurrentHashSet<ulong> LinkFilteringServers { get; }

    /// <summary>
    /// Specifies the execution priority of this behavior in the pipeline.
    /// </summary>
    public int Priority => -50;

    /// <summary>
    /// Indicates the type of behavior this service represents.
    /// </summary>
    public ModuleBehaviorType BehaviorType => ModuleBehaviorType.Blocker;

    /// <summary>
    /// Orchestrates various filters, applying them to messages based on guild-specific configurations and global blacklist settings.
    /// </summary>
    /// <param name="socketClient">The Discord client for interacting with the API.</param>
    /// <param name="guild">The guild where the message was posted.</param>
    /// <param name="msg">The user message to be checked against the filters.</param>
    /// <returns>A task that resolves to true if the message was acted upon due to a filter match; otherwise, false.</returns>
    public async Task<bool> RunBehavior(DiscordSocketClient socketClient, IGuild guild, IUserMessage msg) =>
        msg.Author is IGuildUser gu && !gu.RoleIds.Contains(await ass.GetStaffRole(guild.Id)) &&
        !gu.GuildPermissions.Administrator && (await FilterInvites(guild, msg).ConfigureAwait(false)
                                               || await FilterWords(guild, msg).ConfigureAwait(false)
                                               || await FilterLinks(guild, msg).ConfigureAwait(false)
                                               || await FilterBannedWords(guild, msg).ConfigureAwait(false));

    private ValueTask OnReload(HashSet<AutoBanEntry> blacklist)
    {
        Blacklist = blacklist;
        return default;
    }

    /// <summary>
    /// Reloads the blacklist from the database and optionally publishes the updated list to subscribed components.
    /// </summary>
    /// <param name="publish">Whether to publish the updated blacklist to other components.</param>
    public void Reload(bool publish = true)
    {
        using var uow = db.GetDbContext();
        var toPublish = uow.AutoBanWords.AsNoTracking().ToHashSet();
        Blacklist = toPublish;
        if (publish) pubSub.Pub(blPubKey, toPublish);
    }

    /// <summary>
    /// Adds a word to the blacklist for a specified guild.
    /// </summary>
    /// <param name="id">The word to add to the blacklist.</param>
    /// <param name="id2">The ID of the guild for which the word is blacklisted.</param>
    public void WordBlacklist(string id, ulong id2)
    {
        using var uow = db.GetDbContext();
        var item = new AutoBanEntry
        {
            Word = id, GuildId = id2
        };
        uow.AutoBanWords.Add(item);
        uow.SaveChanges();

        Reload();
    }

    /// <summary>
    /// Removes a word from the blacklist for a specified guild.
    /// </summary>
    /// <param name="id">The word to remove from the blacklist.</param>
    /// <param name="id2">The ID of the guild from which the word is removed.</param>
    public void UnBlacklist(string id, ulong id2)
    {
        using var uow = db.GetDbContext();
        var toRemove = uow.AutoBanWords
            .FirstOrDefault(bi => bi.Word == id && bi.GuildId == id2);

        if (toRemove is not null)
            uow.AutoBanWords.Remove(toRemove);

        uow.SaveChanges();

        Reload();
    }

    /// <summary>
    /// Retrieves the set of filtered words for a specific channel within a guild.
    /// </summary>
    /// <param name="channelId">The ID of the channel.</param>
    /// <param name="guildId">The ID of the guild.</param>
    /// <returns>A set of filtered words for the channel, or null if no filters are set.</returns>
    public ConcurrentHashSet<string?>? FilteredWordsForChannel(ulong channelId, ulong guildId)
    {
        var words = new ConcurrentHashSet<string>();
        if (WordFilteringChannels.Contains(channelId))
            ServerFilteredWords.TryGetValue(guildId, out words);
        return words;
    }

    /// <summary>
    /// Retrieves the number of warnings a guild has set for invite violations.
    /// </summary>
    /// <param name="id">The ID of the guild.</param>
    /// <returns>The number of warnings set for invite violations in the guild.</returns>
    public async Task<int> GetInvWarn(ulong id) => (await gss.GetGuildConfig(id)).invwarn;

    /// <summary>
    /// Sets the number of warnings for invite violations in a guild.
    /// </summary>
    /// <param name="guild">The guild for which to set the warning count.</param>
    /// <param name="yesnt">The warning count to set.</param>
    public async Task InvWarn(IGuild guild, string yesnt)
    {
        var yesno = -1;
        await using (db.GetDbContext().ConfigureAwait(false))
        {
            yesno = yesnt switch
            {
                "y" => 1,
                "n" => 0,
                _ => yesno
            };
        }

        var uow = db.GetDbContext();
        await using (uow.ConfigureAwait(false))
        {
            var gc = await uow.ForGuildId(guild.Id, set => set);
            gc.invwarn = yesno;
            await uow.SaveChangesAsync().ConfigureAwait(false);
            await gss.UpdateGuildConfig(guild.Id, gc);
        }
    }

    /// <summary>
    /// Retrieves the number of warnings a guild has set for filtered word violations.
    /// </summary>
    /// <param name="id">The ID of the guild.</param>
    /// <returns>The number of warnings set for filtered word violations in the guild.</returns>
    public async Task<int> GetFw(ulong id) => (await gss.GetGuildConfig(id)).fwarn;

    /// <summary>
    /// Sets the number of warnings for filtered word violations in a guild.
    /// </summary>
    /// <param name="guild">The guild for which to set the warning count.</param>
    /// <param name="yesnt">The warning count to set.</param>
    public async Task SetFwarn(IGuild guild, string yesnt)
    {
        var yesno = -1;
        await using (db.GetDbContext().ConfigureAwait(false))
        {
            yesno = yesnt switch
            {
                "y" => 1,
                "n" => 0,
                _ => yesno
            };
        }

        var uow = db.GetDbContext();
        await using (uow.ConfigureAwait(false))
        {
            var gc = await uow.ForGuildId(guild.Id, set => set);
            gc.fwarn = yesno;
            await uow.SaveChangesAsync().ConfigureAwait(false);
            await gss.UpdateGuildConfig(guild.Id, gc);
        }
    }

    /// <summary>
    /// Clears all filtered words for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild for which to clear filtered words.</param>
    public async Task ClearFilteredWords(ulong guildId)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guildId,
            set => set.Include(x => x.FilteredWords)
                .Include(x => x.FilterWordsChannelIds));

        WordFilteringServers.TryRemove(guildId);
        ServerFilteredWords.TryRemove(guildId, out _);

        foreach (var c in gc.FilterWordsChannelIds) WordFilteringChannels.TryRemove(c.ChannelId);

        gc.FilterWords = 0;
        gc.FilteredWords.Clear();
        gc.FilterWordsChannelIds.Clear();

        await uow.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Retrieves the set of filtered words for an entire server.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <returns>A set of filtered words for the server, or null if no filters are set.</returns>
    public ConcurrentHashSet<string>? FilteredWordsForServer(ulong guildId)
    {
        var words = new ConcurrentHashSet<string>();
        if (WordFilteringServers.Contains(guildId))
            ServerFilteredWords.TryGetValue(guildId, out words);
        return words;
    }

    private string? GetText(string? key, params object?[] args) => Strings.GetText(key, cultureInfo, args);

    /// <summary>
    /// Filters messages containing banned words and takes appropriate action.
    /// </summary>
    /// <param name="guild">The guild in which the message was posted.</param>
    /// <param name="msg">The message to check for banned words.</param>
    /// <returns>True if the message contained banned words and was acted upon; otherwise, false.</returns>
    public async Task<bool> FilterBannedWords(IGuild? guild, IUserMessage? msg)
    {
        if (guild is null)
            return false;
        if (msg is null)
            return false;
        var bannedwords = Blacklist.Where(x => x.GuildId == guild.Id);
        foreach (var i in bannedwords)
        {
            Regex regex;
            try
            {
                regex = new Regex(i.Word, RegexOptions.Compiled, TimeSpan.FromMilliseconds(250));
            }
            catch
            {
                Log.Error($"Invalid regex, removing.: {i.Word}");
                await using var uow = db.GetDbContext();
                uow.AutoBanWords.Remove(i);
                await uow.SaveChangesAsync();
                Blacklist.Remove(i);
                return false;
            }

            var match = regex.Match(msg.Content.ToLower()).Value;
            if (!regex.IsMatch(msg.Content.ToLower())) continue;
            try
            {
                await msg.DeleteAsync().ConfigureAwait(false);
                var defaultMessage = GetText("bandm", Format.Bold(guild.Name),
                    $"Banned for saying autoban word {i}");
                var embed = await upun.GetBanUserDmEmbed(client, guild as SocketGuild,
                    await guild.GetUserAsync(client.CurrentUser.Id).ConfigureAwait(false), msg.Author as IGuildUser,
                    defaultMessage,
                    $"Banned for saying autoban word {match}", null).ConfigureAwait(false);
                await (await msg.Author.CreateDMChannelAsync().ConfigureAwait(false)).SendMessageAsync(embed.Item2,
                        embeds: embed.Item1, components: embed.Item3.Build())
                    .ConfigureAwait(false);
                await guild.AddBanAsync(msg.Author, options: new RequestOptions
                {
                    AuditLogReason = $"AutoBan word detected: {match}"
                }).ConfigureAwait(false);
                return true;
            }
            catch
            {
                try
                {
                    await guild.AddBanAsync(msg.Author, 1, options: new RequestOptions
                    {
                        AuditLogReason = $"AutoBan word detected: {match}"
                    }).ConfigureAwait(false);
                    return true;
                }
                catch
                {
                    Log.Error("Im unable to autoban in {ChannelName}", msg.Channel.Name);
                    return false;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Filters messages containing specified words and takes appropriate action.
    /// </summary>
    /// <param name="guild">The guild in which the message was posted.</param>
    /// <param name="usrMsg">The message to check for specified words.</param>
    /// <returns>True if the message contained specified words and was acted upon; otherwise, false.</returns>
    public async Task<bool> FilterWords(IGuild? guild, IUserMessage? usrMsg)
    {
        if (guild is null)
            return false;
        if (usrMsg is null)
            return false;

        var filteredChannelWords =
            FilteredWordsForChannel(usrMsg.Channel.Id, guild.Id) ?? new ConcurrentHashSet<string>();
        var filteredServerWords = FilteredWordsForServer(guild.Id) ?? new ConcurrentHashSet<string>();
        if (filteredChannelWords.Count != 0 || filteredServerWords.Count != 0)
        {
            foreach (var word in filteredChannelWords)
            {
                Regex regex;
                try
                {
                    regex = new Regex(word, RegexOptions.Compiled, TimeSpan.FromMilliseconds(250));
                }
                catch
                {
                    Log.Error($"Invalid regex, removing.: {word}");
                    await using var uow = db.GetDbContext();
                    var config = await uow.ForGuildId(guild.Id, set => set.Include(gc => gc.FilteredWords));

                    var removed = config.FilteredWords.FirstOrDefault(fw => fw.Word.Trim().ToLowerInvariant() == word);
                    if (removed is null)
                        return false;
                    uow.Remove(removed);
                    await uow.SaveChangesAsync().ConfigureAwait(false);
                    var toremove = ServerFilteredWords.GetOrAdd(guild.Id, new ConcurrentHashSet<string>());
                    toremove.TryRemove(word);
                    return false;
                }

                if (!regex.IsMatch(usrMsg.Content.ToLower())) continue;
                try
                {
                    await usrMsg.DeleteAsync().ConfigureAwait(false);
                    if (await GetFw(guild.Id) != 0)
                    {
                        await upun.Warn(guild, usrMsg.Author.Id, client.CurrentUser,
                            "Warned for Filtered Word").ConfigureAwait(false);
                        var user = await usrMsg.Author.CreateDMChannelAsync().ConfigureAwait(false);
                        await user.SendErrorAsync(
                                $"You have been warned for using the word {Format.Code(regex.Match(usrMsg.Content.ToLower()).Value)}",
                                config)
                            .ConfigureAwait(false);
                    }
                }
                catch (HttpException ex)
                {
                    Log.Warning(
                        "I do not have permission to filter words in channel with id " + usrMsg.Channel.Id, ex);
                }

                return true;
            }
        }

        foreach (var word in filteredServerWords)
        {
            var regex = new Regex(word, RegexOptions.Compiled, TimeSpan.FromMilliseconds(250));
            if (!regex.IsMatch(usrMsg.Content.ToLower())) continue;
            try
            {
                await usrMsg.DeleteAsync().ConfigureAwait(false);
                if (await GetFw(guild.Id) != 0)
                {
                    await upun.Warn(guild, usrMsg.Author.Id, client.CurrentUser,
                        "Warned for Filtered Word").ConfigureAwait(false);
                    var user = await usrMsg.Author.CreateDMChannelAsync().ConfigureAwait(false);
                    await user.SendErrorAsync(
                            $"You have been warned for using the word {Format.Code(regex.Match(usrMsg.Content.ToLower()).Value)}",
                            config)
                        .ConfigureAwait(false);
                }
            }
            catch (HttpException ex)
            {
                Log.Warning(
                    "I do not have permission to filter words in channel with id " + usrMsg.Channel.Id, ex);
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// Filters messages containing invites and takes appropriate action.
    /// </summary>
    /// <param name="guild">The guild in which the message was posted.</param>
    /// <param name="usrMsg">The message to check for invites.</param>
    /// <returns>True if the message contained invites and was acted upon; otherwise, false.</returns>
    public async Task<bool> FilterInvites(IGuild? guild, IUserMessage? usrMsg)
    {
        if (guild is null)
            return false;
        if (usrMsg is null)
            return false;

        if ((InviteFilteringChannels.Contains(usrMsg.Channel.Id)
             || InviteFilteringServers.Contains(guild.Id))
            && usrMsg.Content.IsDiscordInvite())
        {
            try
            {
                await usrMsg.DeleteAsync().ConfigureAwait(false);
                if (await GetInvWarn(guild.Id) == 0) return true;
                await upun.Warn(guild, usrMsg.Author.Id, client.CurrentUser, "Warned for Posting Invite")
                    .ConfigureAwait(false);
                var user = await usrMsg.Author.CreateDMChannelAsync().ConfigureAwait(false);
                await user.SendErrorAsync("You have been warned for sending an invite, this is not allowed!", config)
                    .ConfigureAwait(false);

                return true;
            }
            catch (HttpException ex)
            {
                Log.Warning("I do not have permission to filter invites in channel with id " + usrMsg.Channel.Id,
                    ex);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Filters messages containing links and takes appropriate action.
    /// </summary>
    /// <param name="guild">The guild in which the message was posted.</param>
    /// <param name="usrMsg">The message to check for links.</param>
    /// <returns>True if the message contained links and was acted upon; otherwise, false.</returns>
    public async Task<bool> FilterLinks(IGuild? guild, IUserMessage? usrMsg)
    {
        if (guild is null)
            return false;
        if (usrMsg is null)
            return false;

        if ((LinkFilteringChannels.Contains(usrMsg.Channel.Id)
             || LinkFilteringServers.Contains(guild.Id))
            && usrMsg.Content.TryGetUrlPath(out _))
        {
            try
            {
                await usrMsg.DeleteAsync().ConfigureAwait(false);
                return true;
            }
            catch
            {
                Log.Warning("I do not have permission to filter links in channel with id " + usrMsg.Channel.Id);
                return true;
            }
        }

        return false;
    }
}