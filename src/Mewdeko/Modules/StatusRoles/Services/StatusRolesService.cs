using Mewdeko.Common.ModuleBehaviors;
using Serilog;
using Embed = Discord.Embed;

namespace Mewdeko.Modules.StatusRoles.Services;

/// <summary>
/// Service responsible for managing status-based roles.
/// </summary>
public class StatusRolesService : INService, IReadyExecutor
{
    private readonly IDataCache cache;
    private readonly DiscordShardedClient client;
    private readonly DbService db;
    private readonly HashSet<StatusRolesTable> statusRoles = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="StatusRolesService"/> class.
    /// </summary>
    /// <param name="client">The Discord socket client.</param>
    /// <param name="db">The database service.</param>
    /// <param name="eventHandler">The event handler.</param>
    /// <param name="cache">The data cache service.</param>
    public StatusRolesService(DiscordShardedClient client, DbService db, EventHandler eventHandler, IDataCache cache)
    {
        this.client = client;
        this.db = db;
        this.cache = cache;
        eventHandler.PresenceUpdated += EventHandlerOnPresenceUpdated;
    }

    /// <inheritdoc />
    public async Task OnReadyAsync()
    {
        await using var uow = db.GetDbContext();
        foreach (var i in uow.StatusRoles)
        {
            statusRoles.Add(i);
        }

        Log.Information("StatusRoles cached");
    }

    private async Task EventHandlerOnPresenceUpdated(SocketUser args, SocketPresence args2, SocketPresence args3)
    {
        try
        {
            if (this.statusRoles.Count == 0)
                return;

            if (args is not SocketGuildUser user)
                return;

            var beforeStatus = args2?.Activities?.FirstOrDefault() as CustomStatusGame;
            if (args3.Activities?.FirstOrDefault() is not CustomStatusGame status)
            {
                return;
            }

            if (status.State is null && beforeStatus?.State is null || status.State == beforeStatus?.State)
            {
                return;
            }

            if (!await cache.SetUserStatusCache(args.Id,
                    status.State?.ToBase64() is null ? "none" : status.State.ToBase64()))
            {
                return;
            }

            await using var uow = db.GetDbContext();
            if (statusRoles.Count == 0)
            {
                return;
            }

            var statusRolesTables = statusRoles.Where(x => x.GuildId == user.Guild.Id).ToList();

            foreach (var i in statusRolesTables)
            {
                var toAdd = new List<ulong>();
                var toRemove = new List<ulong>();
                if (!string.IsNullOrWhiteSpace(i.ToAdd))
                    toAdd = i.ToAdd.Split(" ").Select(ulong.Parse).ToList();
                if (!string.IsNullOrWhiteSpace(i.ToRemove))
                    toRemove = i.ToRemove.Split(" ").Select(ulong.Parse).ToList();
                if (status.State is null || !status.State.Contains(i.Status))
                {
                    if (beforeStatus is not null && beforeStatus.State.Contains(i.Status))
                    {
                        if (i.RemoveAdded)
                        {
                            if (toAdd.Count != 0)
                            {
                                foreach (var role in toAdd.Where(socketRole =>
                                             user.Roles.Select(x => x.Id).Contains(socketRole)))
                                {
                                    try
                                    {
                                        await user.RemoveRoleAsync(role);
                                    }
                                    catch
                                    {
                                        Log.Error(
                                            "Unable to remove added role {Role} for {User} in {UserGuild} due to permission issues",
                                            role, user, user.Guild);
                                    }
                                }
                            }
                        }

                        if (i.ReaddRemoved)
                        {
                            if (toRemove.Count != 0)
                            {
                                foreach (var role in toRemove.Where(socketRole =>
                                             !user.Roles.Select(x => x.Id).Contains(socketRole)))
                                {
                                    try
                                    {
                                        await user.AddRoleAsync(role);
                                    }
                                    catch
                                    {
                                        Log.Error(
                                            $"Unable to add removed role {role} for {user} in {user.Guild} due to permission issues.");
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        continue;
                    }
                }

                if (beforeStatus is not null && beforeStatus.State.Contains(i.Status))
                {
                    continue;
                }

                if (toRemove.Count != 0)
                {
                    try
                    {
                        await user.RemoveRolesAsync(toRemove);
                    }
                    catch
                    {
                        Log.Error($"Unable to remove statusroles in {user.Guild} due to permission issues.");
                    }
                }

                if (toAdd.Any())
                {
                    try
                    {
                        await user.AddRolesAsync(toAdd);
                    }
                    catch
                    {
                        Log.Error($"Unable to add statusroles in {user.Guild} due to permission issues.");
                    }
                }

                var channel = user.Guild.GetTextChannel(i.StatusChannelId);

                if (channel is null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(i.StatusEmbed))
                {
                    continue;
                }

                var rep = new ReplacementBuilder().WithDefault(user, channel, user.Guild, client).Build();

                if (SmartEmbed.TryParse(rep.Replace(i.StatusEmbed), user.Guild.Id, out var embeds, out var plainText,
                        out var components))
                {
                    await channel.SendMessageAsync(plainText ?? null, embeds: embeds ?? Array.Empty<Embed>(),
                        components: components?.Build());
                }
                else
                {
                    await channel.SendMessageAsync(rep.Replace(i.StatusEmbed));
                }
            }
        }
        catch (Exception e)
        {
            var status = args3.Activities?.FirstOrDefault() as CustomStatusGame;
            Log.Error("Error in StatusRolesService. After Status: {Status} args: {Args2} args2: {Args3}\n{Exception}",
                status.State, args2, args3, e);
        }
    }

    /// <summary>
    /// Adds a new status role configuration for a guild.
    /// </summary>
    /// <param name="status">The status for which the role should be added.</param>
    /// <param name="guildId">The ID of the guild.</param>
    /// <returns>True if the configuration was successfully added; otherwise, false.</returns>
    public async Task<bool> AddStatusRoleConfig(string status, ulong guildId)
    {
        await using var uow = db.GetDbContext();
        var toAdd = new StatusRolesTable
        {
            Status = status, GuildId = guildId
        };
        if (uow.StatusRoles.Where(x => x.GuildId == guildId).Any(x => x.Status == status))
            return false;
        uow.StatusRoles.Add(toAdd);
        await uow.SaveChangesAsync();
        await cache.SetStatusRoleCache(uow.StatusRoles.ToList());
        return true;
    }

    /// <summary>
    /// Removes a status role configuration by its index.
    /// </summary>
    /// <param name="index">The index of the status role configuration to remove.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task RemoveStatusRoleConfig(int index)
    {
        await using var uow = db.GetDbContext();
        var status = uow.StatusRoles.FirstOrDefault(x => x.Id == index);
        if (status is null)
            return;
        uow.StatusRoles.Remove(status);
        await uow.SaveChangesAsync();
        var toremove = statusRoles.FirstOrDefault(x => x.Id == index);
        if (toremove is not null)
            statusRoles.Remove(toremove);
        statusRoles.Add(status);
    }

    /// <summary>
    /// Removes a status role configuration.
    /// </summary>
    /// <param name="status">The status role configuration to remove.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task RemoveStatusRoleConfig(StatusRolesTable status)
    {
        try
        {
            await using var uow = db.GetDbContext();
            uow.StatusRoles.Remove(status);
            await uow.SaveChangesAsync();
            var toremove = statusRoles.FirstOrDefault(x => x.Id == status.Id);
            if (toremove is not null)
                statusRoles.Remove(toremove);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    /// <summary>
    /// Retrieves the status role configurations for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <returns>The set of status role configurations for the guild.</returns>
    public Task<HashSet<StatusRolesTable>?> GetStatusRoleConfig(ulong guildId)
    {
        if (statusRoles.Count == 0)
            return Task.FromResult(new HashSet<StatusRolesTable>());
        var statusList = statusRoles.Where(x => x.GuildId == guildId).ToHashSet();
        return Task.FromResult(statusList.Count != 0 ? statusList : []);
    }

    /// <summary>
    /// Sets the roles to be added when a specific status is detected.
    /// </summary>
    /// <param name="status">The status role configuration.</param>
    /// <param name="toAdd">The IDs of the roles to add.</param>
    /// <returns>True if the roles were successfully set; otherwise, false.</returns>
    public async Task<bool> SetAddRoles(StatusRolesTable status, string toAdd)
    {
        await using var uow = db.GetDbContext();
        status.ToAdd = toAdd;
        uow.StatusRoles.Update(status);
        await uow.SaveChangesAsync();
        var statusCache = await cache.GetStatusRoleCache();
        var listIndex = statusCache.IndexOf(statusCache.FirstOrDefault(x => x.Id == status.Id));
        statusCache[listIndex] = status;
        await cache.SetStatusRoleCache(statusCache);
        return true;
    }

    /// <summary>
    /// Sets the roles to be removed when a specific status is detected.
    /// </summary>
    /// <param name="status">The status role configuration.</param>
    /// <param name="toRemove">The IDs of the roles to remove.</param>
    /// <returns>True if the roles were successfully set; otherwise, false.</returns>
    public async Task<bool> SetRemoveRoles(StatusRolesTable status, string toRemove)
    {
        await using var uow = db.GetDbContext();
        status.ToRemove = toRemove;
        uow.StatusRoles.Update(status);
        await uow.SaveChangesAsync();
        var statusCache = await cache.GetStatusRoleCache();
        var listIndex = statusCache.IndexOf(statusCache.FirstOrDefault(x => x.Id == status.Id));
        statusCache[listIndex] = status;
        await cache.SetStatusRoleCache(statusCache);
        return true;
    }

    /// <summary>
    /// Sets the channel where status-based messages should be sent.
    /// </summary>
    /// <param name="status">The status role configuration.</param>
    /// <param name="channelId">The ID of the channel.</param>
    /// <returns>True if the channel was successfully set; otherwise, false.</returns>
    public async Task<bool> SetStatusChannel(StatusRolesTable status, ulong channelId)
    {
        await using var uow = db.GetDbContext();
        status.StatusChannelId = channelId;
        uow.StatusRoles.Update(status);
        await uow.SaveChangesAsync();
        var statusCache = await cache.GetStatusRoleCache();
        var listIndex = statusCache.IndexOf(statusCache.FirstOrDefault(x => x.Id == status.Id));
        statusCache[listIndex] = status;
        await cache.SetStatusRoleCache(statusCache);
        return true;
    }

    /// <summary>
    /// Sets the embed text for status-based messages.
    /// </summary>
    /// <param name="status">The status role configuration.</param>
    /// <param name="embedText">The embed text to set.</param>
    /// <returns>True if the embed text was successfully set; otherwise, false.</returns>
    public async Task<bool> SetStatusEmbed(StatusRolesTable status, string embedText)
    {
        await using var uow = db.GetDbContext();
        status.StatusEmbed = embedText;
        uow.StatusRoles.Update(status);
        await uow.SaveChangesAsync();
        var statusCache = await cache.GetStatusRoleCache();
        var listIndex = statusCache.IndexOf(statusCache.FirstOrDefault(x => x.Id == status.Id));
        statusCache[listIndex] = status;
        await cache.SetStatusRoleCache(statusCache);
        return true;
    }

    /// <summary>
    /// Toggles whether to remove roles that were added based on status.
    /// </summary>
    /// <param name="status">The status role configuration.</param>
    /// <returns>True if the toggle was successful; otherwise, false.</returns>
    public async Task<bool> ToggleRemoveAdded(StatusRolesTable status)
    {
        await using var uow = db.GetDbContext();
        status.RemoveAdded = !status.RemoveAdded;
        uow.StatusRoles.Update(status);
        await uow.SaveChangesAsync();
        var statusCache = await cache.GetStatusRoleCache();
        var listIndex = statusCache.IndexOf(statusCache.FirstOrDefault(x => x.Id == status.Id));
        statusCache[listIndex] = status;
        await cache.SetStatusRoleCache(statusCache);
        return status.RemoveAdded;
    }

    /// <summary>
    /// Toggles whether to add roles that were removed based on status.
    /// </summary>
    /// <param name="status">The status role configuration.</param>
    /// <returns>True if the toggle was successful; otherwise, false.</returns>
    public async Task<bool> ToggleAddRemoved(StatusRolesTable status)
    {
        await using var uow = db.GetDbContext();
        status.ReaddRemoved = !status.ReaddRemoved;
        uow.StatusRoles.Update(status);
        await uow.SaveChangesAsync();
        var statusCache = await cache.GetStatusRoleCache();
        var listIndex = statusCache.IndexOf(statusCache.FirstOrDefault(x => x.Id == status.Id));
        statusCache[listIndex] = status;
        await cache.SetStatusRoleCache(statusCache);
        return status.ReaddRemoved;
    }
}