using Mewdeko.Common.ModuleBehaviors;
using Serilog;
using Embed = Discord.Embed;

namespace Mewdeko.Modules.StatusRoles.Services;

public class StatusRolesService : INService, IReadyExecutor
{
    private readonly DiscordSocketClient client;
    private readonly DbService db;
    private readonly IDataCache cache;
    private readonly HashSet<StatusRolesTable> statusRoles = [];

    public StatusRolesService(DiscordSocketClient client, DbService db, EventHandler eventHandler, IDataCache cache)
    {
        this.client = client;
        this.db = db;
        this.cache = cache;
        eventHandler.PresenceUpdated += EventHandlerOnPresenceUpdated;
    }

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
            // Early exit if there are no status roles
            if (this.statusRoles.Count == 0)
                return;

            // Ensure the incoming user is a guild user
            if (args is not SocketGuildUser user)
                return;

            // Get the status of the user before and after the event
            var beforeStatus = args2?.Activities?.FirstOrDefault() as CustomStatusGame;
            var afterStatus = args3.Activities?.FirstOrDefault() as CustomStatusGame;

            // Continue only if the event is non-null and status has changed
            if (afterStatus == null || afterStatus.State == beforeStatus?.State)
                return;

            // Update user status in cache only if status has changed
            var newStatusBase64 = afterStatus.State?.ToBase64() ?? "none";
            if (!await cache.SetUserStatusCache(args.Id, newStatusBase64))
                return;

            // Check for any statusRoles, if there are none, no further actions needed
            if (statusRoles.Count == 0)
                return;

            // Fetch role ids of user to local variable
            var userRoleIds = user.Roles.Select(x => x.Id).ToList();

            // Filter statusRoles for a particular guild
            var statusRolesTables = statusRoles.Where(x => x.GuildId == user.Guild.Id);

            // Loops through each status role in the guild
            foreach (var config in statusRolesTables)
            {
                var toAdd = string.IsNullOrWhiteSpace(config.ToAdd)
                    ? new List<ulong>()
                    : config.ToAdd.Split(" ").Select(ulong.Parse).Where(role => !userRoleIds.Contains(role)).ToList();

                var toRemove = string.IsNullOrWhiteSpace(config.ToRemove)
                    ? new List<ulong>()
                    : config.ToRemove.Split(" ").Select(ulong.Parse).Where(role => userRoleIds.Contains(role)).ToList();

                var channel = user.Guild.GetTextChannel(config.StatusChannelId);
                // If the StatusEmbed field is empty or channel is null, continue to the next statusRole
                if (string.IsNullOrWhiteSpace(config.StatusEmbed) || channel == null)
                    continue;

                if (afterStatus.State.Contains(config.Status) && beforeStatus?.State != afterStatus.State)
                {
                    // On Status Add
                    try
                    {
                        await user.AddRolesAsync(toAdd);
                    }
                    catch
                    {
                        Log.Error($"Unable to add status roles in {user.Guild} due to permission issues.");
                    }
                }
                else if ((beforeStatus?.State.Contains(config.Status) ?? false) &&
                         beforeStatus?.State != afterStatus.State)
                {
                    // On Status Remove
                    try
                    {
                        await user.RemoveRolesAsync(toRemove);
                    }
                    catch
                    {
                        Log.Error($"Unable to remove status roles in {user.Guild} due to permission issues.");
                    }
                }

                var rep = new ReplacementBuilder().WithDefault(user, channel, user.Guild, client).Build();

                if (SmartEmbed.TryParse(rep.Replace(config.StatusEmbed), user.Guild.Id, out var embeds,
                        out var plainText,
                        out var components))
                {
                    await channel.SendMessageAsync(plainText ?? null, embeds: embeds ?? Array.Empty<Embed>(),
                        components: components?.Build());
                }
                else
                {
                    await channel.SendMessageAsync(rep.Replace(config.StatusEmbed));
                }
            }
        }
        catch (Exception e)
        {
            var status = args3.Activities?.FirstOrDefault() as CustomStatusGame;
            Log.Error(
                "Error in StatusRolesService: {Exception}, After Status: {Status}, args2: {Args2}, args3: {Args3}",
                e, status?.State, args2, args3);
        }
    }

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

    public async Task<HashSet<StatusRolesTable>?> GetStatusRoleConfig(ulong guildId)
    {
        if (!statusRoles.Any())
            return new HashSet<StatusRolesTable>();
        var statusList = statusRoles.Where(x => x.GuildId == guildId).ToHashSet();
        return statusList.Any() ? statusList : new HashSet<StatusRolesTable>();
    }


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

    public async Task<bool> SetRemoveRoles(StatusRolesTable status, string toAdd)
    {
        await using var uow = db.GetDbContext();
        status.ToRemove = toAdd;
        uow.StatusRoles.Update(status);
        await uow.SaveChangesAsync();
        var statusCache = await cache.GetStatusRoleCache();
        var listIndex = statusCache.IndexOf(statusCache.FirstOrDefault(x => x.Id == status.Id));
        statusCache[listIndex] = status;
        await cache.SetStatusRoleCache(statusCache);
        return true;
    }

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

    public async Task<bool> ToggleRemoveAdded(StatusRolesTable status)
    {
        await using var uow = db.GetDbContext();
        status.RemoveAdded = status.RemoveAdded == 1 ? 0 : 1;
        uow.StatusRoles.Update(status);
        await uow.SaveChangesAsync();
        var statusCache = await cache.GetStatusRoleCache();
        var listIndex = statusCache.IndexOf(statusCache.FirstOrDefault(x => x.Id == status.Id));
        statusCache[listIndex] = status;
        await cache.SetStatusRoleCache(statusCache);
        return false.ParseBoth(status.RemoveAdded);
    }

    public async Task<bool> ToggleAddRemoved(StatusRolesTable status)
    {
        await using var uow = db.GetDbContext();
        status.ReaddRemoved = status.ReaddRemoved == 1 ? 0 : 1;
        uow.StatusRoles.Update(status);
        await uow.SaveChangesAsync();
        var statusCache = await cache.GetStatusRoleCache();
        var listIndex = statusCache.IndexOf(statusCache.FirstOrDefault(x => x.Id == status.Id));
        statusCache[listIndex] = status;
        await cache.SetStatusRoleCache(statusCache);
        return false.ParseBoth(status.ReaddRemoved);
    }
}