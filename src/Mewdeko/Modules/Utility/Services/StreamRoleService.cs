using Discord;
using Discord.Net;
using Discord.WebSocket;
using Mewdeko._Extensions;
using Mewdeko.Common.TypeReaders;
using Mewdeko.Database;
using Mewdeko.Database.Extensions;
using Mewdeko.Database.Models;
using Mewdeko.Modules.Utility.Common;
using Mewdeko.Modules.Utility.Common.Exceptions;
using Serilog;
using System.Collections.Concurrent;
using System.Net;

namespace Mewdeko.Modules.Utility.Services;

public class StreamRoleService : INService, IUnloadableService
{
    private readonly DiscordSocketClient _client;
    private readonly DbService _db;
    private readonly ConcurrentDictionary<ulong, StreamRoleSettings> _guildSettings;

    public StreamRoleService(DiscordSocketClient client, DbService db, Mewdeko bot)
    {
        _db = db;
        _client = client;

        _guildSettings = bot.CachedGuildConfigs
            .ToDictionary(x => x.GuildId, x => x.StreamRole)
            .Where(x => x.Value != null && x.Value.Enabled)
            .ToConcurrent();

        _client.GuildMemberUpdated += Client_GuildMemberUpdated;

        var _ = Task.Run(async () =>
        {
            try
            {
                await Task.WhenAll(client.Guilds.Select(g => RescanUsers(g))).ConfigureAwait(false);
            }
            catch
            {
                // ignored
            }
        });
    }

    public Task Unload()
    {
        _client.GuildMemberUpdated -= Client_GuildMemberUpdated;
        return Task.CompletedTask;
    }

    private Task Client_GuildMemberUpdated(Cacheable<SocketGuildUser, ulong> cacheable, SocketGuildUser after)
    {
        var _ = Task.Run(async () =>
        {
            //if user wasn't streaming or didn't have a game status at all
            if (_guildSettings.TryGetValue(after.Guild.Id, out var setting))
                await RescanUser(after, setting).ConfigureAwait(false);
        });

        return Task.CompletedTask;
    }
    
    public async Task<bool> ApplyListAction(StreamRoleListType listType, IGuild guild, AddRemove action,
        ulong userId, string userName)
    {
        userName.ThrowIfNull(nameof(userName));

        var success = false;
        await using (var uow = _db.GetDbContext())
        {
            var streamRoleSettings = uow.GetStreamRoleSettings(guild.Id);

            if (listType == StreamRoleListType.Whitelist)
            {
                var userObj = new StreamRoleWhitelistedUser
                {
                    UserId = userId,
                    Username = userName
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
                    UserId = userId,
                    Username = userName
                };

                if (action == AddRemove.Rem)
                {
                    var toRemove = streamRoleSettings.Blacklist.FirstOrDefault(x => x.Equals(userObj));
                    if (toRemove != null)
                    {
                        success = true;
                        success = streamRoleSettings.Blacklist.Remove(toRemove);
                    }
                }
                else
                {
                    success = streamRoleSettings.Blacklist.Add(userObj);
                }
            }

            await uow.SaveChangesAsync();
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
        keyword = keyword?.Trim()?.ToLowerInvariant();

        await using (var uow = _db.GetDbContext())
        {
            var streamRoleSettings = uow.GetStreamRoleSettings(guild.Id);

            streamRoleSettings.Keyword = keyword;
            UpdateCache(guild.Id, streamRoleSettings);
            await uow.SaveChangesAsync();
        }

        await RescanUsers(guild).ConfigureAwait(false);
        return keyword;
    }

    /// <summary>
    ///     Gets the currently set keyword on a guild.
    /// </summary>
    /// <param name="guildId">Guild Id</param>
    /// <returns>The keyword set</returns>
    public string GetKeyword(ulong guildId)
    {
        if (_guildSettings.TryGetValue(guildId, out var outSetting))
            return outSetting.Keyword;

        StreamRoleSettings setting;
        using (var uow = _db.GetDbContext())
        {
            setting = uow.GetStreamRoleSettings(guildId);
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
        await using (var uow = _db.GetDbContext())
        {
            var streamRoleSettings = uow.GetStreamRoleSettings(fromRole.Guild.Id);

            streamRoleSettings.Enabled = true;
            streamRoleSettings.AddRoleId = addRole.Id;
            streamRoleSettings.FromRoleId = fromRole.Id;

            setting = streamRoleSettings;
            await uow.SaveChangesAsync();
        }

        UpdateCache(fromRole.Guild.Id, setting);

        foreach (var usr in await fromRole.GetMembersAsync().ConfigureAwait(false))
            if (usr is { } x)
                await RescanUser(x, setting, addRole).ConfigureAwait(false);
    }
    public async Task StopStreamRole(IGuild guild, bool cleanup = false)
    {
        await using (var uow = _db.GetDbContext())
        {
            var streamRoleSettings = uow.GetStreamRoleSettings(guild.Id);
            streamRoleSettings.Enabled = false;
            streamRoleSettings.AddRoleId = 0;
            streamRoleSettings.FromRoleId = 0;
            await uow.SaveChangesAsync();
        }

        if (_guildSettings.TryRemove(guild.Id, out _) && cleanup)
            await RescanUsers(guild).ConfigureAwait(false);
    }

    private async Task RescanUser(IGuildUser user, StreamRoleSettings setting, IRole? addRole = null)
    {
        var g = (StreamingGame) user.Activities
            .FirstOrDefault(a => a is StreamingGame &&
                                 (string.IsNullOrWhiteSpace(setting.Keyword)
                                  || a.Name.ToUpperInvariant().Contains(setting.Keyword.ToUpperInvariant())
                                  || setting.Whitelist.Any(x => x.UserId == user.Id)));

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
                    Log.Warning("Stream role in server {0} no longer exists. Stopping.", setting.AddRoleId);
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

    private async Task RescanUsers(IGuild guild)
    {
        if (!_guildSettings.TryGetValue(guild.Id, out var setting))
            return;

        var addRole = guild.GetRole(setting.AddRoleId);
        if (addRole == null)
            return;

        if (setting.Enabled)
        {
            var users = await guild.GetUsersAsync(CacheMode.CacheOnly).ConfigureAwait(false);
            foreach (var usr in users.Where(x =>
                         x.RoleIds.Contains(setting.FromRoleId) || x.RoleIds.Contains(addRole.Id)))
                if (usr is { } x)
                    await RescanUser(x, setting, addRole).ConfigureAwait(false);
        }
    }

    private void UpdateCache(ulong guildId, StreamRoleSettings setting) => _guildSettings.AddOrUpdate(guildId, _ => setting, (_, _) => setting);
}