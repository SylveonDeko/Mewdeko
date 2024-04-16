﻿using System.Net;
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
    private readonly DbService db;
    private readonly EventHandler eventHandler;
    private readonly ConcurrentDictionary<ulong, StreamRoleSettings> guildSettings;

    /// <summary>
    /// Initializes a new instance of the <see cref="StreamRoleService"/>.
    /// </summary>
    /// <param name="client">The Discord client used to access guild and user information.</param>
    /// <param name="db">The database service for storing and retrieving stream role settings.</param>
    /// <param name="eventHandler">Event handler for capturing and responding to guild member updates.</param>
    /// <param name="bot">The bot instance for initializing service with current guild configurations.</param>
    public StreamRoleService(DiscordSocketClient client, DbService db, EventHandler eventHandler, Mewdeko bot)
    {
        this.db = db;
        this.eventHandler = eventHandler;

        guildSettings = bot.AllGuildConfigs
            .ToDictionary(x => x.Key, x => x.Value.StreamRole)
            .Where(x => x.Value is { Enabled: 1 })
            .ToConcurrent();

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

    private Task Client_GuildMemberUpdated(Cacheable<SocketGuildUser, ulong> cacheable, SocketGuildUser after)
    {
        _ = Task.Run(() =>
        {
            //if user wasn't streaming or didn't have a game status at all
            if (guildSettings.TryGetValue(after.Guild.Id, out var setting))
                return RescanUser(after, setting);
            return Task.CompletedTask;
        });

        return Task.CompletedTask;
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
        var uow = db.GetDbContext();
        await using (uow.ConfigureAwait(false))
        {
            var streamRoleSettings = await uow.GetStreamRoleSettings(guild.Id);

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
                        uow.Remove(toDelete);
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

            await uow.SaveChangesAsync().ConfigureAwait(false);
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

        var uow = db.GetDbContext();
        await using (uow.ConfigureAwait(false))
        {
            var streamRoleSettings = await uow.GetStreamRoleSettings(guild.Id);

            streamRoleSettings.Keyword = keyword;
            UpdateCache(guild.Id, streamRoleSettings);
            await uow.SaveChangesAsync().ConfigureAwait(false);
        }

        await RescanUsers(guild).ConfigureAwait(false);
        return keyword;
    }

    /// <summary>
    ///     Gets the currently set keyword on a guild.
    /// </summary>
    /// <param name="guildId">Guild Id</param>
    /// <returns>The keyword set</returns>
    public async Task<string> GetKeyword(ulong guildId)
    {
        if (guildSettings.TryGetValue(guildId, out var outSetting))
            return outSetting.Keyword;

        StreamRoleSettings setting;
        await using (var uow = db.GetDbContext())
        {
            setting = await uow.GetStreamRoleSettings(guildId);
        }

        UpdateCache(guildId, setting);

        return setting.Keyword;
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
        var uow = db.GetDbContext();
        await using (uow.ConfigureAwait(false))
        {
            var streamRoleSettings = await uow.GetStreamRoleSettings(fromRole.Guild.Id);

            streamRoleSettings.Enabled = 1;
            streamRoleSettings.AddRoleId = addRole.Id;
            streamRoleSettings.FromRoleId = fromRole.Id;

            setting = streamRoleSettings;
            await uow.SaveChangesAsync().ConfigureAwait(false);
        }

        UpdateCache(fromRole.Guild.Id, setting);

        foreach (var usr in await fromRole.GetMembersAsync().ConfigureAwait(false))
        {
            if (usr is { } x)
                await RescanUser(x, setting, addRole).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Stops the stream role management in a guild, optionally cleaning up by removing the stream role from all users.
    /// </summary>
    /// <param name="guild">The guild to stop stream role management in.</param>
    /// <param name="cleanup">Whether to clean up by removing the stream role from all users.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task StopStreamRole(IGuild guild, bool cleanup = false)
    {
        var uow = db.GetDbContext();
        await using (uow.ConfigureAwait(false))
        {
            var streamRoleSettings = await uow.GetStreamRoleSettings(guild.Id);
            streamRoleSettings.Enabled = 0;
            streamRoleSettings.AddRoleId = 0;
            streamRoleSettings.FromRoleId = 0;
            await uow.SaveChangesAsync().ConfigureAwait(false);
        }

        if (guildSettings.TryRemove(guild.Id, out _) && cleanup)
            await RescanUsers(guild).ConfigureAwait(false);
    }

    private async Task RescanUser(IGuildUser user, StreamRoleSettings setting, IRole? addRole = null)
    {
        var g = (StreamingGame)user.Activities
            .FirstOrDefault(a => a is StreamingGame &&
                                 (string.IsNullOrWhiteSpace(setting.Keyword)
                                  || a.Name.Contains(setting.Keyword, StringComparison.InvariantCultureIgnoreCase) ||
                                  setting.Whitelist.Any(x => x.UserId == user.Id)));

        if (g is not null
            && setting.Enabled == 1
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
        if (!guildSettings.TryGetValue(guild.Id, out var setting))
            return;

        var addRole = guild.GetRole(setting.AddRoleId);
        if (addRole == null)
            return;

        if (setting.Enabled == 1)
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

    private void UpdateCache(ulong guildId, StreamRoleSettings setting) =>
        guildSettings.AddOrUpdate(guildId, _ => setting, (_, _) => setting);
}