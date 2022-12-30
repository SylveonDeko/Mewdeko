using System.Threading.Tasks;
using Serilog;

namespace Mewdeko.Modules.StatusRoles.Services;

public class StatusRolesService : INService
{
    private readonly DiscordSocketClient client;
    private readonly DbService db;
    private readonly IDataCache cache;
    private readonly List<ulong> proccesingUserCache = new List<ulong>();

    public StatusRolesService(DiscordSocketClient client, DbService db, EventHandler eventHandler, IDataCache cache)
    {
        this.client = client;
        this.db = db;
        this.cache = cache;
        eventHandler.PresenceUpdated += EventHandlerOnPresenceUpdated;
    }

    private async Task EventHandlerOnPresenceUpdated(SocketUser args, SocketPresence args2, SocketPresence args3)
    {
        if (proccesingUserCache.Contains(args.Id))
            return;
        proccesingUserCache.Add(args.Id);
        var status = args3.Activities.FirstOrDefault() as CustomStatusGame;
        var beforeStatus = args2.Activities.FirstOrDefault() as CustomStatusGame;
        if (!await cache.SetUserStatusCache(args.Id, status.State.GetHashCode()))
        {
            proccesingUserCache.Remove(args.Id);
            return;
        }

        await using var uow = db.GetDbContext();
        var statusRolesConfigs = await cache.GetStatusRoleCache();
        foreach (var i in statusRolesConfigs)
        {
            var guild = client.GetGuild(i.GuildId) as IGuild;
            var curUser = await guild.GetUserAsync(args.Id);
            var toAdd = new List<ulong>();
            var toRemove = new List<ulong>();
            if (!string.IsNullOrWhiteSpace(i.ToAdd))
                toAdd = i.ToAdd.Split(" ").Select(ulong.Parse).ToList();
            if (!string.IsNullOrWhiteSpace(i.ToRemove))
                toRemove = i.ToRemove.Split(" ").Select(ulong.Parse).ToList();

            if (!status.State.Contains(i.Status))
            {
                if (beforeStatus is not null && beforeStatus.State.Contains(i.Status))
                {
                    if (i.RemoveAdded)
                    {
                        proccesingUserCache.Remove(args.Id);
                        if (toAdd.Any())
                        {
                            foreach (var role in toAdd.Where(socketRole => curUser.RoleIds.Contains(socketRole)))
                            {
                                try
                                {
                                    await curUser.RemoveRoleAsync(role);
                                }
                                catch
                                {
                                    Log.Error($"Unable to remove added role {role} for {curUser} in {guild} due to permission issues.");
                                }
                            }
                        }
                    }

                    if (i.ReaddRemoved)
                    {
                        proccesingUserCache.Remove(args.Id);
                        if (toRemove.Any())
                        {
                            foreach (var role in toRemove.Where(socketRole => !curUser.RoleIds.Contains(socketRole)))
                            {
                                try
                                {
                                    await curUser.AddRoleAsync(role);
                                }
                                catch
                                {
                                    Log.Error($"Unable to add removed role {role} for {curUser} in {guild} due to permission issues.");
                                }
                            }
                        }
                    }
                }
                else
                {
                    proccesingUserCache.Remove(args.Id);
                    continue;
                }
            }

            if (beforeStatus is not null && beforeStatus.State.Contains(i.Status))
            {
                proccesingUserCache.Remove(args.Id);
                continue;
            }

            if (toRemove.Any())
            {
                try
                {
                    await curUser.RemoveRolesAsync(toRemove);
                }
                catch
                {
                    Log.Error($"Unable to remove statusroles in {guild} due to permission issues.");
                }
            }

            if (toAdd.Any())
            {
                try
                {
                    await curUser.AddRolesAsync(toAdd);
                }
                catch
                {
                    Log.Error($"Unable to add statusroles in {guild} due to permission issues.");
                }
            }

            var channel = await guild.GetTextChannelAsync(i.StatusChannelId);

            if (channel is null)
            {
                proccesingUserCache.Remove(args.Id);
                continue;
            }

            if (string.IsNullOrWhiteSpace(i.StatusEmbed))
            {
                proccesingUserCache.Remove(args.Id);
                continue;
            }

            var rep = new ReplacementBuilder().WithDefault(curUser, channel, guild as SocketGuild, client).Build();

            if (SmartEmbed.TryParse(rep.Replace(i.StatusEmbed), guild.Id, out var embeds, out var plainText, out var components))
            {
                proccesingUserCache.Remove(args.Id);
                await channel.SendMessageAsync(plainText, embeds: embeds, components: components.Build());
            }
            else
            {
                proccesingUserCache.Remove(args.Id);
                await channel.SendMessageAsync(rep.Replace(i.StatusEmbed));
            }
        }
    }

    public async Task AddStatusRoleConfig(string status, ulong guildId)
    {
        await using var uow = db.GetDbContext();
        var toAdd = new StatusRolesTable
        {
            Status = status, GuildId = guildId
        };
        var getCache = await cache.GetStatusRoleCache();
        uow.StatusRoles.Add(toAdd);
        await uow.SaveChangesAsync();
        if (getCache.Any())
        {
            toAdd.Id = getCache.LastOrDefault().Id + 1;
            getCache.Add(toAdd);
            await cache.SetStatusRoleCache(getCache);
        }
        else
        {
            toAdd.Id = 1;
            getCache.Add(toAdd);
            await cache.SetStatusRoleCache(getCache);
        }
    }

    public async Task<List<StatusRolesTable>?> GetStatusRoleConfig(ulong guildId)
    {
        var statusList = await cache.GetStatusRoleCache();
        if (!statusList.Any())
            return new List<StatusRolesTable>();
        statusList = statusList.Where(x => x.GuildId == guildId).ToList();
        return statusList.Any() ? statusList : new List<StatusRolesTable>();
    }

    public async Task<bool> SetAddRoles(int index, string toAdd)
    {
        await using var uow = db.GetDbContext();
        var status = uow.StatusRoles.FirstOrDefault(x => x.Id == index);
        if (status is null)
            return false;
        status.ToAdd = toAdd;
        uow.StatusRoles.Update(status);
        await uow.SaveChangesAsync();
        var statusCache = await cache.GetStatusRoleCache();
        var listIndex = statusCache.IndexOf(statusCache.FirstOrDefault(x => x.Id == status.Id));
        statusCache[listIndex] = status;
        await cache.SetStatusRoleCache(statusCache);
        return true;
    }

    public async Task<bool> SetRemoveRoles(int index, string toAdd)
    {
        await using var uow = db.GetDbContext();
        var status = uow.StatusRoles.FirstOrDefault(x => x.Id == index);
        if (status is null)
            return false;
        status.ToRemove = toAdd;
        uow.StatusRoles.Update(status);
        await uow.SaveChangesAsync();
        var statusCache = await cache.GetStatusRoleCache();
        var listIndex = statusCache.IndexOf(statusCache.FirstOrDefault(x => x.Id == status.Id));
        statusCache[listIndex] = status;
        await cache.SetStatusRoleCache(statusCache);
        return true;
    }

    public async Task<bool> SetStatusChannel(int index, ulong channelId)
    {
        await using var uow = db.GetDbContext();
        var status = uow.StatusRoles.FirstOrDefault(x => x.Id == index);
        if (status is null)
            return false;
        status.StatusChannelId = channelId;
        uow.StatusRoles.Update(status);
        await uow.SaveChangesAsync();
        var statusCache = await cache.GetStatusRoleCache();
        var listIndex = statusCache.IndexOf(statusCache.FirstOrDefault(x => x.Id == status.Id));
        statusCache[listIndex] = status;
        await cache.SetStatusRoleCache(statusCache);
        return true;
    }

    public async Task<bool> SetStatusEmbed(int index, string embedText)
    {
        await using var uow = db.GetDbContext();
        var status = uow.StatusRoles.FirstOrDefault(x => x.Id == index);
        if (status is null)
            return false;
        status.StatusEmbed = embedText;
        uow.StatusRoles.Update(status);
        await uow.SaveChangesAsync();
        var statusCache = await cache.GetStatusRoleCache();
        var listIndex = statusCache.IndexOf(statusCache.FirstOrDefault(x => x.Id == status.Id));
        statusCache[listIndex] = status;
        await cache.SetStatusRoleCache(statusCache);
        return true;
    }

    public async Task<bool> ToggleRemoveAdded(int index)
    {
        await using var uow = db.GetDbContext();
        var status = uow.StatusRoles.FirstOrDefault(x => x.Id == index);
        if (status is null)
            return false;
        status.RemoveAdded = !status.RemoveAdded;
        uow.StatusRoles.Update(status);
        await uow.SaveChangesAsync();
        var statusCache = await cache.GetStatusRoleCache();
        var listIndex = statusCache.IndexOf(statusCache.FirstOrDefault(x => x.Id == status.Id));
        statusCache[listIndex] = status;
        await cache.SetStatusRoleCache(statusCache);
        return status.RemoveAdded;
    }

    public async Task<bool> ToggleAddRemoved(int index)
    {
        await using var uow = db.GetDbContext();
        var status = uow.StatusRoles.FirstOrDefault(x => x.Id == index);
        if (status is null)
            return false;
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