using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Database.DbContextStuff;
using Mewdeko.Modules.Server_Management;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

/// <summary>
///     Service for managing channel commands and lockdowns, accounting for various channel types such as text, voice, and
///     forum.
/// </summary>
public class ChannelCommandService : INService, IReadyExecutor
{
    private readonly IDataCache dataCache;
    private readonly DiscordShardedClient client;
    private readonly DbContextProvider dbContext;

    private readonly ConcurrentDictionary<ulong, (ServerManagement.LockdownType, PunishmentAction?)> lockdownGuilds =
        new();

    /// <summary>
    ///     Constructs a new instance of the ChannelCommandService.
    /// </summary>
    /// <param name="dataCache">The data cache for accessing Redis.</param>
    /// <param name="handler">The event handler.</param>
    public ChannelCommandService(IDataCache dataCache, EventHandler handler, DbContextProvider dbContext,
        DiscordShardedClient client)
    {
        this.dataCache = dataCache;
        this.dbContext = dbContext;
        this.client = client;
        handler.UserJoined += HandleUserJoinDuringLockdown;
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

        lockdownGuilds[guild.Id] = (lockdownType, action);

        if (lockdownType is not (ServerManagement.LockdownType.Joins or ServerManagement.LockdownType.Full))
            return (false, lockdownType);
        var redisDb = dataCache.Redis.GetDatabase();
        await redisDb.SetAddAsync("join-blocked-guilds", guild.Id);

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
        await redisDb.SetRemoveAsync("join-blocked-guilds", guild.Id);
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
    ///     Gets the type of lockdown and action (if any) for the guild.
    /// </summary>
    /// <param name="guild">The guild to retrieve the lockdown info from.</param>
    /// <returns>A tuple containing the lockdown type and action, or null if the guild is not in lockdown.</returns>
    public (ServerManagement.LockdownType lockdownType, PunishmentAction? action)? GetLockdownInfo(IGuild guild)
    {
        return lockdownGuilds.TryGetValue(guild.Id, out var lockdownInfo) ? lockdownInfo : (default, default);
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
    ///     Handles the event when a user joins a guild during a lockdown.
    /// </summary>
    /// <param name="user">The user that has joined the guild.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task HandleUserJoinDuringLockdown(IGuildUser user)
    {
        if (lockdownGuilds.TryGetValue(user.Guild.Id, out var lockdownInfo) &&
            lockdownInfo.Item1 is ServerManagement.LockdownType.Joins or ServerManagement.LockdownType.Full)
        {
            var action = lockdownInfo.Item2;
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

    /// <summary>
    ///     Stores the original permission overrides for all roles and users in each relevant channel of the guild.
    /// </summary>
    /// <param name="guild">The guild whose channel permissions are being stored.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task StoreOriginalPermissions(IGuild guild)
    {
        await using var context = await dbContext.GetContextAsync();
        var channels = await guild.GetChannelsAsync();

        foreach (var channel in channels)
        {
            if (!IsRelevantChannel(channel)) continue;

            var permissionOverrides = channel.PermissionOverwrites;

            foreach (var overwrite in permissionOverrides)
            {
                // Store overrides for both roles and users
                var existingEntry = await context.LockdownChannelPermissions
                    .FirstOrDefaultAsync(p =>
                        p.GuildId == guild.Id && p.ChannelId == channel.Id && p.TargetId == overwrite.TargetId);

                if (existingEntry != null) continue;

                // Add new entry for each permission override
                var newPermission = new LockdownChannelPermissions
                {
                    GuildId = guild.Id,
                    ChannelId = channel.Id,
                    TargetId = overwrite.TargetId,
                    TargetType = overwrite.TargetType, // Role or User
                    AllowPermissions = GetRawPermissionValue(overwrite.Permissions.ToAllowList()),
                    DenyPermissions = GetRawPermissionValue(overwrite.Permissions.ToDenyList())
                };

                await context.LockdownChannelPermissions.AddAsync(newPermission);
            }
        }

        await context.SaveChangesAsync();
    }

    /// <summary>
    ///     Removes all permission overrides from all channels in the guild.
    /// </summary>
    /// <param name="guild">The guild whose permissions are being removed.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task RemovePermissions(IGuild guild)
    {
        var channels = await guild.GetChannelsAsync();

        foreach (var channel in channels)
        {
            if (!IsRelevantChannel(channel)) continue;

            var permissionOverrides = channel.PermissionOverwrites;

            foreach (var overwrite in permissionOverrides)
            {
                // Remove permission overrides for both roles and users
                if (overwrite.TargetType == PermissionTarget.Role)
                {
                    var role = guild.GetRole(overwrite.TargetId);
                    if (role != null)
                    {
                        await channel.RemovePermissionOverwriteAsync(role).ConfigureAwait(false);
                    }
                }
                else if (overwrite.TargetType == PermissionTarget.User)
                {
                    var user = await guild.GetUserAsync(overwrite.TargetId);
                    if (user != null)
                    {
                        await channel.RemovePermissionOverwriteAsync(user).ConfigureAwait(false);
                    }
                }
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
        await StoreOriginalPermissions(guild); // Store all permissions first
        await RemovePermissions(guild); // Remove all permissions from the channels

        var everyoneRole = guild.EveryoneRole;
        var channels = await guild.GetChannelsAsync();

        foreach (var channel in channels)
        {
            if (!IsRelevantChannel(channel)) continue;

            OverwritePermissions lockdownPerms;

            if (channel is IVoiceChannel)
            {
                // For voice channels, deny "Connect" and "Send Messages"
                lockdownPerms = new OverwritePermissions(connect: PermValue.Deny, sendMessages: PermValue.Deny);
            }
            else if (channel is IForumChannel)
            {
                // For forum channels, deny "Send Messages" and "Create Threads"
                lockdownPerms = new OverwritePermissions(sendMessages: PermValue.Deny,
                    createPublicThreads: PermValue.Deny, createPrivateThreads: PermValue.Deny);
            }
            else
            {
                // For text channels, deny "Send Messages" and "Create Threads"
                lockdownPerms = new OverwritePermissions(sendMessages: PermValue.Deny,
                    createPublicThreads: PermValue.Deny, createPrivateThreads: PermValue.Deny);
            }

            // Apply lockdown to the @everyone role
            await ((IGuildChannel)channel).AddPermissionOverwriteAsync(everyoneRole, lockdownPerms)
                .ConfigureAwait(false);
        }
    }


    /// <summary>
    ///     Restores the original permissions for all roles and users in each relevant channel after the lockdown is lifted.
    /// </summary>
    /// <param name="guild">The guild where the lockdown is being lifted.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task RestoreOriginalPermissions(IGuild guild)
    {
        await using var context = await dbContext.GetContextAsync();
        var channels = await guild.GetChannelsAsync();

        foreach (var channel in channels)
        {
            if (!IsRelevantChannel(channel)) continue;

            var storedPermissions = await context.LockdownChannelPermissions
                .Where(p => p.GuildId == guild.Id && p.ChannelId == channel.Id)
                .ToListAsync();

            foreach (var storedPerm in storedPermissions)
            {
                if (storedPerm.TargetType == PermissionTarget.Role)
                {
                    var role = guild.GetRole(storedPerm.TargetId);
                    if (role != null)
                    {
                        var permissions =
                            new OverwritePermissions(storedPerm.AllowPermissions, storedPerm.DenyPermissions);
                        await channel.AddPermissionOverwriteAsync(role, permissions).ConfigureAwait(false);
                    }
                }
                else if (storedPerm.TargetType == PermissionTarget.User)
                {
                    var user = await guild.GetUserAsync(storedPerm.TargetId);
                    if (user != null)
                    {
                        var permissions =
                            new OverwritePermissions(storedPerm.AllowPermissions, storedPerm.DenyPermissions);
                        await channel.AddPermissionOverwriteAsync(user, permissions).ConfigureAwait(false);
                    }
                }
            }

            // Remove the restored permissions from the database
            context.LockdownChannelPermissions.RemoveRange(storedPermissions);
        }

        await context.SaveChangesAsync();
    }


    /// <summary>
    ///     Determines if the channel is relevant for lockdown.
    /// </summary>
    private static bool IsRelevantChannel(IGuildChannel channel)
    {
        return channel is ITextChannel or IVoiceChannel or IForumChannel;
    }

    /// <summary>
    ///     Called when the bot is ready. Updates the list of join-blocked guilds from Redis and checks the database for
    ///     additional lockdowns.
    ///     Determines whether the guild is in Joins, Readonly, or Full lockdown based on Redis and database information.
    /// </summary>
    public async Task OnReadyAsync()
    {
        var redisDb = dataCache.Redis.GetDatabase();
        var redisJoinBlockedGuilds = await redisDb.SetMembersAsync("join-blocked-guilds").ConfigureAwait(false);

        await using var context = await dbContext.GetContextAsync();

        // Fetch all guilds from the database that have lockdown channel permissions stored
        var dbLockdownGuilds = await context.LockdownChannelPermissions
            .Select(p => p.GuildId)
            .Distinct()
            .ToListAsync();

        foreach (var guild in client.Guilds)
        {
            var guildId = guild.Id;
            var isInRedis = redisJoinBlockedGuilds.Any(g => g == (RedisValue)guildId.ToString());
            var isInDb = dbLockdownGuilds.Contains(guildId);

            // If the guild is only in Redis, it's in Joins lockdown
            if (isInRedis && !isInDb)
            {
                lockdownGuilds[guildId] = (ServerManagement.LockdownType.Joins, null);
            }
            // If the guild is only in the database, it's in Readonly lockdown
            else if (!isInRedis && isInDb)
            {
                lockdownGuilds[guildId] = (ServerManagement.LockdownType.Readonly, null);
            }
            // If the guild is in both Redis and the database, it's in Full lockdown
            else if (isInRedis && isInDb)
            {
                lockdownGuilds[guildId] = (ServerManagement.LockdownType.Full, null);
            }
        }

        // If there are guilds in Redis but not in lockdownGuilds (not recognized during the loop), remove them from Redis
        foreach (var guildId in redisJoinBlockedGuilds.Select(g => (ulong)g))
        {
            if (!lockdownGuilds.ContainsKey(guildId))
            {
                await redisDb.SetRemoveAsync("join-blocked-guilds", guildId).ConfigureAwait(false);
            }
        }
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