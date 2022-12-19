using System.Threading.Tasks;
using Mewdeko.Database.Common;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Mewdeko.Modules.Administration.Services;

public class RoleCommandsService : INService
{
    private readonly DbService db;
    private readonly ConcurrentDictionary<ulong, IndexedCollection<ReactionRoleMessage>> models;

    public RoleCommandsService(DiscordSocketClient client, DbService db, EventHandler eventHandler)
    {
        this.db = db;
        using var uow = db.GetDbContext();
        var gc = uow.GuildConfigs.Include(x => x.ReactionRoleMessages).Where(x => client.Guilds.Select(socketGuild => socketGuild.Id).Contains(x.GuildId));
        models = gc.ToDictionary(x => x.GuildId,
                x => x.ReactionRoleMessages)
            .ToConcurrent();
        eventHandler.ReactionAdded += _client_ReactionAdded;
        eventHandler.ReactionRemoved += _client_ReactionRemoved;
    }

    private async Task _client_ReactionAdded(Cacheable<IUserMessage, ulong> msg, Cacheable<IMessageChannel, ulong> chan,
        SocketReaction reaction)
    {
        try
        {
            if (!reaction.User.IsSpecified ||
                reaction.User.Value.IsBot ||
                reaction.User.Value is not SocketGuildUser gusr)
            {
                return;
            }

            if (chan.Value is not SocketGuildChannel gch)
                return;

            if (!models.TryGetValue(gch.Guild.Id, out var confs))
                return;
            IUserMessage message;
            if (msg.HasValue)
                message = msg.Value;
            else
                message = await msg.GetOrDownloadAsync();

            var conf = confs.FirstOrDefault(x => x.MessageId == message.Id);

            // compare emote names for backwards compatibility :facepalm:
            var reactionRole = conf?.ReactionRoles.Find(x =>
                x.EmoteName == reaction.Emote.Name || x.EmoteName == reaction.Emote.ToString());
            if (reactionRole == null)
                return;
            if (conf.Exclusive)
            {
                var roleIds = conf.ReactionRoles.Select(x => x.RoleId)
                    .Where(x => x != reactionRole.RoleId)
                    .Select(x => gusr.Guild.GetRole(x))
                    .Where(x => x != null);

                try
                {
                    //if the role is exclusive,
                    // remove all other reactions user added to the message
                    var dl = await msg.GetOrDownloadAsync().ConfigureAwait(false);
                    foreach (var (key, _) in dl.Reactions)
                    {
                        if (key.Name == reaction.Emote.Name)
                            continue;
                        try
                        {
                            await dl.RemoveReactionAsync(key, gusr).ConfigureAwait(false);
                        }
                        catch
                        {
                            // ignored
                        }

                        await Task.Delay(100).ConfigureAwait(false);
                    }
                }
                catch
                {
                    // ignored
                }

                await gusr.RemoveRolesAsync(roleIds).ConfigureAwait(false);
            }

            var toAdd = gusr.Guild.GetRole(reactionRole.RoleId);
            if (toAdd != null && !gusr.Roles.Contains(toAdd))
                await gusr.AddRolesAsync(new[]
                {
                    toAdd
                }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            var gch = chan.Value as IGuildChannel;
            Log.Error($"Reaction Role Add failed in {gch.Guild}\n{0}", ex);
        }
    }

    private async Task _client_ReactionRemoved(Cacheable<IUserMessage, ulong> msg, Cacheable<IMessageChannel, ulong> chan,
        SocketReaction reaction)
    {
        try
        {
            if (!reaction.User.IsSpecified ||
                reaction.User.Value.IsBot ||
                reaction.User.Value is not SocketGuildUser gusr)
            {
                return;
            }

            if (chan.Value is not SocketGuildChannel gch)
                return;
            IUserMessage message;
            if (msg.HasValue)
                message = msg.Value;
            else
                message = await msg.GetOrDownloadAsync();

            if (!models.TryGetValue(gch.Guild.Id, out var confs))
                return;
            var conf = confs.FirstOrDefault(x => x.MessageId == message.Id);

            if (conf == null)
                return;

            var reactionRole = conf.ReactionRoles.Find(x =>
                x.EmoteName == reaction.Emote.Name || x.EmoteName == reaction.Emote.ToString());

            if (reactionRole != null)
            {
                var role = gusr.Guild.GetRole(reactionRole.RoleId);
                if (role == null)
                    return;
                await gusr.RemoveRoleAsync(role).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            var gch = chan.Value as IGuildChannel;
            Log.Error($"Reaction Role Remove failed in {gch.Guild}\n{0}", ex);
        }
    }

    public bool Get(ulong id, out IndexedCollection<ReactionRoleMessage> rrs) => models.TryGetValue(id, out rrs);

    public async Task<bool> Add(ulong id, ReactionRoleMessage rrm)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(id, set => set
            .Include(x => x.ReactionRoleMessages)
            .ThenInclude(x => x.ReactionRoles));
        gc.ReactionRoleMessages.Add(rrm);
        models.AddOrUpdate(id,
            gc.ReactionRoleMessages,
            delegate { return gc.ReactionRoleMessages; });
        await uow.SaveChangesAsync().ConfigureAwait(false);

        return true;
    }

    public async Task Remove(ulong id, int index)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(id,
            set => set.Include(x => x.ReactionRoleMessages)
                .ThenInclude(x => x.ReactionRoles));
        uow.Set<ReactionRole>()
            .RemoveRange(gc.ReactionRoleMessages[index].ReactionRoles);
        gc.ReactionRoleMessages.RemoveAt(index);
        models.AddOrUpdate(id,
            gc.ReactionRoleMessages,
            delegate { return gc.ReactionRoleMessages; });
        await uow.SaveChangesAsync().ConfigureAwait(false);
    }
}