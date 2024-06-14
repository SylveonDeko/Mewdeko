using System.Threading;
using LinqToDB.EntityFrameworkCore;
using Mewdeko.Common.Collections;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Mewdeko.Modules.Moderation.Services;

/// <summary>
/// Represents the type of mute.
/// </summary>
public enum MuteType
{
    /// <summary>
    /// Voice mute.
    /// </summary>
    Voice,

    /// <summary>
    /// Chat mute.
    /// </summary>
    Chat,

    /// <summary>
    /// Mute in both voice and chat.
    /// </summary>
    All
}

/// <summary>
/// Service for managing mutes.
/// </summary>
public class MuteService : INService
{
    /// <summary>
    /// The type of timer for punishment.
    /// </summary>
    public enum TimerType
    {
        /// <summary>
        /// Mute
        /// </summary>
        Mute,

        /// <summary>
        /// Yeet
        /// </summary>
        Ban,

        /// <summary>
        /// Add role
        /// </summary>
        AddRole
    }

    private static readonly OverwritePermissions DenyOverwrite =
        new(addReactions: PermValue.Deny, sendMessages: PermValue.Deny,
            attachFiles: PermValue.Deny, sendMessagesInThreads: PermValue.Deny, createPublicThreads: PermValue.Deny);

    private readonly DiscordShardedClient client;
    private readonly DbService db;

    private readonly GuildSettingsService guildSettings;

    /// <summary>
    /// Roles to remove on mute.
    /// </summary>
    public string[] Uroles = Array.Empty<string>();

    /// <summary>
    /// Initializes a new instance of <see cref="MuteService"/>.
    /// </summary>
    /// <param name="client">The discord client</param>
    /// <param name="db">The database provider</param>
    /// <param name="guildSettings">Service for retrieving guildconfigs</param>
    /// <param name="eventHandler">Handler for async events (Hear that dnet? ASYNC, not GATEWAY THREAD)</param>
    /// <param name="bot">The bot</param>
    public MuteService(DiscordShardedClient client, DbService db, GuildSettingsService guildSettings,
        EventHandler eventHandler, Mewdeko bot)
    {
        this.client = client;
        this.db = db;
        this.guildSettings = guildSettings;

        var max = TimeSpan.FromDays(49);

        using var uow = db.GetDbContext();
        var guilds = uow.GuildConfigs.ToLinqToDB().Include(x => x.MutedUsers)
            .Include(x => x.UnmuteTimers)
            .Include(x => x.UnbanTimer)
            .Include(x => x.UnroleTimer).ToList();

        MutedUsers = new ConcurrentDictionary<ulong, ConcurrentHashSet<ulong>>(
            guilds.ToDictionary(x => x.GuildId, x => new ConcurrentHashSet<ulong>(x.MutedUsers.Select(y => y.UserId))));
        Parallel.ForEach(guilds, conf =>
        {
            foreach (var x in conf.UnmuteTimers)
            {
                TimeSpan after;
                if (x.UnmuteAt - TimeSpan.FromMinutes(2) <= DateTime.UtcNow)
                {
                    after = TimeSpan.FromMinutes(2);
                }
                else
                {
                    var unmute = x.UnmuteAt - DateTime.UtcNow;
                    after = unmute > max ? max : unmute;
                }

                StartUn_Timer(conf.GuildId, x.UserId, after, TimerType.Mute);
            }

            foreach (var x in conf.UnbanTimer)
            {
                TimeSpan after;
                if (x.UnbanAt - TimeSpan.FromMinutes(2) <= DateTime.UtcNow)
                {
                    after = TimeSpan.FromMinutes(2);
                }
                else
                {
                    var unban = x.UnbanAt - DateTime.UtcNow;
                    after = unban > max ? max : unban;
                }

                StartUn_Timer(conf.GuildId, x.UserId, after, TimerType.Ban);
            }

            foreach (var x in conf.UnroleTimer)
            {
                TimeSpan after;
                if (x.UnbanAt - TimeSpan.FromMinutes(2) <= DateTime.UtcNow)
                {
                    after = TimeSpan.FromMinutes(2);
                }
                else
                {
                    var unban = x.UnbanAt - DateTime.UtcNow;
                    after = unban > max ? max : unban;
                }

                StartUn_Timer(conf.GuildId, x.UserId, after, TimerType.AddRole, x.RoleId);
            }
        });

        eventHandler.UserJoined += Client_UserJoined;

        UserMuted += OnUserMuted;
        UserUnmuted += OnUserUnmuted;
    }

    /// <summary>
    /// Guild mute roles cache.
    /// </summary>
    public ConcurrentDictionary<ulong, string> GuildMuteRoles { get; } = new();

    /// <summary>
    /// Muted users cache.
    /// </summary>
    public ConcurrentDictionary<ulong, ConcurrentHashSet<ulong>> MutedUsers { get; }

    /// <summary>
    /// Unmute timers cache.
    /// </summary>
    public ConcurrentDictionary<ulong, ConcurrentDictionary<(ulong, TimerType), Timer>> UnTimers { get; }
        = new();

    /// <summary>
    /// Event for when a user is muted.
    /// </summary>
    public event EventHandler.AsyncEventHandler<IGuildUser, IUser, MuteType, string> UserMuted;

    /// <summary>
    /// Event for when a user is unmuted.
    /// </summary>
    public event EventHandler.AsyncEventHandler<IGuildUser, IUser, MuteType, string> UserUnmuted;

    private static async Task OnUserMuted(IGuildUser user, IUser mod, MuteType type, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return;

        await user.SendMessageAsync(embed: new EmbedBuilder()
            .WithDescription($"You've been muted in {user.Guild} server")
            .AddField("Mute Type", type.ToString())
            .AddField("Moderator", mod.ToString())
            .AddField("Reason", reason)
            .Build());
    }

    private static async Task OnUserUnmuted(IGuildUser user, IUser mod, MuteType type, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return;

        await user.SendMessageAsync(embed: new EmbedBuilder()
            .WithDescription($"You've been unmuted in {user.Guild} server")
            .AddField("Unmute Type", type.ToString())
            .AddField("Moderator", mod.ToString())
            .AddField("Reason", reason)
            .Build());
    }

    private async Task Client_UserJoined(IGuildUser usr)
    {
        try
        {
            MutedUsers.TryGetValue(usr.Guild.Id, out var muted);

            if (muted == null || !muted.Contains(usr.Id))
                return;
            await MuteUser(usr, client.CurrentUser, reason: "Sticky mute").ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error in MuteService UserJoined event");
        }
    }

    /// <summary>
    /// Sets the mute role for a guild.
    /// </summary>
    /// <param name="guildId">The id of the guild to set the role in</param>
    /// <param name="name">The name of the role (What in your right fucking mind possessed you to make it this way kwoth???)</param>
    public async Task SetMuteRoleAsync(ulong guildId, string name)
    {
        await using var uow = db.GetDbContext();
        var config = await uow.ForGuildId(guildId, set => set);
        config.MuteRoleName = name;
        GuildMuteRoles.AddOrUpdate(guildId, name, (_, _) => name);
        await uow.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Flow for muting a user
    /// </summary>
    /// <param name="usr">The user to mute </param>
    /// <param name="mod">The mod who muted the user</param>
    /// <param name="type">The type of mute</param>
    /// <param name="reason">The mute reason</param>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public async Task MuteUser(IGuildUser usr, IUser mod, MuteType type = MuteType.All, string reason = "")
    {
        switch (type)
        {
            case MuteType.All:
            {
                try
                {
                    await usr.ModifyAsync(x => x.Mute = true).ConfigureAwait(false);
                }
                catch
                {
                    // ignored
                }

                var uow = db.GetDbContext();
                await using var _ = uow.ConfigureAwait(false);
                var config = await uow.ForGuildId(usr.Guild.Id,
                    set => set.Include(gc => gc.MutedUsers).Include(gc => gc.UnmuteTimers));
                var roles = usr.GetRoles().Where(p => p.Tags == null).Except(new[]
                {
                    usr.Guild.EveryoneRole
                });
                var enumerable = roles as IRole[] ?? roles.ToArray();
                var uroles = string.Join(" ", enumerable.Select(x => x.Id));
                if (await GetRemoveOnMute(usr.Guild.Id) == 0)
                    config.MutedUsers.Add(new MutedUserId
                    {
                        UserId = usr.Id
                    });
                if (await GetRemoveOnMute(usr.Guild.Id) == 1)
                    config.MutedUsers.Add(new MutedUserId
                    {
                        UserId = usr.Id, roles = uroles
                    });
                if (MutedUsers.TryGetValue(usr.Guild.Id, out var muted)) muted.Add(usr.Id);

                config.UnmuteTimers.RemoveWhere(x => x.UserId == usr.Id);

                await uow.SaveChangesAsync().ConfigureAwait(false);
                var muteRole = await GetMuteRole(usr.Guild).ConfigureAwait(false);
                if (!usr.RoleIds.Contains(muteRole.Id))
                {
                    if (await GetRemoveOnMute(usr.Guild.Id) == 1)
                        await usr.RemoveRolesAsync(enumerable).ConfigureAwait(false);
                }

                await usr.AddRoleAsync(muteRole).ConfigureAwait(false);
                StopTimer(usr.GuildId, usr.Id, TimerType.Mute);

                await UserMuted(usr, mod, MuteType.All, reason);
                break;
            }
            case MuteType.Voice:
                try
                {
                    await usr.ModifyAsync(x => x.Mute = true).ConfigureAwait(false);
                    await UserMuted(usr, mod, MuteType.Voice, reason);
                }
                catch
                {
                    // ignored
                }

                break;
            case MuteType.Chat:
                await usr.AddRoleAsync(await GetMuteRole(usr.Guild).ConfigureAwait(false)).ConfigureAwait(false);
                await UserMuted(usr, mod, MuteType.Chat, reason);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }
    }

    /// <summary>
    /// gets whether roles should be removed on mute
    /// </summary>
    /// <param name="id">The server id</param>
    /// <returns></returns>
    public async Task<int> GetRemoveOnMute(ulong id)
        => (await guildSettings.GetGuildConfig(id)).removeroles;

    /// <summary>
    /// Sets whether roles should be removed on mute
    /// </summary>
    /// <param name="guild">The server to set this setting</param>
    /// <param name="yesnt">nosnt</param>
    public async Task Removeonmute(IGuild guild, string yesnt)
    {
        var yesno = -1;
        await using var uow = db.GetDbContext();
        yesno = yesnt switch
        {
            "y" => 1,
            "n" => 0,
            _ => yesno
        };

        var gc = await uow.ForGuildId(guild.Id, set => set);
        gc.removeroles = yesno;
        await guildSettings.UpdateGuildConfig(guild.Id, gc);
    }

    /// <summary>
    /// Flow for unmuting a user
    /// </summary>
    /// <param name="guildId">The guildId where the user is unmooted</param>
    /// <param name="usrId">The user to unmoot </param>
    /// <param name="mod">The mod who unmooted the user</param>
    /// <param name="type">The type of moot</param>
    /// <param name="reason">The unmoot reason reason</param>
    public async Task UnmuteUser(ulong guildId, ulong usrId, IUser mod, MuteType type = MuteType.All,
        string reason = "")
    {
        var usr = client.GetGuild(guildId)?.GetUser(usrId);
        switch (type)
        {
            case MuteType.All:
            {
                StopTimer(guildId, usrId, TimerType.Mute);
                var uow = db.GetDbContext();
                await using (uow.ConfigureAwait(false))
                {
                    var config = await uow.ForGuildId(guildId, set => set.Include(gc => gc.MutedUsers)
                        .Include(gc => gc.UnmuteTimers));
                    if (usr != null && await GetRemoveOnMute(usr.Guild.Id) == 1)
                    {
                        try
                        {
                            Uroles = config.MutedUsers
                                .FirstOrDefault(p => p.UserId == usr.Id && p.roles != null)
                                ?.roles
                                .Split(' ');
                        }
                        catch (Exception)
                        {
                            // ignored
                        }

                        if (Uroles != null)
                        {
                            foreach (var i in Uroles)
                                if (ulong.TryParse(i, out var roleId))
                                    try
                                    {
                                        await usr.AddRoleAsync(usr.Guild.GetRole(roleId)).ConfigureAwait(false);
                                    }
                                    catch
                                    {
                                        // ignored
                                    }
                        }
                    }

                    var match = new MutedUserId
                    {
                        UserId = usrId
                    };
                    var toRemove = config.MutedUsers.FirstOrDefault(x => x.Equals(match));

                    if (toRemove != null) uow.Remove(toRemove);
                    if (MutedUsers.TryGetValue(guildId, out var muted))
                        muted.TryRemove(usrId);

                    config.UnmuteTimers.RemoveWhere(x => x.UserId == usrId);

                    await uow.SaveChangesAsync().ConfigureAwait(false);
                }

                if (usr != null)
                {
                    try
                    {
                        await usr.ModifyAsync(x => x.Mute = false).ConfigureAwait(false);
                    }
                    catch
                    {
                        // ignored
                    }

                    try
                    {
                        await usr.RemoveRoleAsync(await GetMuteRole(usr.Guild).ConfigureAwait(false))
                            .ConfigureAwait(false);
                    }
                    catch
                    {
                        /*ignore*/
                    }

                    await UserUnmuted(usr, mod, MuteType.All, reason);
                }

                break;
            }
            case MuteType.Voice when usr == null:
                return;
            case MuteType.Voice:
                try
                {
                    await usr.ModifyAsync(x => x.Mute = false).ConfigureAwait(false);
                    await UserUnmuted(usr, mod, MuteType.Voice, reason);
                }
                catch
                {
                    // ignored
                }

                break;
            case MuteType.Chat when usr == null:
                return;
            case MuteType.Chat:
                await usr.RemoveRoleAsync(await GetMuteRole(usr.Guild).ConfigureAwait(false)).ConfigureAwait(false);
                await UserUnmuted(usr, mod, MuteType.Chat, reason);
                break;
        }
    }

    /// <summary>
    /// Gets the mute role for a guild.
    /// </summary>
    /// <param name="guild">The guildid to get the mute role from</param>
    /// <returns>The mute <see cref="IRole"/></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public async Task<IRole> GetMuteRole(IGuild guild)
    {
        ArgumentNullException.ThrowIfNull(guild);

        const string defaultMuteRoleName = "Mewdeko-mute";

        var muteRoleName = GuildMuteRoles.GetOrAdd(guild.Id, defaultMuteRoleName);

        var muteRole = guild.Roles.FirstOrDefault(r => r.Name == muteRoleName);
        if (muteRole == null)
        {
            //if it doesn't exist, create it
            try
            {
                muteRole = await guild.CreateRoleAsync(muteRoleName, isMentionable: false).ConfigureAwait(false);
            }
            catch
            {
                //if creations fails,  maybe the name != correct, find default one, if doesn't work, create default one
                muteRole = guild.Roles.FirstOrDefault(r => r.Name == muteRoleName) ??
                           await guild.CreateRoleAsync(defaultMuteRoleName, isMentionable: false)
                               .ConfigureAwait(false);
            }
        }

        foreach (var toOverwrite in await guild.GetTextChannelsAsync().ConfigureAwait(false))
        {
            try
            {
                if (toOverwrite is IThreadChannel)
                    continue;
                if (toOverwrite.PermissionOverwrites.Any(x => x.TargetId == muteRole.Id
                                                              && x.TargetType == PermissionTarget.Role))
                {
                    continue;
                }

                await toOverwrite.AddPermissionOverwriteAsync(muteRole, DenyOverwrite)
                    .ConfigureAwait(false);

                await Task.Delay(200).ConfigureAwait(false);
            }
            catch
            {
                // ignored
            }
        }

        return muteRole;
    }

    /// <summary>
    /// Mutes a user for a specified amount of time.
    /// </summary>
    /// <param name="user">The user to mute</param>
    /// <param name="mod">The mod who muted the user</param>
    /// <param name="after">The time to mute the user for</param>
    /// <param name="muteType">The type of mute</param>
    /// <param name="reason">The reason for the mute</param>
    public async Task TimedMute(IGuildUser user, IUser mod, TimeSpan after, MuteType muteType = MuteType.All,
        string reason = "")
    {
        await MuteUser(user, mod, muteType, reason)
            .ConfigureAwait(false); // mute the user. This will also remove any previous unmute timers
        var uow = db.GetDbContext();
        await using (uow.ConfigureAwait(false))
        {
            var config = await uow.ForGuildId(user.GuildId, set => set.Include(x => x.UnmuteTimers));
            config.UnmuteTimers.Add(new UnmuteTimer
            {
                UserId = user.Id, UnmuteAt = DateTime.UtcNow + after
            }); // add teh unmute timer to the database
            await uow.SaveChangesAsync().ConfigureAwait(false);
        }

        StartUn_Timer(user.GuildId, user.Id, after, TimerType.Mute); // start the timer
    }

    /// <summary>
    /// Bans a user for a specified amount of time.
    /// </summary>
    /// <param name="guild">The guild to ban the user from</param>
    /// <param name="user">The user to ban</param>
    /// <param name="after">The time to ban the user for</param>
    /// <param name="reason">The reason for the ban</param>
    /// <param name="todelete">The time to delete the ban message</param>
    public async Task TimedBan(IGuild guild, IUser user, TimeSpan after, string reason, TimeSpan todelete = default)
    {
        await guild.AddBanAsync(user.Id, todelete.Days, options: new RequestOptions
        {
            AuditLogReason = reason
        }).ConfigureAwait(false);
        var uow = db.GetDbContext();
        await using (uow.ConfigureAwait(false))
        {
            var config = await uow.ForGuildId(guild.Id, set => set.Include(x => x.UnbanTimer));
            config.UnbanTimer.Add(new UnbanTimer
            {
                UserId = user.Id, UnbanAt = DateTime.UtcNow + after
            }); // add teh unmute timer to the database
            await uow.SaveChangesAsync().ConfigureAwait(false);
        }

        StartUn_Timer(guild.Id, user.Id, after, TimerType.Ban); // start the timer
    }

    /// <summary>
    /// Adds a role to a user for a specified amount of time.
    /// </summary>
    /// <param name="user">The user to add the role to</param>
    /// <param name="after">The time to add the role for</param>
    /// <param name="reason">The reason for adding the role</param>
    /// <param name="role">The role to add</param>
    public async Task TimedRole(IGuildUser user, TimeSpan after, string reason, IRole role)
    {
        await user.AddRoleAsync(role).ConfigureAwait(false);
        var uow = db.GetDbContext();
        await using (uow.ConfigureAwait(false))
        {
            var config = await uow.ForGuildId(user.GuildId, set => set.Include(x => x.UnroleTimer));
            config.UnroleTimer.Add(new UnroleTimer
            {
                UserId = user.Id, UnbanAt = DateTime.UtcNow + after, RoleId = role.Id
            }); // add teh unmute timer to the database
            await uow.SaveChangesAsync().ConfigureAwait(false);
        }

        StartUn_Timer(user.GuildId, user.Id, after, TimerType.AddRole, role.Id); // start the timer
    }

    /// <summary>
    /// Starts a timer to unmute a user.
    /// </summary>
    /// <param name="guildId">The guildId where the user is unmuted</param>
    /// <param name="userId">The user to unmute</param>
    /// <param name="after">The time to unmute the user after</param>
    /// <param name="type">The type of timer</param>
    /// <param name="roleId">The role to remove</param>
    public void StartUn_Timer(ulong guildId, ulong userId, TimeSpan after, TimerType type, ulong? roleId = null)
    {
        //load the unmute timers for this guild
        var userUnTimers = UnTimers.GetOrAdd(guildId, new ConcurrentDictionary<(ulong, TimerType), Timer>());

        //unmute timer to be added
        // ReSharper disable once AsyncVoidLambda
        var toAdd = new Timer(async _ =>
        {
            switch (type)
            {
                case TimerType.Ban:
                    try
                    {
                        await RemoveTimerFromDb(guildId, userId, type);
                        StopTimer(guildId, userId, type);
                        var guild = client.GetGuild(guildId); // load the guild
                        if (guild != null) await guild.RemoveBanAsync(userId).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Couldn't unban user {0} in guild {1}", userId, guildId);
                    }

                    break;
                case TimerType.AddRole:
                    try
                    {
                        await RemoveTimerFromDb(guildId, userId, type);
                        StopTimer(guildId, userId, type);
                        var guild = client.GetGuild(guildId);
                        var user = guild?.GetUser(userId);
                        if (guild == null) return;
                        if (roleId == null) return;
                        var role = guild.GetRole(roleId.Value);
                        if (user != null && user.Roles.Contains(role))
                            await user.RemoveRoleAsync(role).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Couldn't remove role from user {0} in guild {1}", userId, guildId);
                    }

                    break;
                default:
                    try
                    {
                        // unmute the user, this will also remove the timer from the db
                        await UnmuteUser(guildId, userId, client.CurrentUser, reason: "Timed mute expired")
                            .ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        await RemoveTimerFromDb(guildId, userId, type); // if unmute errored, just remove unmute from db
                        Log.Warning(ex, "Couldn't unmute user {0} in guild {1}", userId, guildId);
                    }

                    break;
            }
        }, null, after, Timeout.InfiniteTimeSpan);

        //add it, or stop the old one and add this one
        userUnTimers.AddOrUpdate((userId, type), _ => toAdd, (_, old) =>
        {
            old.Change(Timeout.Infinite, Timeout.Infinite);
            return toAdd;
        });
    }

    /// <summary>
    /// Stops a timer for a user.
    /// </summary>
    /// <param name="guildId">The guildId where the timer is stopped</param>
    /// <param name="userId">The user to stop the timer for</param>
    /// <param name="type">The type of timer</param>
    public void StopTimer(ulong guildId, ulong userId, TimerType type)
    {
        if (!UnTimers.TryGetValue(guildId, out var userTimer))
            return;

        if (userTimer.TryRemove((userId, type), out var removed))
            removed.Change(Timeout.Infinite, Timeout.Infinite);
    }

    private async Task RemoveTimerFromDb(ulong guildId, ulong userId, TimerType type)
    {
        await using var uow = db.GetDbContext();
        object toDelete;
        if (type == TimerType.Mute)
        {
            var config = await uow.ForGuildId(guildId, set => set.Include(x => x.UnmuteTimers));
            toDelete = config.UnmuteTimers.FirstOrDefault(x => x.UserId == userId);
        }
        else
        {
            var config = await uow.ForGuildId(guildId, set => set.Include(x => x.UnbanTimer));
            toDelete = config.UnbanTimer.FirstOrDefault(x => x.UserId == userId);
        }

        if (toDelete != null) uow.Remove(toDelete);
        await uow.SaveChangesAsync().ConfigureAwait(false);
    }
}