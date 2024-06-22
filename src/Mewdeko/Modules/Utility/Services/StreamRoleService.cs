using System.Net;
using Discord.Net;
using Mewdeko.Common.TypeReaders;
using Mewdeko.Modules.Utility.Common;
using Mewdeko.Modules.Utility.Common.Exceptions;
using Serilog;

namespace Mewdeko.Modules.Utility.Services;

/// <summary>
/// Manages stream role assignments based on user streaming status and additional configurable conditions within guilds.
/// </summary>
public class StreamRoleService : INService, IUnloadableService
{
    private readonly MewdekoContext dbContext;
    private readonly EventHandler eventHandler;
    private readonly GuildSettingsService gss;

    /// <summary>
    /// Initializes a new instance of the <see cref="StreamRoleService"/>.
    /// </summary>
    /// <param name="client">The Discord client used to access guild and user information.</param>
    /// <param name="db">The database service for storing and retrieving stream role settings.</param>
    /// <param name="eventHandler">Event handler for capturing and responding to guild member updates.</param>
    /// <param name="gss">The guild settings service for retrieving guild-specific settings.</param>
    public StreamRoleService(DiscordShardedClient client, MewdekoContext dbContext, EventHandler eventHandler,
        GuildSettingsService gss)
    {
        this.dbContext = dbContext;
        this.eventHandler = eventHandler;
        this.gss = gss;


        eventHandler.GuildMemberUpdated += Client_GuildMemberUpdated;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.WhenAll(client.Guilds.Select(RescanUsers)).ConfigureAwait(false);
            }
            catch
            {
                // ignored
            }
        });
    }

    /// <summary>
    /// Unloads the service, detaching event handlers to stop listening to guild member updates.
    /// </summary>
    public Task Unload()
    {
        eventHandler.GuildMemberUpdated -= Client_GuildMemberUpdated;
        return Task.CompletedTask;
    }

    private async Task Client_GuildMemberUpdated(Cacheable<SocketGuildUser, ulong> cacheable, SocketGuildUser after)
    {
        var config = await gss.GetGuildConfig(after.Guild.Id);
        if (config.StreamRole.Enabled)
            return;
        await RescanUser(after, config.StreamRole);
    }

    /// <summary>
    /// Adds or removes a user to/from a whitelist or blacklist for stream role management, and rescans users if successful.
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

        var success = false;

        await using (dbContext.ConfigureAwait(false))
        {
            var streamRoleSettings = await dbContext.GetStreamRoleSettings(guild.Id);

            if (listType == StreamRoleListType.Whitelist)
            {
                var userObj = new StreamRoleWhitelistedUser
                {
                    UserId = userId, Username = userName
                };

                if (action == AddRemove.Rem)
                {
                    var toDelete = streamRoleSettings.Whitelist.FirstOrDefault(x => x.Equals(userObj));
                    if (toDelete != null)
                    {
                        dbContext.Remove(toDelete);
                        success = true;
                    }
                }
                else
                {
                    success = streamRoleSettings.Whitelist.Add(userObj);
                }
            }
            else
            {
                var userObj = new StreamRoleBlacklistedUser
                {
                    UserId = userId, Username = userName
                };

                if (action == AddRemove.Rem)
                {
                    var toRemove = streamRoleSettings.Blacklist.FirstOrDefault(x => x.Equals(userObj));
                    if (toRemove != null)
                    {
                        success = streamRoleSettings.Blacklist.Remove(toRemove);
                    }
                }
                else
                {
                    success = streamRoleSettings.Blacklist.Add(userObj);
                }
            }

            await dbContext.SaveChangesAsync().ConfigureAwait(false);
            UpdateCache(guild.Id, streamRoleSettings);
        }

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


        await using (dbContext.ConfigureAwait(false))
        {
            var streamRoleSettings = await dbContext.GetStreamRoleSettings(guild.Id);

            streamRoleSettings.Keyword = keyword;
            UpdateCache(guild.Id, streamRoleSettings);
            await dbContext.SaveChangesAsync().ConfigureAwait(false);
        }

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

        StreamRoleSettings setting;

        await using (dbContext.ConfigureAwait(false))
        {
            var streamRoleSettings = await dbContext.GetStreamRoleSettings(fromRole.Guild.Id);

            streamRoleSettings.Enabled = true;
            streamRoleSettings.AddRoleId = addRole.Id;
            streamRoleSettings.FromRoleId = fromRole.Id;

            setting = streamRoleSettings;
            await dbContext.SaveChangesAsync().ConfigureAwait(false);
        }

        UpdateCache(fromRole.Guild.Id, setting);

        foreach (var usr in await fromRole.GetMembersAsync().ConfigureAwait(false))
        {
            if (usr is { } x)
                await RescanUser(x, setting, addRole).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Stops the stream role management in a guild.
    /// </summary>
    /// <param name="guild">The guild to stop stream role management in.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task StopStreamRole(IGuild guild)
    {

        await using var disposable = dbContext.ConfigureAwait(false);
        var streamRoleSettings = await dbContext.GetStreamRoleSettings(guild.Id);
        streamRoleSettings.Enabled = false;
        streamRoleSettings.AddRoleId = 0;
        streamRoleSettings.FromRoleId = 0;
        await dbContext.SaveChangesAsync().ConfigureAwait(false);
        UpdateCache(guild.Id, streamRoleSettings);
    }

    private async Task RescanUser(IGuildUser user, StreamRoleSettings setting, IRole? addRole = null)
    {
        var g = (StreamingGame)user.Activities
            .FirstOrDefault(a => a is StreamingGame &&
                                 (string.IsNullOrWhiteSpace(setting.Keyword)
                                  || a.Name.Contains(setting.Keyword, StringComparison.InvariantCultureIgnoreCase) ||
                                  setting.Whitelist.Any(x => x.UserId == user.Id)));

        if (g is not null
            && setting.Enabled
            && setting.Blacklist.All(x => x.UserId != user.Id)
            && user.RoleIds.Contains(setting.FromRoleId))
        {
            try
            {
                addRole ??= user.Guild.GetRole(setting.AddRoleId);
                if (addRole == null)
                {
                    await StopStreamRole(user.Guild).ConfigureAwait(false);
                    Log.Warning("Stream role in server {0} no longer exists. Stopping", setting.AddRoleId);
                    return;
                }

                //check if he doesn't have addrole already, to avoid errors
                if (!user.RoleIds.Contains(setting.AddRoleId))
                    await user.AddRoleAsync(addRole).ConfigureAwait(false);
                Log.Information("Added stream role to user {0} in {1} server", user.ToString(),
                    user.Guild.ToString());
            }
            catch (HttpException ex) when (ex.HttpCode == HttpStatusCode.Forbidden)
            {
                await StopStreamRole(user.Guild).ConfigureAwait(false);
                Log.Warning(ex, "Error adding stream role(s). Forcibly disabling stream role feature");
                throw new StreamRolePermissionException();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed adding stream role");
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
                    Log.Information("Removed stream role from the user {0} in {1} server", user.ToString(),
                        user.Guild.ToString());
                }
                catch (HttpException ex) when (ex.HttpCode == HttpStatusCode.Forbidden)
                {
                    await StopStreamRole(user.Guild).ConfigureAwait(false);
                    Log.Warning(ex, "Error removing stream role(s). Forcibly disabling stream role feature");
                    throw new StreamRolePermissionException();
                }
            }
        }
    }

    private async Task RescanUsers(IGuild guild)
    {
        var config = await gss.GetGuildConfig(guild.Id);
        var setting = config.StreamRole;

        if (!setting.Enabled)
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

    private async void UpdateCache(ulong guildId, StreamRoleSettings setting)
    {
        var gc = await gss.GetGuildConfig(guildId);
        gc.StreamRole = setting;
        await gss.UpdateGuildConfig(guildId, gc);
    }
}