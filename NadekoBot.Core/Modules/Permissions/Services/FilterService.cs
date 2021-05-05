using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Net;
using Discord.WebSocket;
using NadekoBot.Common.Collections;
using NadekoBot.Common.ModuleBehaviors;
using NadekoBot.Extensions;
using NadekoBot.Core.Services;
using NLog;
using Microsoft.EntityFrameworkCore;
using NadekoBot.Core.Services.Database.Models;

namespace NadekoBot.Modules.Permissions.Services
{
    public class FilterService : IEarlyBehavior, INService
    {
        private readonly Logger _log;
        private readonly DbService _db;

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

        public ConcurrentHashSet<string> FilteredWordsForChannel(ulong channelId, ulong guildId)
        {
            ConcurrentHashSet<string> words = new ConcurrentHashSet<string>();
            if (WordFilteringChannels.Contains(channelId))
                ServerFilteredWords.TryGetValue(guildId, out words);
            return words;
        }

        public void ClearFilteredWords(ulong guildId)
        {
            using (var uow = _db.GetDbContext())
            {
                var gc = uow.GuildConfigs.ForId(guildId,
                    set => set.Include(x => x.FilteredWords)
                        .Include(x => x.FilterWordsChannelIds));

                WordFilteringServers.TryRemove(guildId);
                ServerFilteredWords.TryRemove(guildId, out _);

                foreach (var c in gc.FilterWordsChannelIds)
                {
                    WordFilteringChannels.TryRemove(c.ChannelId);
                }

                gc.FilterWords = false;
                gc.FilteredWords.Clear();
                gc.FilterWordsChannelIds.Clear();

                uow.SaveChanges();
            }
        }

        public ConcurrentHashSet<string> FilteredWordsForServer(ulong guildId)
        {
            var words = new ConcurrentHashSet<string>();
            if (WordFilteringServers.Contains(guildId))
                ServerFilteredWords.TryGetValue(guildId, out words);
            return words;
        }

        public FilterService(DiscordSocketClient client, NadekoBot bot, DbService db)
        {
            _log = LogManager.GetCurrentClassLogger();
            _db = db;

            using(var uow = db.GetDbContext())
            {
                var ids = client.GetGuildIds();
                var configs = uow._context.Set<GuildConfig>()
                    .AsQueryable()
                    .Include(x => x.FilteredWords)
                    .Include(x => x.FilterLinksChannelIds)
                    .Include(x => x.FilterWordsChannelIds)
                    .Include(x => x.FilterInvitesChannelIds)
                    .Where(gc => ids.Contains(gc.GuildId))
                    .ToList();
                    
                InviteFilteringServers = new ConcurrentHashSet<ulong>(configs.Where(gc => gc.FilterInvites).Select(gc => gc.GuildId));
                InviteFilteringChannels = new ConcurrentHashSet<ulong>(configs.SelectMany(gc => gc.FilterInvitesChannelIds.Select(fci => fci.ChannelId)));

                LinkFilteringServers = new ConcurrentHashSet<ulong>(configs.Where(gc => gc.FilterLinks).Select(gc => gc.GuildId));
                LinkFilteringChannels = new ConcurrentHashSet<ulong>(configs.SelectMany(gc => gc.FilterLinksChannelIds.Select(fci => fci.ChannelId)));

                var dict = configs.ToDictionary(gc => gc.GuildId, gc => new ConcurrentHashSet<string>(gc.FilteredWords.Select(fw => fw.Word)));

                ServerFilteredWords = new ConcurrentDictionary<ulong, ConcurrentHashSet<string>>(dict);

                var serverFiltering = configs.Where(gc => gc.FilterWords);
                WordFilteringServers = new ConcurrentHashSet<ulong>(serverFiltering.Select(gc => gc.GuildId));
                WordFilteringChannels = new ConcurrentHashSet<ulong>(configs.SelectMany(gc => gc.FilterWordsChannelIds.Select(fwci => fwci.ChannelId)));
            }

            client.MessageUpdated += (oldData, newMsg, channel) =>
            {
                var _ = Task.Run(() =>
                {
                    var guild = (channel as ITextChannel)?.Guild;
                    var usrMsg = newMsg as IUserMessage;

                    if (guild == null || usrMsg == null)
                        return Task.CompletedTask;

                    return RunBehavior(null, guild, usrMsg);
                });
                return Task.CompletedTask;
            };
        }

        public async Task<bool> RunBehavior(DiscordSocketClient _, IGuild guild, IUserMessage msg)
            => !(msg.Author is IGuildUser gu) //it's never filtered outside of guilds, and never block administrators
                ? false
                : !gu.GuildPermissions.Administrator &&
                    (await FilterInvites(guild, msg).ConfigureAwait(false)
                    || await FilterWords(guild, msg).ConfigureAwait(false)
                    || await FilterLinks(guild, msg).ConfigureAwait(false));

        public async Task<bool> FilterWords(IGuild guild, IUserMessage usrMsg)
        {
            if (guild is null)
                return false;
            if (usrMsg is null)
                return false;

            var filteredChannelWords = FilteredWordsForChannel(usrMsg.Channel.Id, guild.Id) ?? new ConcurrentHashSet<string>();
            var filteredServerWords = FilteredWordsForServer(guild.Id) ?? new ConcurrentHashSet<string>();
            var wordsInMessage = usrMsg.Content.ToLowerInvariant().Split(' ');
            if (filteredChannelWords.Count != 0 || filteredServerWords.Count != 0)
            {
                foreach (var word in wordsInMessage)
                {
                    if (filteredChannelWords.Contains(word) ||
                        filteredServerWords.Contains(word))
                    {
                        try
                        {
                            await usrMsg.DeleteAsync().ConfigureAwait(false);
                        }
                        catch (HttpException ex)
                        {
                            _log.Warn("I do not have permission to filter words in channel with id " + usrMsg.Channel.Id, ex);
                        }
                        return true;
                    }
                }
            }
            return false;
        }

        public async Task<bool> FilterInvites(IGuild guild, IUserMessage usrMsg)
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
                    return true;
                }
                catch (HttpException ex)
                {
                    _log.Warn("I do not have permission to filter invites in channel with id " + usrMsg.Channel.Id, ex);
                    return true;
                }
            }
            return false;
        }

        public async Task<bool> FilterLinks(IGuild guild, IUserMessage usrMsg)
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
                catch (HttpException ex)
                {
                    _log.Warn("I do not have permission to filter links in channel with id " + usrMsg.Channel.Id, ex);
                    return true;
                }
            }
            return false;
        }
    }
}
