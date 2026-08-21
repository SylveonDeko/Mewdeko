using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Modules.Administration.Common;
using Mewdeko.Modules.Moderation.Common;
using Mewdeko.Modules.Moderation.Services;
using Mewdeko.Services.Settings;
using Mewdeko.Services.Strings;

namespace Mewdeko.Modules.Server_Management.Services;

/// <summary>
///     Service that monitors role assignments and permission changes, enforcing blacklisted roles and permissions.
/// </summary>
public class RoleMonitorService : INService, IReadyExecutor
{
    private readonly BanPruneService banPrune;
    private readonly BotConfigService botConfigService;
    private readonly DiscordShardedClient client;
    private readonly IDataCache dataCache;
    private readonly IDataConnectionFactory dbFactory;

    // In-memory caches for quick access
    private readonly ConcurrentDictionary<ulong, GuildSettings> guildSettingsCache = new();
    private readonly MuteService muteService;
    private readonly GeneratedBotStrings strings;
    private readonly UserPunishService userPunishService;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RoleMonitorService" /> class.
    /// </summary>
    /// <param name="client">The Discord client.</param>
    /// <param name="handler">The event handler.</param>
    /// <param name="dbFactory">The database context provider.</param>
    /// <param name="dataCache">The data cache for accessing Redis.</param>
    /// <param name="userPunishService">The user punishment service.</param>
    /// <param name="botConfigService">The bot configuration service.</param>
    /// <param name="muteService">The mute service for handling user mutes.</param>
    /// <param name="strings">The localized strings service.</param>
    /// <param name="banPrune">The service resolving how many days of messages a ban purges</param>
    public RoleMonitorService(DiscordShardedClient client, EventHandler handler, IDataConnectionFactory dbFactory,
        IDataCache dataCache, UserPunishService userPunishService, BotConfigService botConfigService,
        MuteService muteService, GeneratedBotStrings strings, BanPruneService banPrune)
    {
        this.banPrune = banPrune;
        this.client = client;
        this.dbFactory = dbFactory;
        this.dataCache = dataCache;
        this.userPunishService = userPunishService;
        this.botConfigService = botConfigService;
        this.muteService = muteService;
        this.strings = strings;

        handler.Subscribe("AuditLogCreated", "RoleMonitorService", OnAuditLogCreatedAsync);
    }

    /// <summary>
    ///     Loads all guild settings into Redis and cache on startup.
    /// </summary>
    public async Task OnReadyAsync()
    {
        var redisDb = dataCache.Redis.GetDatabase();

        await using var context = await dbFactory.CreateConnectionAsync();

        var guilds = client.Guilds;

        foreach (var guild in guilds)
        {
            var guildId = guild.Id;

            var roleSettings = await context.RoleMonitoringSettings.FirstOrDefaultAsync(s => s.GuildId == guildId);
            var defaultPunishment = (ValueType)roleSettings?.DefaultPunishmentAction ?? PunishmentAction.None;

            await redisDb.StringSetAsync($"guild:{guildId}:default_punishment", ((int)defaultPunishment).ToString());

            var blacklistedRoles = await context.BlacklistedRoles.Where(r => r.GuildId == guildId).ToListAsync();
            var blacklistedRoleIds = new Dictionary<ulong, PunishmentAction?>();
            foreach (var role in blacklistedRoles)
            {
                var punishment = role.PunishmentAction ?? defaultPunishment;
                await redisDb.HashSetAsync($"guild:{guildId}:blacklisted_roles", role.RoleId.ToString(),
                    ((int)punishment).ToString());
                blacklistedRoleIds[role.RoleId] = (PunishmentAction?)punishment;
            }

            var blacklistedPermissions =
                await context.BlacklistedPermissions.Where(p => p.GuildId == guildId).ToListAsync();
            var blacklistedPermsDict = new Dictionary<GuildPermission, PunishmentAction?>();
            foreach (var perm in blacklistedPermissions)
            {
                var punishment = perm.PunishmentAction ?? defaultPunishment;
                await redisDb.HashSetAsync($"guild:{guildId}:blacklisted_permissions",
                    ((int)perm.Permission).ToString(), ((int)punishment).ToString());
                blacklistedPermsDict[(GuildPermission)perm.Permission] = (PunishmentAction?)punishment;
            }

            var whitelistedUsers = await context.WhitelistedUsers.Where(u => u.GuildId == guildId).ToListAsync();
            foreach (var user in whitelistedUsers)
            {
                await redisDb.SetAddAsync($"guild:{guildId}:whitelisted_users", user.UserId);
            }

            var whitelistedRoles = await context.WhitelistedRoles.Where(r => r.GuildId == guildId).ToListAsync();
            foreach (var role in whitelistedRoles)
            {
                await redisDb.SetAddAsync($"guild:{guildId}:whitelisted_roles", role.RoleId);
            }

            var guildSettings = new GuildSettings
            {
                GuildId = guildId,
                DefaultPunishmentAction = (PunishmentAction?)defaultPunishment,
                BlacklistedRoleIds = blacklistedRoleIds,
                BlacklistedPermissions = blacklistedPermsDict
            };

            guildSettingsCache[guildId] = guildSettings;
        }
    }

    /// <summary>
    ///     Handles the AuditLogCreated event to monitor role assignments and permission changes.
    /// </summary>
    /// <param name="guild">The guild where the audit log was created.</param>
    /// <param name="entry">The audit log entry.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task OnAuditLogCreatedAsync(SocketAuditLogEntry entry, SocketGuild guild)
    {
        switch (entry.Action)
        {
            case ActionType.MemberRoleUpdated:
                await HandleMemberRoleUpdatedAsync(guild, entry);
                break;
            case ActionType.RoleUpdated:
                await HandleRoleUpdatedAsync(guild, entry);
                break;
        }
    }

    /// <summary>
    ///     Handles role assignments, checking for blacklisted roles and permissions.
    /// </summary>
    private async Task HandleMemberRoleUpdatedAsync(IGuild guild, IAuditLogEntry entry)
    {
        if (entry.Data is not SocketMemberRoleAuditLogData roleUpdate)
            return;

        if (entry.User == null)
            return;

        var userId = roleUpdate.Target.Id;
        var user = await guild.GetUserAsync(userId);

        if (user == null)
            return;

        if (await IsUserWhitelistedAsync(guild, user.Id) || await IsRoleWhitelistedAsync(guild.Id, user.RoleIds))
            return;

        var addedRoles = roleUpdate.Roles.Where(x => x.Added).Select(x => x.RoleId).ToList();

        if (addedRoles.Count == 0)
            return;

        var gUser = await guild.GetUserAsync(entry.User.Id);

        await CheckRolesAsync(user, addedRoles, gUser);
    }


    /// <summary>
    ///     Handles role permission updates, checking for blacklisted permissions.
    /// </summary>
    private async Task HandleRoleUpdatedAsync(IGuild guild, IAuditLogEntry entry)
    {
        if (entry.Data is not SocketRoleUpdateAuditLogData roleUpdate)
            return;

        if (entry.User == null)
            return;

        var roleId = roleUpdate.RoleId;
        var role = guild.GetRole(roleId);
        var user = await guild.GetUserAsync(entry.User.Id);

        if (role == null || user == null)
            return;

        if (await IsRoleWhitelistedAsync(guild.Id, user.RoleIds))
            return;

        if (await IsUserWhitelistedAsync(guild, user.Id))
            return;

        var beforePermissions = roleUpdate.Before.Permissions;
        var afterPermissions = roleUpdate.After.Permissions;

        if (!beforePermissions.HasValue || !afterPermissions.HasValue)
            return;

        var addedPermissions = Enum.GetValues<GuildPermission>()
            .Where(p => afterPermissions.Value.Has(p) && !beforePermissions.Value.Has(p));

        await CheckRolePermissionsAsync(guild, role, addedPermissions, entry.User.Id, beforePermissions.Value);
    }


    /// <summary>
    ///     Checks if a user is whitelisted in the guild.
    /// </summary>
    private async Task<bool> IsUserWhitelistedAsync(IGuild guild, ulong userId)
    {
        if (guild.OwnerId == userId)
            return true;
        var redisDb = dataCache.Redis.GetDatabase();
        var key = $"guild:{guild.Id}:whitelisted_users";

        return await redisDb.SetContainsAsync(key, userId);
    }

    /// <summary>
    ///     Checks if any of the roles are whitelisted in the guild.
    /// </summary>
    private async Task<bool> IsRoleWhitelistedAsync(ulong guildId, IEnumerable<ulong> roleIds)
    {
        var redisDb = dataCache.Redis.GetDatabase();
        var key = $"guild:{guildId}:whitelisted_roles";

        var tasks = roleIds.Select(roleId => redisDb.SetContainsAsync(key, roleId));
        var results = await Task.WhenAll(tasks);

        return results.Any(r => r);
    }

    /// <summary>
    ///     Checks the roles assigned to a user for blacklisted roles and permissions.
    /// </summary>
    private async Task CheckRolesAsync(IGuildUser user, IEnumerable<ulong> roleIds, IGuildUser? executor)
    {
        var guild = user.Guild;
        var guildId = guild.Id;

        var guildSettings = await GetGuildSettingsAsync(guildId);

        foreach (var roleId in roleIds)
        {
            var role = guild.GetRole(roleId);
            if (role == null)
                continue;

            PunishmentAction? punishmentAction;

            if (guildSettings.BlacklistedRoleIds.TryGetValue(roleId, out var rolePunishment))
            {
                punishmentAction = rolePunishment;
            }
            else
            {
                var rolePermissions = role.Permissions;
                var blacklistedPermissions = guildSettings.BlacklistedPermissions
                    .Where(p => rolePermissions.Has(p.Key))
                    .Select(p => p.Value);

                if (blacklistedPermissions.Any())
                {
                    punishmentAction = blacklistedPermissions.OrderByDescending(p => p).FirstOrDefault();
                }
                else
                {
                    // No blacklisted role or permission found, so we continue to the next role
                    continue;
                }
            }

            punishmentAction ??= guildSettings.DefaultPunishmentAction;

            if (punishmentAction != PunishmentAction.None)
            {
                if (executor != null && !await IsUserWhitelistedAsync(guild, executor.Id))
                {
                    await ApplyPunishmentAsync(executor, punishmentAction.Value, role);
                }
            }

            // Remove the role only if a blacklisted role or permission was found
            await user.RemoveRoleAsync(role);
        }
    }


    /// <summary>
    ///     Checks the updated permissions of a role for blacklisted permissions.
    /// </summary>
    private async Task CheckRolePermissionsAsync(IGuild guild, IRole role,
        IEnumerable<GuildPermission> addedPermissions, ulong executorId, GuildPermissions beforePermissions)
    {
        var guildId = guild.Id;
        var guildSettings = await GetGuildSettingsAsync(guildId);

        var blacklistedPermissions = addedPermissions
            .Where(p => guildSettings.BlacklistedPermissions.ContainsKey(p))
            .Select(p => (Permission: p, Punishment: guildSettings.BlacklistedPermissions[p]));

        if (!blacklistedPermissions.Any())
        {
            // No blacklisted permissions found, return early
            return;
        }

        var highestPunishment = blacklistedPermissions
            .Select(p => p.Punishment)
            .OrderByDescending(p => p)
            .FirstOrDefault();

        if (highestPunishment == null && guildSettings.DefaultPunishmentAction == null)
        {
            // No punishment available, return early
            return;
        }

        // Use the highest punishment if available, otherwise use the default punishment
        var punishmentToApply = highestPunishment ?? guildSettings.DefaultPunishmentAction;

        if (punishmentToApply == null)
        {
            // This should not happen given the previous check, but added for safety
            return;
        }

        var executor = await guild.GetUserAsync(executorId);
        if (executor != null && !await IsUserWhitelistedAsync(guild, executor.Id))
        {
            await role.ModifyAsync(rp => rp.Permissions = beforePermissions);

            var usersWithRole = await guild.GetUsersAsync();
            foreach (var user in usersWithRole)
            {
                if (user.RoleIds.Contains(role.Id))
                {
                    await user.RemoveRoleAsync(role);
                }
            }

            await ApplyPunishmentAsync(executor, punishmentToApply.Value, role, false, true);
        }
    }


    /// <summary>
    ///     Applies the specified punishment to the user.
    /// </summary>
    private async Task ApplyPunishmentAsync(IGuildUser? user, PunishmentAction? punishmentAction, IRole role,
        bool selfAssigned = false, bool permissionChange = false)
    {
        var guild = user.Guild;
        var actionDescription = permissionChange
            ? $"modifying prohibited permissions in role: {role.Name}"
            : $"assigning a prohibited role: {role.Name}";

        if (selfAssigned)
            actionDescription = $"self-assigning a prohibited role: {role.Name}";

        try
        {
            var dmChannel = await user.CreateDMChannelAsync();
            var action = punishmentAction switch
            {
                PunishmentAction.Warn => "warned",
                PunishmentAction.Ban => "banned",
                PunishmentAction.Kick => "kicked",
                PunishmentAction.Mute => "muted",
                PunishmentAction.Softban => "softbanned",
                PunishmentAction.Timeout => "timed out",
                PunishmentAction.RemoveRoles => "stripped of all roles",
                _ => "somehow forgiven..."
            };
            await dmChannel.SendErrorAsync(
                strings.RoleActionDm(guild.Id, action, actionDescription), botConfigService.Data);
        }
        catch
        {
            // User has DMs disabled or blocked the bot
        }

        switch (punishmentAction)
        {
            case PunishmentAction.Warn:
                await userPunishService.Warn(guild, user.Id, client.CurrentUser, actionDescription);
                break;
            case PunishmentAction.Kick:
                await user.KickAsync($"Violation: {actionDescription}");
                break;
            case PunishmentAction.Ban:
                await guild.AddBanAsync(user.Id,
                    await banPrune.GetPruneDaysAsync(guild.Id, BanPruneAction.RoleMonitor),
                    $"Violation: {actionDescription}");
                break;
            case PunishmentAction.Mute:
                await muteService.MuteUser(user, client.CurrentUser, reason: actionDescription);
                break;
            case PunishmentAction.Softban:
                await guild.AddBanAsync(user.Id,
                    await banPrune.GetPruneDaysAsync(guild.Id, BanPruneAction.SoftBan),
                    $"Violation: {actionDescription}");
                await guild.RemoveBanAsync(user);
                break;
            case PunishmentAction.RemoveRoles:
                await user.RemoveRolesAsync(user.RoleIds, new RequestOptions
                {
                    AuditLogReason = $"Violation: {actionDescription}"
                });
                break;
            case PunishmentAction.Timeout:
                await user.SetTimeOutAsync(TimeSpan.FromDays(28), new RequestOptions
                {
                    AuditLogReason = $"Violation: {actionDescription}"
                });
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(punishmentAction), punishmentAction, null);
        }
    }


    /// <summary>
    ///     Loads guild settings from the cache or database.
    /// </summary>
    private async Task<GuildSettings> GetGuildSettingsAsync(ulong guildId)
    {
        if (guildSettingsCache.TryGetValue(guildId, out var settings))
        {
            return settings;
        }

        var redisDb = dataCache.Redis.GetDatabase();

        var defaultPunishmentValue = await redisDb.StringGetAsync($"guild:{guildId}:default_punishment");
        var defaultPunishment = defaultPunishmentValue.HasValue
            ? (PunishmentAction)int.Parse(defaultPunishmentValue)
            : PunishmentAction.None;

        var blacklistedRoleIds = new Dictionary<ulong, PunishmentAction?>();
        var blacklistedRoles = await redisDb.HashGetAllAsync($"guild:{guildId}:blacklisted_roles");
        foreach (var entry in blacklistedRoles)
        {
            blacklistedRoleIds[ulong.Parse(entry.Name)] = (PunishmentAction)int.Parse(entry.Value);
        }

        var blacklistedPermissions = new Dictionary<GuildPermission, PunishmentAction?>();
        var blacklistedPerms = await redisDb.HashGetAllAsync($"guild:{guildId}:blacklisted_permissions");
        foreach (var entry in blacklistedPerms)
        {
            blacklistedPermissions[(GuildPermission)int.Parse(entry.Name)] = (PunishmentAction)int.Parse(entry.Value);
        }

        var guildSettings = new GuildSettings
        {
            GuildId = guildId,
            DefaultPunishmentAction = defaultPunishment,
            BlacklistedRoleIds = blacklistedRoleIds,
            BlacklistedPermissions = blacklistedPermissions
        };

        guildSettingsCache[guildId] = guildSettings;

        return guildSettings;
    }

    /// <summary>
    ///     Adds a user to the whitelist.
    /// </summary>
    /// <param name="guild">The Discord guild.</param>
    /// <param name="user">The user parameter.</param>
    public async Task AddWhitelistedUserAsync(IGuild guild, IGuildUser user)
    {
        await using var context = await dbFactory.CreateConnectionAsync();
        var existing = await context.WhitelistedUsers
            .FirstOrDefaultAsync(u => u.GuildId == guild.Id && u.UserId == user.Id);

        if (existing == null)
        {
            await context.InsertAsync(new WhitelistedUser
            {
                GuildId = guild.Id, UserId = user.Id
            });

            var redisDb = dataCache.Redis.GetDatabase();
            await redisDb.SetAddAsync($"guild:{guild.Id}:whitelisted_users", user.Id);
        }
    }

    /// <summary>
    ///     Removes a user from the whitelist.
    /// </summary>
    /// <param name="guild">The Discord guild.</param>
    /// <param name="user">The user parameter.</param>
    public async Task RemoveWhitelistedUserAsync(IGuild guild, IGuildUser user)
    {
        await using var context = await dbFactory.CreateConnectionAsync();
        var existing = await context.WhitelistedUsers
            .FirstOrDefaultAsync(u => u.GuildId == guild.Id && u.UserId == user.Id);

        if (existing != null)
        {
            await context.DeleteAsync(existing);

            var redisDb = dataCache.Redis.GetDatabase();
            await redisDb.SetRemoveAsync($"guild:{guild.Id}:whitelisted_users", user.Id);
        }
    }

    /// <summary>
    ///     Adds a role to the whitelist.
    /// </summary>
    /// <param name="guild">The Discord guild.</param>
    /// <param name="role">The role parameter.</param>
    public async Task AddWhitelistedRoleAsync(IGuild guild, IRole role)
    {
        await using var context = await dbFactory.CreateConnectionAsync();
        var existing = await context.WhitelistedRoles
            .FirstOrDefaultAsync(r => r.GuildId == guild.Id && r.RoleId == role.Id);

        if (existing == null)
        {
            await context.InsertAsync(new WhitelistedRole
            {
                GuildId = guild.Id, RoleId = role.Id
            });


            var redisDb = dataCache.Redis.GetDatabase();
            await redisDb.SetAddAsync($"guild:{guild.Id}:whitelisted_roles", role.Id);
        }
    }

    /// <summary>
    ///     Removes a role from the whitelist.
    /// </summary>
    /// <param name="guild">The Discord guild.</param>
    /// <param name="role">The role parameter.</param>
    public async Task RemoveWhitelistedRoleAsync(IGuild guild, IRole role)
    {
        await using var context = await dbFactory.CreateConnectionAsync();
        var existing = await context.WhitelistedRoles
            .FirstOrDefaultAsync(r => r.GuildId == guild.Id && r.RoleId == role.Id);

        if (existing != null)
        {
            await context.DeleteAsync(existing);


            var redisDb = dataCache.Redis.GetDatabase();
            await redisDb.SetRemoveAsync($"guild:{guild.Id}:whitelisted_roles", role.Id);
        }
    }

    /// <summary>
    ///     Sets the default punishment action for a guild.
    /// </summary>
    /// <param name="guild">The guild to set the default punishment for.</param>
    /// <param name="punishmentAction">The default punishment action.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task SetDefaultPunishmentAsync(IGuild guild, PunishmentAction punishmentAction)
    {
        await using var context = await dbFactory.CreateConnectionAsync();
        var existing = await context.RoleMonitoringSettings
            .FirstOrDefaultAsync(s => s.GuildId == guild.Id);

        if (existing != null)
        {
            existing.DefaultPunishmentAction = (int)punishmentAction;
        }
        else
        {
            await context.InsertAsync(new RoleMonitoringSetting
            {
                GuildId = guild.Id, DefaultPunishmentAction = (int)punishmentAction
            });
        }


        var redisDb = dataCache.Redis.GetDatabase();
        await redisDb.StringSetAsync($"guild:{guild.Id}:default_punishment", ((int)punishmentAction).ToString());

        if (guildSettingsCache.TryGetValue(guild.Id, out var settings))
        {
            settings.DefaultPunishmentAction = punishmentAction;
        }
        else
        {
            guildSettingsCache[guild.Id] = new GuildSettings
            {
                GuildId = guild.Id, DefaultPunishmentAction = punishmentAction
            };
        }
    }

    /// <summary>
    ///     Adds a role to the blacklist.
    /// </summary>
    /// <param name="guild">The Discord guild.</param>
    /// <param name="role">The role parameter.</param>
    /// <param name="punishmentAction">The punishmentAction parameter.</param>
    public async Task AddBlacklistedRoleAsync(IGuild guild, IRole role, PunishmentAction? punishmentAction)
    {
        await using var context = await dbFactory.CreateConnectionAsync();
        var existing = await context.BlacklistedRoles
            .FirstOrDefaultAsync(r => r.GuildId == guild.Id && r.RoleId == role.Id);

        if (existing != null)
        {
            throw new InvalidOperationException("role_already_blacklisted");
        }

        var current = await GetDefaultPunishmentAsync(guild.Id);

        if (current is null && punishmentAction is null)
        {
            throw new InvalidOperationException("default_punish_action_not_set");
        }

        punishmentAction ??= current;

        await context.InsertAsync(new BlacklistedRole
        {
            GuildId = guild.Id, RoleId = role.Id, PunishmentAction = (int?)punishmentAction
        });


        var redisDb = dataCache.Redis.GetDatabase();
        var punishmentValue = ((int)punishmentAction.Value).ToString();
        await redisDb.HashSetAsync($"guild:{guild.Id}:blacklisted_roles", role.Id.ToString(), punishmentValue);

        // Update in-memory cache
        if (guildSettingsCache.TryGetValue(guild.Id, out var settings))
        {
            settings.BlacklistedRoleIds[role.Id] = punishmentAction.Value;
        }
        else
        {
            guildSettingsCache[guild.Id] = new GuildSettings
            {
                GuildId = guild.Id,
                BlacklistedRoleIds = new Dictionary<ulong, PunishmentAction?>
                {
                    [role.Id] = punishmentAction.Value
                }
            };
        }
    }

    /// <summary>
    ///     Removes a role from the blacklist.
    /// </summary>
    /// <param name="guild">The Discord guild.</param>
    /// <param name="role">The role parameter.</param>
    public async Task RemoveBlacklistedRoleAsync(IGuild guild, IRole role)
    {
        await using var context = await dbFactory.CreateConnectionAsync();
        var existing = await context.BlacklistedRoles
            .FirstOrDefaultAsync(r => r.GuildId == guild.Id && r.RoleId == role.Id);

        if (existing != null)
        {
            await context.DeleteAsync(existing);


            var redisDb = dataCache.Redis.GetDatabase();
            await redisDb.HashDeleteAsync($"guild:{guild.Id}:blacklisted_roles", role.Id.ToString());

            if (guildSettingsCache.TryGetValue(guild.Id, out var settings))
            {
                settings.BlacklistedRoleIds.Remove(role.Id);
            }
        }
        else
        {
            throw new InvalidOperationException("role_not_blacklisted");
        }
    }


    /// <summary>
    ///     Adds a permission to the blacklist.
    /// </summary>
    public async Task AddBlacklistedPermissionAsync(IGuild guild, GuildPermission permission,
        PunishmentAction? punishmentAction)
    {
        await using var context = await dbFactory.CreateConnectionAsync();
        var existing = await context.BlacklistedPermissions
            .FirstOrDefaultAsync(p => p.GuildId == guild.Id && p.Permission == (ulong)permission);

        if (existing != null)
        {
            throw new InvalidOperationException("permission_already_blacklisted");
        }

        var current = await GetDefaultPunishmentAsync(guild.Id);

        if (current is null && punishmentAction is null)
        {
            throw new InvalidOperationException("default_punish_action_not_set");
        }

        punishmentAction ??= current;

        await context.InsertAsync(new BlacklistedPermission
        {
            GuildId = guild.Id, Permission = (ulong)permission, PunishmentAction = (int?)punishmentAction
        });


        var redisDb = dataCache.Redis.GetDatabase();
        var punishmentValue = ((int)punishmentAction.Value).ToString();
        await redisDb.HashSetAsync($"guild:{guild.Id}:blacklisted_permissions", ((int)permission).ToString(),
            punishmentValue);

        if (guildSettingsCache.TryGetValue(guild.Id, out var settings))
        {
            settings.BlacklistedPermissions[permission] = punishmentAction.Value;
        }
        else
        {
            guildSettingsCache[guild.Id] = new GuildSettings
            {
                GuildId = guild.Id,
                BlacklistedPermissions = new Dictionary<GuildPermission, PunishmentAction?>
                {
                    [permission] = punishmentAction.Value
                }
            };
        }
    }


    /// <summary>
    ///     Removes a permission from the blacklist.
    /// </summary>
    /// <param name="guild">The Discord guild.</param>
    /// <param name="permission">The permission parameter.</param>
    public async Task RemoveBlacklistedPermissionAsync(IGuild guild, GuildPermission permission)
    {
        await using var context = await dbFactory.CreateConnectionAsync();
        var existing = await context.BlacklistedPermissions
            .FirstOrDefaultAsync(p => p.GuildId == guild.Id && p.Permission == (ulong)permission);

        if (existing != null)
        {
            await context.DeleteAsync(existing);


            var redisDb = dataCache.Redis.GetDatabase();
            await redisDb.HashDeleteAsync($"guild:{guild.Id}:blacklisted_permissions", ((int)permission).ToString());

            if (guildSettingsCache.TryGetValue(guild.Id, out var settings))
            {
                settings.BlacklistedPermissions.Remove(permission);
            }
        }
        else
        {
            throw new InvalidOperationException("permission_not_blacklisted");
        }
    }

    private async Task<PunishmentAction?> GetDefaultPunishmentAsync(ulong guildId)
    {
        await using var context = await dbFactory.CreateConnectionAsync();

        var existing = await context.RoleMonitoringSettings
            .FirstOrDefaultAsync(s => s.GuildId == guildId);
        return (PunishmentAction?)existing?.DefaultPunishmentAction;
    }

    private class GuildSettings
    {
        public ulong GuildId { get; set; }
        public PunishmentAction? DefaultPunishmentAction { get; set; }
        public Dictionary<ulong, PunishmentAction?> BlacklistedRoleIds { get; set; } = new();
        public Dictionary<GuildPermission, PunishmentAction?> BlacklistedPermissions { get; set; } = new();
    }
}