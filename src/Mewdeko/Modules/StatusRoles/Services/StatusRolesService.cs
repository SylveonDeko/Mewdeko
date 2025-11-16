using System.Text;
using DataModel;
using LinqToDB;
using LinqToDB.Async;
using ZiggyCreatures.Caching.Fusion;

namespace Mewdeko.Modules.StatusRoles.Services;

/// <summary>
///     Service responsible for managing status-based roles.
/// </summary>
public class StatusRolesService : INService
{
    private readonly IFusionCache cache;
    private readonly DiscordShardedClient client;
    private readonly IDataConnectionFactory dbFactory;
    private readonly ConcurrentDictionary<ulong, HashSet<StatusRole>> guildStatusRoles = new();
    private readonly ILogger<StatusRolesService> logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="StatusRolesService" /> class.
    /// </summary>
    /// <param name="client">The Discord socket client.</param>
    /// <param name="dbFactory">The database context provider.</param>
    /// <param name="eventHandler">The event handler.</param>
    /// <param name="cache">The data cache service.</param>
    /// <param name="logger">The logger instance for structured logging.</param>
    public StatusRolesService(DiscordShardedClient client, IDataConnectionFactory dbFactory, EventHandler eventHandler,
        IFusionCache cache, ILogger<StatusRolesService> logger)
    {
        this.client = client;
        this.dbFactory = dbFactory;
        this.cache = cache;
        this.logger = logger;
        eventHandler.Subscribe("PresenceUpdated", "StatusRolesService", EventHandlerOnPresenceUpdated);
        _ = OnReadyAsync();
    }

    /// <summary>
    ///     Initializes the service and caches status roles.
    /// </summary>
    private async Task OnReadyAsync()
    {
        logger.LogInformation("Starting {Type} Cache", GetType());
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        var statusRoles = await dbContext.StatusRoles.ToListAsync();

        foreach (var statusRole in statusRoles)
        {
            guildStatusRoles.AddOrUpdate(statusRole.GuildId,
                [statusRole],
                (_, set) =>
                {
                    set.Add(statusRole);
                    return set;
                });
        }

        await cache.SetAsync("statusRoles", statusRoles);

        logger.LogInformation("StatusRoles cached");
    }

    private async Task<List<StatusRole>> GetStatusRolesAsync()
    {
        var statusRoles = await cache.GetOrDefaultAsync<List<StatusRole>>("statusRoles");

        if (statusRoles != null) return statusRoles;
        await using var dbContext = await dbFactory.CreateConnectionAsync();
        statusRoles = await dbContext.StatusRoles.ToListAsync();
        await cache.SetAsync("statusRoles", statusRoles);

        return statusRoles;
    }

    private async Task EventHandlerOnPresenceUpdated(SocketUser args, SocketPresence args2, SocketPresence args3)
    {
        if (args is not SocketGuildUser user)
            return;

        if (user.Guild.Id != 900378009188565022)
            return;

        // Check if user is offline/invisible - if so, ignore completely
        var isOffline = args3?.Status == UserStatus.Offline || args3?.Status == UserStatus.Invisible;
        if (isOffline)
            return;

        // User is online - get their current custom status
        var status = args3.Activities?.FirstOrDefault() as CustomStatusGame;
        var currentState = status?.State;
        var currentEncodedStatus = currentState?.ToBase64();

        // Check what we have cached as their last known status
        var cachedStatus = await cache.GetOrDefaultAsync<string>($"userStatus_{user.Id}");

        // If status hasn't changed from our cache, don't process
        if (cachedStatus == currentEncodedStatus)
            return;

        // Status changed - update cache
        await cache.SetAsync($"userStatus_{user.Id}", currentEncodedStatus);

        if (!this.guildStatusRoles.TryGetValue(user.Guild.Id, out var guildStatusRoles))
            return;

        // Process role changes using cached status as "before"
        CustomStatusGame beforeStatus = null;
        if (!string.IsNullOrEmpty(cachedStatus))
        {
            var decodedStatus = Encoding.UTF8.GetString(Convert.FromBase64String(cachedStatus));
            beforeStatus = new CustomStatusGame(decodedStatus);
        }

        foreach (var statusRole in guildStatusRoles)
        {
            await ProcessStatusRole(user, status, beforeStatus, statusRole);
        }
    }

    private async Task ProcessStatusRole(SocketGuildUser user, CustomStatusGame status, CustomStatusGame? beforeStatus,
        StatusRole? statusRole)
    {
        if (user.Guild.Id != 900378009188565022) return;
        var toAdd = string.IsNullOrWhiteSpace(statusRole.ToAdd)
            ? []
            : statusRole.ToAdd.Split(" ").Select(ulong.Parse).ToList();
        var toRemove = string.IsNullOrWhiteSpace(statusRole.ToRemove)
            ? []
            : statusRole.ToRemove.Split(" ").Select(ulong.Parse).ToList();

        if (status.State?.Contains(statusRole.Status) != true)
        {
            if (beforeStatus?.State?.Contains(statusRole.Status) == true)
            {
                await HandleRoleRemoval(user, statusRole, toAdd, toRemove);
            }

            return;
        }

        if (beforeStatus?.State?.Contains(statusRole.Status) == true)
            return;

        await HandleRoleAddition(user, statusRole, toAdd, toRemove);
    }

    private async Task HandleRoleRemoval(SocketGuildUser user, StatusRole statusRole, List<ulong> toAdd,
        List<ulong> toRemove)
    {
        if (statusRole.RemoveAdded)
        {
            await RemoveRoles(user, toAdd.Where(role => user.Roles.Any(r => r.Id == role)));
        }

        if (statusRole.ReaddRemoved)
        {
            await AddRoles(user, toRemove.Where(role => !user.Roles.Any(r => r.Id == role)));
        }
    }

    private async Task HandleRoleAddition(SocketGuildUser user, StatusRole statusRole, List<ulong> toAdd,
        List<ulong> toRemove)
    {
        await RemoveRoles(user, toRemove);
        await AddRoles(user, toAdd);

        var channel = user.Guild.GetTextChannel(statusRole.StatusChannelId);
        if (channel != null && !string.IsNullOrWhiteSpace(statusRole.StatusEmbed))
        {
            await SendStatusEmbed(user, channel, statusRole.StatusEmbed);
        }
    }

    private async Task RemoveRoles(SocketGuildUser user, IEnumerable<ulong> roleIds)
    {
        try
        {
            await user.RemoveRolesAsync(roleIds);
        }
        catch
        {
            logger.LogError("Unable to remove statusroles in {Guild} due to permission issues", user.Guild);
        }
    }

    private async Task AddRoles(SocketGuildUser user, IEnumerable<ulong> roleIds)
    {
        try
        {
            await user.AddRolesAsync(roleIds);
        }
        catch
        {
            logger.LogError("Unable to add statusroles in {Guild} due to permission issues", user.Guild);
        }
    }

    private async Task SendStatusEmbed(SocketGuildUser user, ITextChannel channel, string embedText)
    {
        var rep = new ReplacementBuilder().WithDefault(user, channel, user.Guild, client).Build();

        if (SmartEmbed.TryParse(rep.Replace(embedText), user.Guild.Id, out var embeds, out var plainText,
                out var components))
        {
            await channel.SendMessageAsync(plainText, embeds: embeds, components: components?.Build());
        }
        else
        {
            await channel.SendMessageAsync(rep.Replace(embedText));
        }
    }

    /// <summary>
    ///     Adds a new status role configuration for a guild.
    /// </summary>
    /// <param name="status">The status for which the role should be added.</param>
    /// <param name="guildId">The ID of the guild.</param>
    /// <returns>True if the configuration was successfully added; otherwise, false.</returns>
    public async Task<bool> AddStatusRoleConfig(string status, ulong guildId)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        if (await dbContext.StatusRoles.AnyAsync(x => x.GuildId == guildId && x.Status == status))
            return false;

        var toAdd = new StatusRole
        {
            Status = status, GuildId = guildId
        };
        var id = await dbContext.InsertWithInt32IdentityAsync(toAdd);

        toAdd.Id = id;

        guildStatusRoles.AddOrUpdate(guildId,
            [toAdd],
            (_, set) =>
            {
                set.Add(toAdd);
                return set;
            });

        var statusRoles = await GetStatusRolesAsync();
        statusRoles.Add(toAdd);
        await cache.SetAsync("statusRoles", statusRoles);

        return true;
    }

    /// <summary>
    ///     Removes a status role configuration by its index.
    /// </summary>
    /// <param name="index">The index of the status role configuration to remove.</param>
    public async Task RemoveStatusRoleConfig(int index)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        var status = await dbContext.StatusRoles.FirstOrDefaultAsync(x => x.Id == index);
        if (status == null)
            return;

        await dbContext.DeleteAsync(status);

        if (guildStatusRoles.TryGetValue(status.GuildId, out var guildSet))
        {
            guildSet.RemoveWhere(x => x.Id == index);
        }

        var statusRoles = await GetStatusRolesAsync();
        statusRoles.RemoveAll(x => x.Id == index);
        await cache.SetAsync("statusRoles", statusRoles);
    }

    /// <summary>
    ///     Removes a status role configuration.
    /// </summary>
    /// <param name="status">The status role configuration to remove.</param>
    public async Task RemoveStatusRoleConfig(StatusRole status)
    {
        try
        {
            await using var dbContext = await dbFactory.CreateConnectionAsync();

            await dbContext.DeleteAsync(status);

            if (guildStatusRoles.TryGetValue(status.GuildId, out var guildSet))
            {
                guildSet.Remove(status);
            }

            var statusRoles = await GetStatusRolesAsync();
            statusRoles.RemoveAll(x => x.Id == status.Id);
            await cache.SetAsync("statusRoles", statusRoles);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error removing status role config");
        }
    }

    /// <summary>
    ///     Retrieves the status role configurations for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <returns>The set of status role configurations for the guild.</returns>
    public async Task<HashSet<StatusRole>> GetStatusRoleConfig(ulong guildId)
    {
        var statusRoles = await GetStatusRolesAsync();
        var roles = statusRoles.Where(x => x.GuildId == guildId);
        return roles.ToHashSet();
    }

    /// <summary>
    ///     Sets the roles to be added when a specific status is detected.
    /// </summary>
    /// <param name="status">The status role configuration.</param>
    /// <param name="toAdd">The IDs of the roles to add.</param>
    /// <returns>True if the roles were successfully set; otherwise, false.</returns>
    public async Task<bool> SetAddRoles(StatusRole status, string toAdd)
    {
        return await UpdateStatusRoleConfig(status, s => s.ToAdd = toAdd);
    }

    /// <summary>
    ///     Sets the roles to be removed when a specific status is detected.
    /// </summary>
    /// <param name="status">The status role configuration.</param>
    /// <param name="toRemove">The IDs of the roles to remove.</param>
    /// <returns>True if the roles were successfully set; otherwise, false.</returns>
    public async Task<bool> SetRemoveRoles(StatusRole status, string toRemove)
    {
        return await UpdateStatusRoleConfig(status, s => s.ToRemove = toRemove);
    }

    /// <summary>
    ///     Sets the channel where status-based messages should be sent.
    /// </summary>
    /// <param name="status">The status role configuration.</param>
    /// <param name="channelId">The ID of the channel.</param>
    /// <returns>True if the channel was successfully set; otherwise, false.</returns>
    public async Task<bool> SetStatusChannel(StatusRole status, ulong channelId)
    {
        return await UpdateStatusRoleConfig(status, s => s.StatusChannelId = channelId);
    }

    /// <summary>
    ///     Sets the embed text for status-based messages.
    /// </summary>
    /// <param name="status">The status role configuration.</param>
    /// <param name="embedText">The embed text to set.</param>
    /// <returns>True if the embed text was successfully set; otherwise, false.</returns>
    public async Task<bool> SetStatusEmbed(StatusRole status, string embedText)
    {
        return await UpdateStatusRoleConfig(status, s => s.StatusEmbed = embedText);
    }

    /// <summary>
    ///     Toggles whether to remove roles that were added based on status.
    /// </summary>
    /// <param name="status">The status role configuration.</param>
    /// <returns>True if the toggle was successful; otherwise, false.</returns>
    public async Task<bool> ToggleRemoveAdded(StatusRole status)
    {
        return await UpdateStatusRoleConfig(status, s => s.RemoveAdded = !s.RemoveAdded);
    }

    /// <summary>
    ///     Toggles whether to add roles that were removed based on status.
    /// </summary>
    /// <param name="status">The status role configuration.</param>
    /// <returns>True if the toggle was successful; otherwise, false.</returns>
    public async Task<bool> ToggleAddRemoved(StatusRole status)
    {
        return await UpdateStatusRoleConfig(status, s => s.ReaddRemoved = !s.ReaddRemoved);
    }

    private async Task<bool> UpdateStatusRoleConfig(StatusRole status, Action<StatusRole> updateAction)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        var dbStatus = await dbContext.StatusRoles.FirstOrDefaultAsync(x => x.Id == status.Id);
        if (dbStatus == null)
            return false;

        updateAction(dbStatus);
        await dbContext.UpdateAsync(dbStatus);

        if (guildStatusRoles.TryGetValue(status.GuildId, out var guildSet))
        {
            guildSet.RemoveWhere(x => x.Id == status.Id);
            guildSet.Add(dbStatus);
        }

        var statusRoles = await GetStatusRolesAsync();
        var listIndex = statusRoles.FindIndex(x => x.Id == status.Id);
        if (listIndex == -1) return true;
        statusRoles[listIndex] = dbStatus;
        await cache.SetAsync("statusRoles", statusRoles);

        return true;
    }
}