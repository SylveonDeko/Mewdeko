using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using NadekoBot.Common.Collections;
using NadekoBot.Core.Services;
using NadekoBot.Core.Services.Database.Models;
using NadekoBot.Extensions;
using NLog;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;

namespace NadekoBot.Modules.Administration.Services
{
    public class RoleCommandsService : INService
    {
        private readonly Logger _log;
        private readonly DbService _db;
        private readonly DiscordSocketClient _client;
        private readonly ConcurrentDictionary<ulong, IndexedCollection<ReactionRoleMessage>> _models;

        public RoleCommandsService(DiscordSocketClient client, DbService db,
            NadekoBot bot)
        {
            _log = LogManager.GetCurrentClassLogger();
            _db = db;
            _client = client;
#if !GLOBAL_NADEKO
            _models = bot.AllGuildConfigs.ToDictionary(x => x.GuildId,
                x => x.ReactionRoleMessages)
                .ToConcurrent();

            _client.ReactionAdded += _client_ReactionAdded;
            _client.ReactionRemoved += _client_ReactionRemoved;
#endif
        }

        private Task _client_ReactionAdded(Cacheable<IUserMessage, ulong> msg, ISocketMessageChannel chan, SocketReaction reaction)
        {
            var _ = Task.Run(async () =>
            {
                try
                {
                    if (!reaction.User.IsSpecified ||
                        reaction.User.Value.IsBot ||
                        !(reaction.User.Value is SocketGuildUser gusr))
                        return;

                    if (!(chan is SocketGuildChannel gch))
                        return;

                    if (!_models.TryGetValue(gch.Guild.Id, out var confs))
                        return;

                    var conf = confs.FirstOrDefault(x => x.MessageId == msg.Id);

                    if (conf == null)
                        return;

                    // compare emote names for backwards compatibility :facepalm:
                    var reactionRole = conf.ReactionRoles.FirstOrDefault(x => x.EmoteName == reaction.Emote.Name || x.EmoteName == reaction.Emote.ToString());
                    if (reactionRole != null)
                    {
                        if (conf.Exclusive)
                        {
                            var roleIds = conf.ReactionRoles.Select(x => x.RoleId)
                                .Where(x => x != reactionRole.RoleId)
                                .Select(x => gusr.Guild.GetRole(x))
                                .Where(x => x != null);

                            var __ = Task.Run(async () =>
                            {
                                try
                                {
                                    //if the role is exclusive,
                                    // remove all other reactions user added to the message
                                    var dl = await msg.GetOrDownloadAsync().ConfigureAwait(false);
                                    foreach (var r in dl.Reactions)
                                    {
                                        if (r.Key.Name == reaction.Emote.Name)
                                            continue;
                                        try { await dl.RemoveReactionAsync(r.Key, gusr).ConfigureAwait(false); } catch { }
                                        await Task.Delay(100).ConfigureAwait(false);
                                    }
                                }
                                catch { }
                            });
                            await gusr.RemoveRolesAsync(roleIds).ConfigureAwait(false);
                        }

                        var toAdd = gusr.Guild.GetRole(reactionRole.RoleId);
                        if (toAdd != null && !gusr.Roles.Contains(toAdd))
                        {
                            await gusr.AddRolesAsync(new[] { toAdd }).ConfigureAwait(false);
                        }
                    }
                    else
                    {
                        var dl = await msg.GetOrDownloadAsync().ConfigureAwait(false);
                        await dl.RemoveReactionAsync(reaction.Emote, dl.Author,
                            new RequestOptions()
                            {
                                RetryMode = RetryMode.RetryRatelimit | RetryMode.Retry502
                            }).ConfigureAwait(false);
                        _log.Warn("User {0} is adding unrelated reactions to the reaction roles message.", dl.Author);
                    }
                }
                catch { }
            });

            return Task.CompletedTask;
        }

        private Task _client_ReactionRemoved(Cacheable<IUserMessage, ulong> msg, ISocketMessageChannel chan, SocketReaction reaction)
        {
            var _ = Task.Run(async () =>
            {
                try
                {
                    if (!reaction.User.IsSpecified ||
                        reaction.User.Value.IsBot ||
                        !(reaction.User.Value is SocketGuildUser gusr))
                        return;

                    if (!(chan is SocketGuildChannel gch))
                        return;

                    if (!_models.TryGetValue(gch.Guild.Id, out var confs))
                        return;

                    var conf = confs.FirstOrDefault(x => x.MessageId == msg.Id);

                    if (conf == null)
                        return;

                    var reactionRole = conf.ReactionRoles.FirstOrDefault(x => x.EmoteName == reaction.Emote.Name || x.EmoteName == reaction.Emote.ToString());

                    if (reactionRole != null)
                    {
                        var role = gusr.Guild.GetRole(reactionRole.RoleId);
                        if (role == null)
                            return;
                        await gusr.RemoveRoleAsync(role).ConfigureAwait(false);
                    }
                }
                catch { }
            });

            return Task.CompletedTask;
        }

        public bool Get(ulong id, out IndexedCollection<ReactionRoleMessage> rrs)
        {
            return _models.TryGetValue(id, out rrs);
        }

        public bool Add(ulong id, ReactionRoleMessage rrm)
        {
            using (var uow = _db.GetDbContext())
            {
                var gc = uow.GuildConfigs.ForId(id, set => set
                    .Include(x => x.ReactionRoleMessages)
                    .ThenInclude(x => x.ReactionRoles));
                if (gc.ReactionRoleMessages.Count >= 5)
                    return false;
                gc.ReactionRoleMessages.Add(rrm);
                _models.AddOrUpdate(id,
                    gc.ReactionRoleMessages,
                    delegate { return gc.ReactionRoleMessages; });
                uow.SaveChanges();
            }
            return true;
        }

        public void Remove(ulong id, int index)
        {
            using (var uow = _db.GetDbContext())
            {
                var gc = uow.GuildConfigs.ForId(id,
                    set => set.Include(x => x.ReactionRoleMessages)
                        .ThenInclude(x => x.ReactionRoles));
                uow._context.Set<ReactionRole>()
                    .RemoveRange(gc.ReactionRoleMessages[index].ReactionRoles);
                gc.ReactionRoleMessages.RemoveAt(index);
                _models.AddOrUpdate(id,
                    gc.ReactionRoleMessages,
                    delegate { return gc.ReactionRoleMessages; });
                uow.SaveChanges();
            }
        }
    }
}
