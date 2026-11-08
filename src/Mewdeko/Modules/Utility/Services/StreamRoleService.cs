using System.Net;
using DataModel;
using Discord.Net;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Common.TypeReaders;
using Mewdeko.Modules.Utility.Common;
using Mewdeko.Modules.Utility.Common.Exceptions;

namespace Mewdeko.Modules.Utility.Services;

/// <summary>
///     Manages stream role assignments based on user streaming status and additional configurable conditions within
///     guilds.
/// </summary>
public class StreamRoleService : INService, IUnloadableService, IReadyExecutor
{
    private readonly DiscordShardedClient client;
    private readonly IDataConnectionFactory dbFactory;
    private readonly EventHandler eventHandler;
    private readonly ILogger<StreamRoleService> logger;
    private readonly GuildOwnershipService ownership;

    /// <summary>
    ///     Initializes a new instance of the <see cref="StreamRoleService" />.
    /// </summary>
    /// <param name="client">The Discord client used to access guild and user information.</param>
    /// <param name="dbFactory">The database service for storing and retrieving stream role settings.</param>
    /// <param name="eventHandler">Event handler for capturing and responding to guild member updates.</param>
    /// <param name="logger">The logger instance for structured logging.</param>
    /// <param name="ownership">The guild ownership service.</param>
    public StreamRoleService(DiscordShardedClient client, IDataConnectionFactory dbFactory, EventHandler eventHandler,
        ILogger<StreamRoleService> logger, GuildOwnershipService ownership)
    {
        this.ownership = ownership;
        this.client = client;
        this.dbFactory = dbFactory;
        this.eventHandler = eventHandler;
        this.logger = logger;

        eventHandler.Subscribe("GuildMemberUpdated", "StreamRoleService", Client_GuildMemberUpdated);
    }

    /// <inheritdoc />
    public async Task OnReadyAsync()
    {
        try
        {
            foreach (var i in client.Guilds.Where(g => ownership.IsMine(g.Id)))
            {
                await RescanUsers(i);
            }
        }
        catch
        {
            // ignored
        }
    }

    /// <summary>
    ///     Unloads the service, detaching event handlers to stop listening to guild member updates.
    /// </summary>
    public Task Unload()
    {
        eventHandler.Unsubscribe("GuildMemberUpdated", "StreamRoleService", Client_GuildMemberUpdated);
        return Task.CompletedTask;
    }


    private async Task Client_GuildMemberUpdated(Cacheable<SocketGuildUser, ulong> cacheable, SocketGuildUser after)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var streamRole = await db.StreamRoleSettings
            .LoadWithAsTable(srs => srs.StreamRoleWhitelistedUsers)
            .LoadWithAsTable(srs => srs.StreamRoleBlacklistedUsers)
            .FirstOrDefaultAsync(srs => srs.GuildId == after.Guild.Id);

        if (streamRole is not { Enabled: true })
            return;

        await RescanUser(after, streamRole);
    }

    /// <summary>
    ///     Adds or removes a user to/from a whitelist or blacklist for stream role management, and rescans users if
    ///     successful.
    /// </summary>
    /// <param name="listType">Specifies whether to modify the whitelist or blacklist.</param>
    /// <param name="guild">The guild where the action is taking place.</param>
    /// <param name="action">Specifies whether to add or remove the user from the list.</param>
    /// <param name="userId">The ID of the user to add or remove.</param>
    /// <param name="userName">The name of the user to add or remove.</param>
    /// <returns>A task that represents the asynchronous operation, containing a boolean indicating the success of the action.</returns>
    public async Task<bool> ApplyListAction(StreamRoleListType listType, IGuild guild, AddRemove action,
        ulong userId, string userName)
    {
        userName.ThrowIfNull(nameof(userName));
        var guildId = guild.Id;
        var success = false;

        await using var db = await dbFactory.CreateConnectionAsync();

        // Get StreamRole settings directly with LinqToDB
        var StreamRoleSetting = await db.StreamRoleSettings
            .LoadWithAsTable(srs => srs.StreamRoleWhitelistedUsers)
            .LoadWithAsTable(srs => srs.StreamRoleBlacklistedUsers)
            .FirstOrDefaultAsync(srs => srs.GuildId == guildId);

        if (StreamRoleSetting == null)
        {
            StreamRoleSetting = new StreamRoleSetting
            {
                GuildId = guildId
            };
            await db.InsertAsync(StreamRoleSetting);

            // After inserting the new settings, reload it with relationships
            StreamRoleSetting = await db.StreamRoleSettings
                .LoadWithAsTable(srs => srs.StreamRoleWhitelistedUsers)
                .LoadWithAsTable(srs => srs.StreamRoleBlacklistedUsers)
                .FirstOrDefaultAsync(srs => srs.GuildId == guildId);
        }

        if (listType == StreamRoleListType.Whitelist)
        {
            if (action == AddRemove.Rem)
            {
                var toDelete = StreamRoleSetting.StreamRoleWhitelistedUsers.FirstOrDefault(x => x.UserId == userId);
                if (toDelete != null)
                {
                    // Delete using LinqToDB
                    await db.StreamRoleWhitelistedUsers
                        .Where(x => x.UserId == userId && x.StreamRoleSettingsId == StreamRoleSetting.Id)
                        .DeleteAsync();

                    success = true;
                }
            }
            else
            {
                var newUser = new StreamRoleWhitelistedUser
                {
                    UserId = userId, Username = userName, StreamRoleSettingsId = StreamRoleSetting.Id
                };

                // Insert using LinqToDB
                await db.InsertAsync(newUser);
                success = true;
            }
        }
        else
        {
            if (action == AddRemove.Rem)
            {
                var toRemove = StreamRoleSetting.StreamRoleBlacklistedUsers.FirstOrDefault(x => x.UserId == userId);
                if (toRemove != null)
                {
                    // Delete using LinqToDB
                    await db.StreamRoleBlacklistedUsers
                        .Where(x => x.UserId == userId && x.StreamRoleSettingsId == StreamRoleSetting.Id)
                        .DeleteAsync();

                    success = true;
                }
            }
            else
            {
                var newUser = new StreamRoleBlacklistedUser
                {
                    UserId = userId, Username = userName, StreamRoleSettingsId = StreamRoleSetting.Id
                };

                // Insert using LinqToDB
                await db.InsertAsync(newUser);
                success = true;
            }
        }

        // Refresh cache with updated settings
        UpdateCache(guildId, await db.StreamRoleSettings
            .LoadWithAsTable(srs => srs.StreamRoleWhitelistedUsers)
            .LoadWithAsTable(srs => srs.StreamRoleBlacklistedUsers)
            .FirstOrDefaultAsync(srs => srs.GuildId == guildId));

        if (success) await RescanUsers(guild).ConfigureAwait(false);
        return success;
    }

    /// <summary>
    ///     Sets keyword on a guild and updates the cache.
    /// </summary>
    /// <param name="guild">Guild Id</param>
    /// <param name="keyword">Keyword to set</param>
    /// <returns>The keyword set</returns>
    public async Task<string> SetKeyword(IGuild guild, string? keyword)
    {
        keyword = keyword?.Trim().ToLowerInvariant();
        var guildId = guild.Id;

        await using var db = await dbFactory.CreateConnectionAsync();

        // Get StreamRole settings directly with LinqToDB
        var StreamRoleSetting = await db.StreamRoleSettings
            .FirstOrDefaultAsync(srs => srs.GuildId == guildId);

        if (StreamRoleSetting == null)
        {
            StreamRoleSetting = new StreamRoleSetting
            {
                GuildId = guildId
            };
            await db.InsertAsync(StreamRoleSetting);

            // Reload the newly created settings
            StreamRoleSetting = await db.StreamRoleSettings
                .FirstOrDefaultAsync(srs => srs.GuildId == guildId);
        }

        StreamRoleSetting.Keyword = keyword;

        // Update using LinqToDB
        await db.UpdateAsync(StreamRoleSetting);

        UpdateCache(guildId, StreamRoleSetting);
        await RescanUsers(guild).ConfigureAwait(false);
        return keyword;
    }

    /// <summary>
    ///     Sets the role to monitor, and a role to which to add to
    ///     the user who starts streaming in the monitored role.
    /// </summary>
    /// <param name="fromRole">Role to monitor</param>
    /// <param name="addRole">Role to add to the user</param>
    public async Task SetStreamRole(IRole fromRole, IRole addRole)
    {
        fromRole.ThrowIfNull(nameof(fromRole));
        addRole.ThrowIfNull(nameof(addRole));
        var guildId = fromRole.Guild.Id;

        await using var db = await dbFactory.CreateConnectionAsync();

        // Get StreamRole settings directly with LinqToDB
        var StreamRoleSetting = await db.StreamRoleSettings
            .FirstOrDefaultAsync(srs => srs.GuildId == guildId);

        if (StreamRoleSetting == null)
        {
            StreamRoleSetting = new StreamRoleSetting
            {
                GuildId = guildId
            };
            await db.InsertAsync(StreamRoleSetting);

            // Reload the newly created settings
            StreamRoleSetting = await db.StreamRoleSettings
                .FirstOrDefaultAsync(srs => srs.GuildId == guildId);
        }

        StreamRoleSetting.Enabled = true;
        StreamRoleSetting.AddRoleId = addRole.Id;
        StreamRoleSetting.FromRoleId = fromRole.Id;

        // Update using LinqToDB
        await db.UpdateAsync(StreamRoleSetting);

        UpdateCache(guildId, StreamRoleSetting);

        foreach (var usr in await fromRole.GetMembersAsync().ConfigureAwait(false))
        {
            if (usr is { } x)
                await RescanUser(x, StreamRoleSetting, addRole).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Stops the stream role management in a guild.
    /// </summary>
    /// <param name="guild">The guild to stop stream role management in.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task StopStreamRole(IGuild guild)
    {
        var guildId = guild.Id;
        await using var db = await dbFactory.CreateConnectionAsync();

        // Get StreamRole settings directly with LinqToDB
        var StreamRoleSetting = await db.StreamRoleSettings
            .FirstOrDefaultAsync(srs => srs.GuildId == guildId);

        if (StreamRoleSetting == null)
        {
            StreamRoleSetting = new StreamRoleSetting
            {
                GuildId = guildId
            };
            await db.InsertAsync(StreamRoleSetting);

            // Reload the newly created settings
            StreamRoleSetting = await db.StreamRoleSettings
                .FirstOrDefaultAsync(srs => srs.GuildId == guildId);
        }

        StreamRoleSetting.Enabled = false;
        StreamRoleSetting.AddRoleId = 0;
        StreamRoleSetting.FromRoleId = 0;

        // Update using LinqToDB
        await db.UpdateAsync(StreamRoleSetting);

        UpdateCache(guildId, StreamRoleSetting);
    }

    private async Task RescanUser(IGuildUser user, StreamRoleSetting setting, IRole? addRole = null)
    {
        var g = (StreamingGame)user.Activities
            .FirstOrDefault(a => a is StreamingGame &&
                                 (string.IsNullOrWhiteSpace(setting.Keyword)
                                  || a.Name.Contains(setting.Keyword, StringComparison.InvariantCultureIgnoreCase) ||
                                  setting.StreamRoleWhitelistedUsers.Any(x => x.UserId == user.Id)));

        if (g is not null
            && setting.Enabled
            && setting.StreamRoleBlacklistedUsers.All(x => x.UserId != user.Id)
            && user.RoleIds.Contains(setting.FromRoleId))
        {
            try
            {
                addRole ??= user.Guild.GetRole(setting.AddRoleId);
                if (addRole == null)
                {
                    await StopStreamRole(user.Guild).ConfigureAwait(false);
                    logger.LogWarning("Stream role in server {0} no longer exists. Stopping", setting.AddRoleId);
                    return;
                }

                //check if he doesn't have addrole already, to avoid errors
                if (!user.RoleIds.Contains(setting.AddRoleId))
                    await user.AddRoleAsync(addRole).ConfigureAwait(false);
                logger.LogInformation("Added stream role to user {0} in {1} server", user.ToString(),
                    user.Guild.ToString());
            }
            catch (HttpException ex) when (ex.HttpCode == HttpStatusCode.Forbidden)
            {
                await StopStreamRole(user.Guild).ConfigureAwait(false);
                logger.LogWarning(ex, "Error adding stream role(s). Forcibly disabling stream role feature");
                throw new StreamRolePermissionException();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed adding stream role");
            }
        }
        else
        {
            //check if user is in the addrole
            if (user.RoleIds.Contains(setting.AddRoleId))
            {
                try
                {
                    addRole ??= user.Guild.GetRole(setting.AddRoleId);
                    if (addRole == null)
                        throw new StreamRoleNotFoundException();

                    await user.RemoveRoleAsync(addRole).ConfigureAwait(false);
                    logger.LogInformation("Removed stream role from the user {0} in {1} server", user.ToString(),
                        user.Guild.ToString());
                }
                catch (HttpException ex) when (ex.HttpCode == HttpStatusCode.Forbidden)
                {
                    await StopStreamRole(user.Guild).ConfigureAwait(false);
                    logger.LogWarning(ex, "Error removing stream role(s). Forcibly disabling stream role feature");
                    throw new StreamRolePermissionException();
                }
            }
        }
    }

    private async Task RescanUsers(IGuild guild)
    {
        var guildId = guild.Id;
        await using var db = await dbFactory.CreateConnectionAsync();

        // Get StreamRole settings directly with LinqToDB
        var setting = await db.StreamRoleSettings
            .LoadWithAsTable(srs => srs.StreamRoleWhitelistedUsers)
            .LoadWithAsTable(srs => srs.StreamRoleBlacklistedUsers)
            .FirstOrDefaultAsync(srs => srs.GuildId == guildId);

        if (setting is null || !setting.Enabled)
            return;

        var addRole = guild.GetRole(setting.AddRoleId);
        if (addRole == null)
            return;

        if (setting.Enabled)
        {
            var users = await guild.GetUsersAsync(CacheMode.CacheOnly).ConfigureAwait(false);
            foreach (var usr in users.Where(x =>
                         x.RoleIds.Contains(setting.FromRoleId) || x.RoleIds.Contains(addRole.Id)))
            {
                if (usr is { } x)
                    await RescanUser(x, setting, addRole).ConfigureAwait(false);
            }
        }
    }

    private async void UpdateCache(ulong guildId, StreamRoleSetting setting)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        // Check if settings exist for this guild
        var config = await db.StreamRoleSettings
            .FirstOrDefaultAsync(x => x.GuildId == guildId);

        if (config is not null)
        {
            // Update existing settings using LinqToDB
            await db.UpdateAsync(setting);
        }
        else
        {
            // Insert new settings using LinqToDB
            await db.InsertAsync(setting);
        }
    }
}