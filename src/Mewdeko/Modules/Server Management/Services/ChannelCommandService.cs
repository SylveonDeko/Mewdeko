using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Database.DbContextStuff;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace Mewdeko.Modules.Server_Management.Services;

/// <summary>
///     Service for managing channel commands and lockdowns, accounting for various channel types such as text, voice, and
///     forum.
/// </summary>
public class ChannelCommandService : INService, IReadyExecutor
{
    private readonly DiscordShardedClient client;
    private readonly IDataCache dataCache;
    private readonly DbContextProvider dbContext;

    private readonly ConcurrentDictionary<ulong, (ServerManagement.LockdownType, PunishmentAction?)> lockdownGuilds =
        new();

    /// <summary>
    ///     Constructs a new instance of the ChannelCommandService.
    /// </summary>
    /// <param name="dataCache">The data cache for accessing Redis.</param>
    /// <param name="handler">The event handler.</param>
    /// <param name="dbContext">The databse connection provider</param>
    /// <param name="client">The discord client</param>
    public ChannelCommandService(IDataCache dataCache, EventHandler handler, DbContextProvider dbContext,
        DiscordShardedClient client)
    {
        this.dataCache = dataCache;
        this.dbContext = dbContext;
        this.client = client;
        handler.UserJoined += HandleUserJoinDuringLockdown;
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

            lockdownGuilds[guildId] = isInRedis switch
            {
                // If the guild is only in Redis, it's in Joins lockdown
                true when !isInDb => (ServerManagement.LockdownType.Joins, null),
                // If the guild is only in the database, it's in Readonly lockdown
                false when isInDb => (ServerManagement.LockdownType.Readonly, null),
                // If the guild is in both Redis and the database, it's in Full lockdown
                true when isInDb => (ServerManagement.LockdownType.Full, null),
                _ => lockdownGuilds[guildId]
            };
        }

        if (redisJoinBlockedGuilds.Length==0)
            return;

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
            select new LockdownChannelPermissions
            {
                GuildId = guild.Id,
                ChannelId = channel.Id,
                TargetId = overwrite.TargetId,
                TargetType = overwrite.TargetType, // Role or User
                AllowPermissions = GetRawPermissionValue(overwrite.Permissions.ToAllowList()),
                DenyPermissions = GetRawPermissionValue(overwrite.Permissions.ToDenyList())
            }).ToList();

        // Add all new permissions in one batch
        if (newPermissions.Count != 0)
        {
            await context.LockdownChannelPermissions.AddRangeAsync(newPermissions);
            await context.SaveChangesAsync();
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

        await using var context = await dbContext.GetContextAsync();

        var relevantChannels = channels.Where(IsRelevantChannel).ToList();
        var channelPermissions = new List<(IGuildChannel Channel, OverwritePermissions Permissions)>();

        foreach (var channel in relevantChannels)
        {
            var storedPerm = await context.LockdownChannelPermissions.FirstOrDefaultAsync(p =>
                p.GuildId == guild.Id && p.ChannelId == channel.Id && p.TargetId == everyoneRole.Id &&
                p.TargetType == PermissionTarget.Role);

            var existingPerms = storedPerm != null
                ? new OverwritePermissions(storedPerm.AllowPermissions, storedPerm.DenyPermissions)
                : OverwritePermissions.InheritAll;

            var lockdownPerms = channel switch
            {
                IVoiceChannel => existingPerms.Modify(connect: PermValue.Deny, speak: PermValue.Deny, sendMessages: PermValue.Deny, sendMessagesInThreads: PermValue.Deny),
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
        await using var context = await dbContext.GetContextAsync();
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

                switch (storedPerm.TargetType)
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

            context.LockdownChannelPermissions.RemoveRange(storedPermissions);
        }

        await context.SaveChangesAsync();
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