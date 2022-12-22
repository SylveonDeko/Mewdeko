using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord.Net;
using Mewdeko.Common.Collections;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Common.PubSub;
using Mewdeko.Modules.Administration.Services;
using Mewdeko.Modules.Moderation.Services;
using Mewdeko.Services.strings;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Mewdeko.Modules.Permissions.Services;

public class FilterService : IEarlyBehavior, INService
{
    private readonly CultureInfo? cultureInfo = new("en-US");
    private readonly DbService db;
    private readonly IPubSub pubSub;

    private readonly TypedKey<AutoBanEntry[]> blPubKey = new("autobanword.reload");
    private readonly DiscordSocketClient client;
    public IReadOnlyList<AutoBanEntry> Blacklist;
    public readonly AdministrationService Ass;
    public readonly UserPunishService Upun;
    private readonly GuildSettingsService gss;

    public FilterService(DiscordSocketClient client, DbService db, IPubSub pubSub,
        UserPunishService upun2, IBotStrings strng, AdministrationService ass,
        GuildSettingsService gss, EventHandler eventHandler)
    {
        this.db = db;
        this.client = client;
        this.pubSub = pubSub;
        Upun = upun2;
        Strings = strng;
        Reload(false);
        this.pubSub.Sub(blPubKey, OnReload);
        Ass = ass;
        this.gss = gss;
        using (var uow = db.GetDbContext())
        {
            var ids = client.GetGuildIds();
            var configs = uow.GuildConfigs
                .AsQueryable()
                .Include(x => x.FilteredWords)
                .Include(x => x.FilterLinksChannelIds)
                .Include(x => x.FilterWordsChannelIds)
                .Include(x => x.FilterInvitesChannelIds)
                .Where(gc => ids.Contains(gc.GuildId))
                .ToList();

            InviteFilteringServers =
                new ConcurrentHashSet<ulong>(configs.Where(gc => gc.FilterInvites).Select(gc => gc.GuildId));
            InviteFilteringChannels =
                new ConcurrentHashSet<ulong>(configs.SelectMany(gc =>
                    gc.FilterInvitesChannelIds.Select(fci => fci.ChannelId)));

            LinkFilteringServers =
                new ConcurrentHashSet<ulong>(configs.Where(gc => gc.FilterLinks).Select(gc => gc.GuildId));
            LinkFilteringChannels =
                new ConcurrentHashSet<ulong>(configs.SelectMany(gc =>
                    gc.FilterLinksChannelIds.Select(fci => fci.ChannelId)));

            var dict = configs.ToDictionary(gc => gc.GuildId,
                gc => new ConcurrentHashSet<string>(gc.FilteredWords.Select(fw => fw.Word)));

            ServerFilteredWords = new ConcurrentDictionary<ulong, ConcurrentHashSet<string>>(dict);

            var serverFiltering = configs.Where(gc => gc.FilterWords);
            WordFilteringServers = new ConcurrentHashSet<ulong>(serverFiltering.Select(gc => gc.GuildId));
            WordFilteringChannels =
                new ConcurrentHashSet<ulong>(configs.SelectMany(gc =>
                    gc.FilterWordsChannelIds.Select(fwci => fwci.ChannelId)));
        }

        eventHandler.MessageUpdated += (_, newMsg, channel) =>
        {
            var guild = (channel as ITextChannel)?.Guild;

            if (guild == null || newMsg is not IUserMessage usrMsg)
                return Task.CompletedTask;

            return RunBehavior(null, guild, usrMsg);
        };
    }

    public IBotStrings Strings { get; set; }
    public ConcurrentHashSet<ulong> InviteFilteringChannels { get; }
    public ConcurrentHashSet<ulong> InviteFilteringServers { get; }

    //serverid, filteredwords
    public ConcurrentDictionary<ulong, ConcurrentHashSet<string>> ServerFilteredWords { get; }

    public ConcurrentHashSet<ulong> WordFilteringChannels { get; }
    public ConcurrentHashSet<ulong> WordFilteringServers { get; }

    public ConcurrentHashSet<ulong> LinkFilteringChannels { get; }
    public ConcurrentHashSet<ulong> LinkFilteringServers { get; }

    public int Priority => -50;
    public ModuleBehaviorType BehaviorType => ModuleBehaviorType.Blocker;

    public async Task<bool> RunBehavior(DiscordSocketClient socketClient, IGuild guild, IUserMessage msg) =>
        msg.Author is IGuildUser gu && !gu.RoleIds.Contains(await Ass.GetStaffRole(guild.Id)) &&
        !gu.GuildPermissions.Administrator && (await FilterInvites(guild, msg).ConfigureAwait(false)
                                               || await FilterWords(guild, msg).ConfigureAwait(false)
                                               || await FilterLinks(guild, msg).ConfigureAwait(false)
                                               || await FilterBannedWords(guild, msg).ConfigureAwait(false));

    private ValueTask OnReload(AutoBanEntry[] blacklist)
    {
        Blacklist = blacklist;
        return default;
    }

    public void Reload(bool publish = true)
    {
        using var uow = db.GetDbContext();
        var toPublish = uow.AutoBanWords.AsNoTracking().ToArray();
        Blacklist = toPublish;
        if (publish) pubSub.Pub(blPubKey, toPublish);
    }

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

    public ConcurrentHashSet<string?>? FilteredWordsForChannel(ulong channelId, ulong guildId)
    {
        var words = new ConcurrentHashSet<string>();
        if (WordFilteringChannels.Contains(channelId))
            ServerFilteredWords.TryGetValue(guildId, out words);
        return words;
    }

    public async Task<int> GetInvWarn(ulong id) => (await gss.GetGuildConfig(id)).invwarn;

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
            gss.UpdateGuildConfig(guild.Id, gc);
        }
    }

    public async Task<int> GetFw(ulong id) => (await gss.GetGuildConfig(id)).fwarn;

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
            gss.UpdateGuildConfig(guild.Id, gc);
        }
    }

    public async Task ClearFilteredWords(ulong guildId)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guildId,
            set => set.Include(x => x.FilteredWords)
                .Include(x => x.FilterWordsChannelIds));

        WordFilteringServers.TryRemove(guildId);
        ServerFilteredWords.TryRemove(guildId, out _);

        foreach (var c in gc.FilterWordsChannelIds) WordFilteringChannels.TryRemove(c.ChannelId);

        gc.FilterWords = false;
        gc.FilteredWords.Clear();
        gc.FilterWordsChannelIds.Clear();

        await uow.SaveChangesAsync().ConfigureAwait(false);
    }

    public ConcurrentHashSet<string>? FilteredWordsForServer(ulong guildId)
    {
        var words = new ConcurrentHashSet<string>();
        if (WordFilteringServers.Contains(guildId))
            ServerFilteredWords.TryGetValue(guildId, out words);
        return words;
    }

    protected string? GetText(string? key, params object?[] args) => Strings.GetText(key, cultureInfo, args);

    public async Task<bool> FilterBannedWords(IGuild? guild, IUserMessage? msg)
    {
        if (guild is null)
            return false;
        if (msg is null)
            return false;
        var bannedwords = Blacklist.Where(x => x.GuildId == guild.Id);
        foreach (var i in bannedwords.Select(x => x.Word))
        {
            var regex = new Regex(i, RegexOptions.Compiled, TimeSpan.FromMilliseconds(250));
            var match = regex.Match(msg.Content.ToLower()).Value;
            if (!regex.IsMatch(msg.Content.ToLower())) continue;
            try
            {
                await msg.DeleteAsync().ConfigureAwait(false);
                var defaultMessage = GetText("bandm", Format.Bold(guild.Name),
                    $"Banned for saying autoban word {i}");
                var embed = await Upun.GetBanUserDmEmbed(client, guild as SocketGuild,
                    await guild.GetUserAsync(client.CurrentUser.Id).ConfigureAwait(false), msg.Author as IGuildUser, defaultMessage,
                    $"Banned for saying autoban word {match}", null).ConfigureAwait(false);
                await (await msg.Author.CreateDMChannelAsync().ConfigureAwait(false)).SendMessageAsync(embed.Item2, embeds: embed.Item1, components: embed.Item3.Build())
                    .ConfigureAwait(false);
                await guild.AddBanAsync(msg.Author, 0, options: new RequestOptions
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
                var regex = new Regex(word, RegexOptions.Compiled, TimeSpan.FromMilliseconds(250));
                if (!regex.IsMatch(usrMsg.Content.ToLower())) continue;
                try
                {
                    await usrMsg.DeleteAsync().ConfigureAwait(false);
                    if (await GetFw(guild.Id) != 0)
                    {
                        await Upun.Warn(guild, usrMsg.Author.Id, client.CurrentUser,
                            "Warned for Filtered Word").ConfigureAwait(false);
                        var user = await usrMsg.Author.CreateDMChannelAsync().ConfigureAwait(false);
                        await user.SendErrorAsync($"You have been warned for using the word {Format.Code(regex.Match(usrMsg.Content.ToLower()).Value)}").ConfigureAwait(false);
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
                    await Upun.Warn(guild, usrMsg.Author.Id, client.CurrentUser,
                        "Warned for Filtered Word").ConfigureAwait(false);
                    var user = await usrMsg.Author.CreateDMChannelAsync().ConfigureAwait(false);
                    await user.SendErrorAsync($"You have been warned for using the word {Format.Code(regex.Match(usrMsg.Content.ToLower()).Value)}").ConfigureAwait(false);
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
                await Upun.Warn(guild, usrMsg.Author.Id, client.CurrentUser, "Warned for Posting Invite").ConfigureAwait(false);
                var user = await usrMsg.Author.CreateDMChannelAsync().ConfigureAwait(false);
                await user.SendErrorAsync("You have been warned for sending an invite, this is not allowed!").ConfigureAwait(false);

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