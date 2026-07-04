using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Database.DbContextStuff;
using Mewdeko.Modules.Administration.Common;
using StackExchange.Redis;

namespace Mewdeko.Modules.Server_Management.Services;

/// <summary>
///     Service for managing channel commands and lockdowns, accounting for various channel types such as text, voice, and
///     forum.
/// </summary>
public class ChannelCommandService : INService, IReadyExecutor
{
    private const string JoinBlockedGuildsKey = "join-blocked-guilds";
    private const string JoinBlockedActionsKey = "join-blocked-guild-actions";
    private readonly DiscordShardedClient client;
    private readonly IDataCache dataCache;
    private readonly IDataConnectionFactory dbFactory;

    private readonly ConcurrentDictionary<ulong, (ServerManagement.LockdownType, PunishmentAction?)> lockdownGuilds =
        new();

    /// <summary>
    ///     Constructs a new instance of the ChannelCommandService.
    /// </summary>
    /// <param name="dataCache">The data cache for accessing Redis.</param>
    /// <param name="handler">The event handler.</param>
    /// <param name="dbFactory">The databse connection provider</param>
    /// <param name="client">The discord client</param>
    public ChannelCommandService(IDataCache dataCache, EventHandler handler, IDataConnectionFactory dbFactory,
        DiscordShardedClient client)
    {
        this.dataCache = dataCache;
        this.dbFactory = dbFactory;
        this.client = client;
        handler.Subscribe("UserJoined", "ChannelCommandService", HandleUserJoinDuringLockdown);
    }

    /// <summary>
    ///     Called when the bot is ready. Updates the list of join-blocked guilds from the database and Redis cache, then
    ///     checks the database for additional readonly/full lockdowns.
    ///     Determines whether the guild is in Joins, Readonly, or Full lockdown based on stored join settings and
    ///     channel permission snapshots.
    /// </summary>
    public async Task OnReadyAsync()
    {
        var redisDb = dataCache.Redis.GetDatabase();
        var redisJoinBlockedGuilds = await redisDb.SetMembersAsync(JoinBlockedGuildsKey).ConfigureAwait(false);

        await using var context = await dbFactory.CreateConnectionAsync();

        var dbJoinLockdownSettings = await context.LockdownJoinSettings.ToListAsync().ConfigureAwait(false);
        var dbJoinLockdowns = dbJoinLockdownSettings.ToDictionary(x => x.GuildId,
            x => NormalizeJoinLockdownAction((PunishmentAction)x.PunishmentAction));

        // Fetch all guilds from the database that have lockdown channel permissions stored
        var dbLockdownGuilds = await context.LockdownChannelPermissions
            .Select(p => p.GuildId)
            .Distinct()
            .ToListAsync();

        foreach (var guild in client.Guilds)
        {
            var guildId = guild.Id;
            var isInRedis = redisJoinBlockedGuilds.Any(g => g == (RedisValue)guildId.ToString());
            var hasJoinLockdown = dbJoinLockdowns.TryGetValue(guildId, out var dbJoinAction);
            var hasChannelLockdown = dbLockdownGuilds.Contains(guildId);
            if (!isInRedis && !hasJoinLockdown && !hasChannelLockdown)
                continue;

            PunishmentAction? action = hasJoinLockdown
                ? dbJoinAction
                : isInRedis
                    ? await GetStoredJoinLockdownAction(redisDb, guildId).ConfigureAwait(false)
                    : null;

            if (action is not null)
            {
                await PersistJoinLockdown(context, guildId, action.Value).ConfigureAwait(false);
                await CacheJoinLockdown(redisDb, guildId, action.Value).ConfigureAwait(false);
            }

            lockdownGuilds[guildId] = (action is not null, hasChannelLockdown) switch
            {
                (true, true) => (ServerManagement.LockdownType.Full, action),
                (true, false) => (ServerManagement.LockdownType.Joins, action),
                (false, true) => (ServerManagement.LockdownType.Readonly, null),
                _ => lockdownGuilds[guildId]
            };
        }

        if (redisJoinBlockedGuilds.Length == 0)
            return;

        // If there are guilds in Redis but not in lockdownGuilds (not recognized during the loop), remove them from Redis
        foreach (var guildId in redisJoinBlockedGuilds.Select(g => (ulong)g))
        {
            if (!lockdownGuilds.ContainsKey(guildId))
            {
                await redisDb.SetRemoveAsync(JoinBlockedGuildsKey, guildId).ConfigureAwait(false);
                await redisDb.HashDeleteAsync(JoinBlockedActionsKey, guildId).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    ///     Locks down the guild based on the specified type.
    /// </summary>
    /// <param name="guild">The guild to lockdown.</param>
    /// <param name="lockdownType">The type of lockdown (Joins, Readonly, Full).</param>
    /// <param name="action">Optional: Action to apply to users who try to join (Kick/Ban).</param>
    public async Task<(bool, ServerManagement.LockdownType)> LockdownGuild(IGuild guild,
        ServerManagement.LockdownType lockdownType, PunishmentAction? action = null)
    {
        if (lockdownGuilds.TryGetValue(guild.Id, out var lockdownGuild))
            return (true, lockdownGuild.Item1);

        if (lockdownType is ServerManagement.LockdownType.Joins or ServerManagement.LockdownType.Full)
            action = NormalizeJoinLockdownAction(action);

        lockdownGuilds[guild.Id] = (lockdownType, action);

        if (lockdownType is not (ServerManagement.LockdownType.Joins or ServerManagement.LockdownType.Full))
            return (false, lockdownType);

        await using var context = await dbFactory.CreateConnectionAsync();
        await PersistJoinLockdown(context, guild.Id, action.Value).ConfigureAwait(false);

        var redisDb = dataCache.Redis.GetDatabase();
        await CacheJoinLockdown(redisDb, guild.Id, action.Value).ConfigureAwait(false);

        return (false, lockdownType);
    }

    /// <summary>
    ///     Lifts the lockdown for the guild.
    /// </summary>
    /// <param name="guild">The guild to lift the lockdown for.</param>
    public async Task LiftLockdown(IGuild guild)
    {
        if (!lockdownGuilds.TryRemove(guild.Id, out var lockdownInfo) ||
            lockdownInfo.Item1 != ServerManagement.LockdownType.Joins &&
            lockdownInfo.Item1 != ServerManagement.LockdownType.Full) return;

        var redisDb = dataCache.Redis.GetDatabase();
        await redisDb.SetRemoveAsync(JoinBlockedGuildsKey, guild.Id);
        await redisDb.HashDeleteAsync(JoinBlockedActionsKey, guild.Id);

        await using var context = await dbFactory.CreateConnectionAsync();
        await context.LockdownJoinSettings
            .Where(x => x.GuildId == guild.Id)
            .DeleteAsync()
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Checks if the guild is in lockdown.
    /// </summary>
    /// <param name="guild">The guild to check.</param>
    /// <returns>True if the guild is in lockdown, otherwise false.</returns>
    public bool IsGuildInLockdown(IGuild guild)
    {
        return lockdownGuilds.ContainsKey(guild.Id);
    }

    /// <summary>
    ///     Checks if the guild is in the specified lockdown type.
    /// </summary>
    /// <param name="guild">The guild to check.</param>
    /// <param name="lockdownType">The lockdown type to check for.</param>
    /// <returns>True if the guild is in the specified lockdown type, otherwise false.</returns>
    public bool IsGuildInLockdown(IGuild guild, ServerManagement.LockdownType lockdownType)
    {
        return lockdownGuilds.TryGetValue(guild.Id, out var info) &&
               (info.Item1 == lockdownType || info.Item1 == ServerManagement.LockdownType.Full);
    }

    /// <summary>
    ///     Gets the type of lockdown and action (if any) for the guild.
    /// </summary>
    /// <param name="guild">The guild to retrieve the lockdown info from.</param>
    /// <returns>A tuple containing the lockdown type and action, or null if the guild is not in lockdown.</returns>
    public (ServerManagement.LockdownType lockdownType, PunishmentAction? action)? GetLockdownInfo(IGuild guild)
    {
        return lockdownGuilds.TryGetValue(guild.Id, out var lockdownInfo) ? lockdownInfo : null;
    }

    /// <summary>
    ///     Checks if the bot has the necessary permissions to modify the @everyone role's permissions across the guild.
    /// </summary>
    /// <param name="guild">The guild where the lockdown will be applied.</param>
    /// <param name="overrideCheck">Whether to override permission failures.</param>
    /// <returns>A list of missing permissions, if any.</returns>
    public async Task<List<string>> CheckLockdownPermissions(IGuild guild, bool overrideCheck)
    {
        var missingPermissions = new List<string>();
        var botUser = await guild.GetCurrentUserAsync().ConfigureAwait(false);

        if (!botUser.GuildPermissions.ManageRoles)
        {
            missingPermissions.Add("Manage Roles (to modify @everyone permissions)");
        }

        if (!botUser.GuildPermissions.ManageChannels)
        {
            missingPermissions.Add("Manage Channels (to modify @everyone permissions in channels)");
        }

        return missingPermissions;
    }

    /// <summary>
    ///     Checks if the bot can apply the requested join lockdown action to newly joined users.
    /// </summary>
    /// <param name="guild">The guild where the join lockdown will run.</param>
    /// <param name="action">The punishment action to apply to new joins.</param>
    /// <returns>A list of missing permissions, if any.</returns>
    public async Task<List<string>> CheckJoinLockdownActionPermissions(IGuild guild, PunishmentAction action)
    {
        var missingPermissions = new List<string>();
        var botUser = await guild.GetCurrentUserAsync().ConfigureAwait(false);

        switch (action)
        {
            case PunishmentAction.Kick when !botUser.GuildPermissions.KickMembers:
                missingPermissions.Add("Kick Members (to kick users who join during lockdown)");
                break;
            case PunishmentAction.Ban when !botUser.GuildPermissions.BanMembers:
                missingPermissions.Add("Ban Members (to ban users who join during lockdown)");
                break;
        }

        return missingPermissions;
    }

    /// <summary>
    ///     Handles the event when a user joins a guild during a lockdown.
    /// </summary>
    /// <param name="user">The user that has joined the guild.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task HandleUserJoinDuringLockdown(IGuildUser user)
    {
        if (lockdownGuilds.TryGetValue(user.Guild.Id, out var lockdownInfo) &&
            lockdownInfo.Item1 is ServerManagement.LockdownType.Joins or ServerManagement.LockdownType.Full)
        {
            var action = NormalizeJoinLockdownAction(lockdownInfo.Item2);
            switch (action)
            {
                case PunishmentAction.Kick:
                    await user.KickAsync("Server is in lockdown").ConfigureAwait(false);
                    break;
                case PunishmentAction.Ban:
                    await user.Guild.AddBanAsync(user, 0, "Server is in lockdown").ConfigureAwait(false);
                    break;
            }
        }
    }

    private static PunishmentAction NormalizeJoinLockdownAction(PunishmentAction? action)
    {
        return action is PunishmentAction.Kick or PunishmentAction.Ban ? action.Value : PunishmentAction.Ban;
    }

    private static async Task<PunishmentAction> GetStoredJoinLockdownAction(IDatabase redisDb, ulong guildId)
    {
        var storedAction = await redisDb.HashGetAsync(JoinBlockedActionsKey, guildId).ConfigureAwait(false);
        return Enum.TryParse<PunishmentAction>(storedAction, true, out var action)
            ? NormalizeJoinLockdownAction(action)
            : PunishmentAction.Ban;
    }

    private static async Task CacheJoinLockdown(IDatabase redisDb, ulong guildId, PunishmentAction action)
    {
        await redisDb.SetAddAsync(JoinBlockedGuildsKey, guildId).ConfigureAwait(false);
        await redisDb.HashSetAsync(JoinBlockedActionsKey, guildId, action.ToString()).ConfigureAwait(false);
    }

    private static async Task PersistJoinLockdown(MewdekoDb context, ulong guildId, PunishmentAction action)
    {
        var existing = await context.LockdownJoinSettings
            .FirstOrDefaultAsync(x => x.GuildId == guildId)
            .ConfigureAwait(false);

        if (existing is null)
        {
            await context.InsertAsync(new LockdownJoinSetting
            {
                GuildId = guildId,
                PunishmentAction = (int)action,
                DateAdded = DateTime.UtcNow,
                DateUpdated = DateTime.UtcNow
            }).ConfigureAwait(false);
            return;
        }

        existing.PunishmentAction = (int)action;
        existing.DateUpdated = DateTime.UtcNow;
        await context.UpdateAsync(existing).ConfigureAwait(false);
    }

    /// <summary>
    ///     Stores the original permission overrides for all roles and users in each relevant channel of the guild.
    /// </summary>
    /// <param name="guild">The guild whose channel permissions are being stored.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task StoreOriginalPermissions(IGuild guild)
    {
        await using var context = await dbFactory.CreateConnectionAsync();
        var channels = await guild.GetChannelsAsync();

        var existingPermissions = await context.LockdownChannelPermissions
            .Where(p => p.GuildId == guild.Id)
            .ToListAsync();

        var newPermissions = (from channel in channels
            where IsRelevantChannel(channel)
            let permissionOverwrites = channel.PermissionOverwrites
            from overwrite in permissionOverwrites
            let existingEntry =
                existingPermissions.FirstOrDefault(p => p.ChannelId == channel.Id && p.TargetId == overwrite.TargetId)
            where existingEntry == null
            select new LockdownChannelPermission
            {
                GuildId = guild.Id,
                ChannelId = channel.Id,
                TargetId = overwrite.TargetId,
                TargetType = (int)overwrite.TargetType, // Role or User
                AllowPermissions = GetRawPermissionValue(overwrite.Permissions.ToAllowList()),
                DenyPermissions = GetRawPermissionValue(overwrite.Permissions.ToDenyList())
            }).ToList();

        // Add all new permissions in one batch
        if (newPermissions.Count != 0)
        {
            await context.InsertAsync(newPermissions);
        }
    }

    /// <summary>
    ///     Removes all permission overrides from all channels in the guild.
    /// </summary>
    /// <param name="guild">The guild whose permissions are being removed.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private static async Task RemovePermissions(IGuild guild)
    {
        var channels = await guild.GetChannelsAsync();

        foreach (var channel in channels)
        {
            if (!IsRelevantChannel(channel)) continue;

            var permissionOverrides = channel.PermissionOverwrites.Where(x => x.TargetId == guild.EveryoneRole.Id);

            if (permissionOverrides.Any())
            {
                await channel.ModifyAsync(x =>
                    x.PermissionOverwrites = new Optional<IEnumerable<Overwrite>>(permissionOverrides));
            }
        }
    }


    /// <summary>
    ///     Applies a lockdown to the guild by first storing all permissions, removing them, and then restricting the @everyone
    ///     role.
    /// </summary>
    /// <param name="guild">The guild to apply the lockdown to.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task ApplyLockdown(IGuild guild)
    {
        await StoreOriginalPermissions(guild);
        await RemovePermissions(guild);

        var everyoneRole = guild.EveryoneRole;
        var channels = await guild.GetChannelsAsync();

        await using var context = await dbFactory.CreateConnectionAsync();

        var relevantChannels = channels.Where(IsRelevantChannel).ToList();
        var channelPermissions = new List<(IGuildChannel Channel, OverwritePermissions Permissions)>();

        foreach (var channel in relevantChannels)
        {
            var storedPerm = await context.LockdownChannelPermissions.FirstOrDefaultAsync(p =>
                p.GuildId == guild.Id && p.ChannelId == channel.Id && p.TargetId == everyoneRole.Id &&
                p.TargetType == (int)PermissionTarget.Role);

            var existingPerms = storedPerm != null
                ? new OverwritePermissions(storedPerm.AllowPermissions, storedPerm.DenyPermissions)
                : OverwritePermissions.InheritAll;

            var lockdownPerms = channel switch
            {
                IVoiceChannel => existingPerms.Modify(connect: PermValue.Deny, speak: PermValue.Deny,
                    sendMessages: PermValue.Deny, sendMessagesInThreads: PermValue.Deny),
                IForumChannel => existingPerms.Modify(sendMessagesInThreads: PermValue.Deny,
                    createPublicThreads: PermValue.Deny,
                    createPrivateThreads: PermValue.Deny,
                    sendMessages: PermValue.Allow),
                _ => existingPerms.Modify(sendMessages: PermValue.Deny, createPublicThreads: PermValue.Deny,
                    createPrivateThreads: PermValue.Deny, sendMessagesInThreads: PermValue.Deny)
            };

            channelPermissions.Add((channel, lockdownPerms));
        }

        var groupedChannels = channelPermissions.GroupBy(x => x.Channel.GetType());

        foreach (var group in groupedChannels)
        {
            if (group.Key == typeof(SocketTextChannel))
            {
                var textChannels = group.Select(x => x.Channel).Cast<ITextChannel>().ToList();
                await ModifyTextChannelsAsync(textChannels, everyoneRole, group.Select(x => x.Permissions));
            }
            else if (group.Key == typeof(SocketVoiceChannel))
            {
                var voiceChannels = group.Select(x => x.Channel).Cast<IVoiceChannel>().ToList();
                await ModifyVoiceChannelsAsync(voiceChannels, everyoneRole, group.Select(x => x.Permissions));
            }
            else if (group.Key == typeof(SocketForumChannel))
            {
                var forumChannels = group.Select(x => x.Channel).Cast<IForumChannel>().ToList();
                await ModifyForumChannelsAsync(forumChannels, everyoneRole, group.Select(x => x.Permissions));
            }
        }
    }

    private static async Task ModifyTextChannelsAsync(List<ITextChannel> channels, IRole everyoneRole,
        IEnumerable<OverwritePermissions> permissions)
    {
        await Task.WhenAll(channels.Select((channel, index) =>
            channel.ModifyAsync(x =>
            {
                x.PermissionOverwrites = new Optional<IEnumerable<Overwrite>>(
                    [
                        new Overwrite(everyoneRole.Id, PermissionTarget.Role, permissions.ElementAt(index))
                    ]
                );
            })
        ));
    }

    private static async Task ModifyForumChannelsAsync(List<IForumChannel> channels, IRole everyoneRole,
        IEnumerable<OverwritePermissions> permissions)
    {
        await Task.WhenAll(channels.Select((channel, index) =>
            channel.ModifyAsync(x =>
            {
                x.PermissionOverwrites = new Optional<IEnumerable<Overwrite>>(
                    [
                        new Overwrite(everyoneRole.Id, PermissionTarget.Role, permissions.ElementAt(index))
                    ]
                );
            })
        ));
    }

    private static async Task ModifyVoiceChannelsAsync(List<IVoiceChannel> channels, IRole everyoneRole,
        IEnumerable<OverwritePermissions> permissions)
    {
        await Task.WhenAll(channels.Select((channel, index) =>
            channel.ModifyAsync(x =>
            {
                x.PermissionOverwrites = new Optional<IEnumerable<Overwrite>>(
                    [
                        new Overwrite(everyoneRole.Id, PermissionTarget.Role, permissions.ElementAt(index))
                    ]
                );
            })
        ));
    }


    /// <summary>
    ///     Restores the original permissions for all roles and users in each relevant channel after the lockdown is lifted.
    /// </summary>
    /// <param name="guild">The guild where the lockdown is being lifted.</param>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task RestoreOriginalPermissions(IGuild guild)
    {
        await using var context = await dbFactory.CreateConnectionAsync();
        var channels = await guild.GetChannelsAsync();

        var relevantChannels = channels.Where(IsRelevantChannel).ToList();

        var guildRoleIds = guild.Roles.Select(r => r.Id).ToHashSet();

        var guildUserIds = (await guild.GetUsersAsync()).Select(u => u.Id).ToHashSet();

        var storedPermissionsByChannel = await context.LockdownChannelPermissions
            .Where(p => p.GuildId == guild.Id && relevantChannels.Select(c => c.Id).Contains(p.ChannelId))
            .GroupBy(p => p.ChannelId)
            .ToDictionaryAsync(g => g.Key, g => g.ToList());

        foreach (var channel in relevantChannels)
        {
            if (!storedPermissionsByChannel.TryGetValue(channel.Id, out var storedPermissions))
                continue;

            var overwrites = new List<Overwrite>();

            foreach (var storedPerm in storedPermissions)
            {
                var permissions = new OverwritePermissions(storedPerm.AllowPermissions, storedPerm.DenyPermissions);

                switch ((PermissionTarget)storedPerm.TargetType)
                {
                    case PermissionTarget.Role when guildRoleIds.Contains(storedPerm.TargetId):
                        overwrites.Add(new Overwrite(storedPerm.TargetId, PermissionTarget.Role, permissions));
                        break;

                    case PermissionTarget.User when guildUserIds.Contains(storedPerm.TargetId):
                        overwrites.Add(new Overwrite(storedPerm.TargetId, PermissionTarget.User, permissions));
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            await channel.ModifyAsync(x => x.PermissionOverwrites = new Optional<IEnumerable<Overwrite>>(overwrites));

            await context.DeleteAsync(storedPermissions);
        }
    }


    /// <summary>
    ///     Determines if the channel is relevant for lockdown.
    /// </summary>
    private static bool IsRelevantChannel(IGuildChannel channel)
    {
        return channel is (ITextChannel or IVoiceChannel or IForumChannel) and not IThreadChannel;
    }


    /// <summary>
    ///     Computes the raw value of a set of channel permissions by aggregating their bitwise representations.
    /// </summary>
    /// <param name="permissions">A collection of channel permissions.</param>
    /// <returns>The aggregated raw permission value.</returns>
    private static ulong GetRawPermissionValue(IEnumerable<ChannelPermission> permissions)
    {
        return permissions.Aggregate<ChannelPermission, ulong>(0,
            (current, permission) => current | (ulong)permission);
    }
}