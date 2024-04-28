using Mewdeko.Database.Common;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Mewdeko.Modules.Administration.Services;

/// <summary>
/// Service for managing role commands.
/// </summary>
public class RoleCommandsService : INService
{
    private readonly DbService db;
    private readonly GuildSettingsService gss;

    /// <summary>
    /// Initializes a new instance of the <see cref="RoleCommandsService"/> class.
    /// </summary>
    /// <param name="db">The database service.</param>
    /// <param name="eventHandler">The event handler.</param>
    /// <param name="bot">The bot.</param>
    public RoleCommandsService(DbService db, EventHandler eventHandler, Mewdeko bot, GuildSettingsService gss)
    {
        this.db = db;
        this.gss = gss;
        eventHandler.ReactionAdded += _client_ReactionAdded;
        eventHandler.ReactionRemoved += _client_ReactionRemoved;
    }

    /// <summary>
    /// Handles the ReactionAdded event of the client.
    /// </summary>
    /// <param name="msg">The message.</param>
    /// <param name="chan">The channel.</param>
    /// <param name="reaction">The reaction.</param>
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

            var config = await gss.GetGuildConfig(gch.Guild.Id);
            var reactRoles = config.ReactionRoleMessages;

            if (reactRoles == null || reactRoles.Count == 0)
                return;

            IUserMessage message;
            if (msg.HasValue)
                message = msg.Value;
            else
                message = await msg.GetOrDownloadAsync();

            var conf = reactRoles.FirstOrDefault(x => x.MessageId == message.Id);

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
            Log.Error($"Reaction Role Add failed in {gch.Guild}\n{ex}");
        }
    }

    /// <summary>
    /// Handles the ReactionRemoved event of the client.
    /// </summary>
    /// <param name="msg">The message.</param>
    /// <param name="chan">The channel.</param>
    /// <param name="reaction">The reaction.</param>
    private async Task _client_ReactionRemoved(Cacheable<IUserMessage, ulong> msg,
        Cacheable<IMessageChannel, ulong> chan,
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

            var config = await gss.GetGuildConfig(gch.Guild.Id);
            var reactRoles = config.ReactionRoleMessages;

            if (reactRoles == null || reactRoles.Count == 0)
                return;

            IUserMessage message;
            if (msg.HasValue)
                message = msg.Value;
            else
                message = await msg.GetOrDownloadAsync();

            var conf = reactRoles.FirstOrDefault(x => x.MessageId == message.Id);

            // compare emote names for backwards compatibility :facepalm:
            var reactionRole = conf?.ReactionRoles.Find(x =>
                x.EmoteName == reaction.Emote.Name || x.EmoteName == reaction.Emote.ToString());
            if (reactionRole == null)
                return;

            var toRemove = gusr.Guild.GetRole(reactionRole.RoleId);
            if (toRemove != null && gusr.Roles.Contains(toRemove))
                await gusr.RemoveRolesAsync(new[]
                {
                    toRemove
                }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            var gch = chan.Value as IGuildChannel;
            Log.Error($"Reaction Role Remove failed in {gch.Guild}\n{ex}");
        }
    }

    /// <summary>
    /// Gets the reaction role messages for a guild.
    /// </summary>
    /// <param name="id">The guild ID.</param>
    /// <returns>A boolean indicating whether the operation was successful.</returns>
    public async Task<(bool, IndexedCollection<ReactionRoleMessage>)> Get(ulong id)
    {
        var config = await gss.GetGuildConfig(id);
        var reactRoles = config.ReactionRoleMessages;

        if (reactRoles == null || reactRoles.Count == 0)
        {
            return (false, null);
        }

        return (true, reactRoles);
    }

    /// <summary>
    /// Adds a reaction role message to a guild.
    /// </summary>
    /// <param name="id">The guild ID.</param>
    /// <param name="rrm">The reaction role message.</param>
    /// <returns>A task that represents the asynchronous operation and contains a boolean indicating whether the operation was successful.</returns>
    public async Task<bool> Add(ulong id, ReactionRoleMessage rrm)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(id, set => set
            .Include(x => x.ReactionRoleMessages)
            .ThenInclude(x => x.ReactionRoles));
        gc.ReactionRoleMessages.Add(rrm);
        await gss.UpdateGuildConfig(id, gc);
        return true;
    }

    /// <summary>
    /// Removes a reaction role message from a guild.
    /// </summary>
    /// <param name="id">The guild ID.</param>
    /// <param name="index">The index of the reaction role message to remove.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task Remove(ulong id, int index)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(id,
            set => set.Include(x => x.ReactionRoleMessages)
                .ThenInclude(x => x.ReactionRoles));
        uow.Set<ReactionRole>()
            .RemoveRange(gc.ReactionRoleMessages[index].ReactionRoles);
        gc.ReactionRoleMessages.RemoveAt(index);
        await gss.UpdateGuildConfig(id, gc);
    }
}