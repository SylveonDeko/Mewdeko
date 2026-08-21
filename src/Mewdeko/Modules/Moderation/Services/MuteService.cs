using System.Diagnostics;
using System.Threading;
using DataModel;
using Discord.Net;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Common.Collections;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Modules.Moderation.Common;
using Mewdeko.Services.Strings;

namespace Mewdeko.Modules.Moderation.Services;

/// <summary>
///     Represents the type of mute.
/// </summary>
public enum MuteType
{
    /// <summary>
    ///     Voice mute.
    /// </summary>
    Voice,

    /// <summary>
    ///     Chat mute.
    /// </summary>
    Chat,

    /// <summary>
    ///     Mute in both voice and chat.
    /// </summary>
    All
}

/// <summary>
///     Service for managing mutes.
/// </summary>
public class MuteService : INService, IReadyExecutor, IDisposable
{
    /// <summary>
    ///     The type of timer for punishment.
    /// </summary>
    public enum TimerType
    {
        /// <summary>
        ///     Mute
        /// </summary>
        Mute,

        /// <summary>
        ///     Yeet
        /// </summary>
        Ban,

        /// <summary>
        ///     Add role
        /// </summary>
        AddRole
    }

    private static readonly OverwritePermissions DenyOverwrite =
        new(addReactions: PermValue.Deny, sendMessages: PermValue.Deny,
            attachFiles: PermValue.Deny, sendMessagesInThreads: PermValue.Deny, createPublicThreads: PermValue.Deny);

    private readonly ConcurrentDictionary<TimerKey, Timer> activeTimers = new();

    private readonly BanPruneService banPrune;

    private readonly DiscordShardedClient client;
    private readonly IDataConnectionFactory dbFactory;
    private readonly EventHandler eventHandler;

    private readonly GuildSettingsService guildSettings;
    private readonly ILogger<MuteService> logger;
    private readonly GeneratedBotStrings strings;


    /// <summary>
    ///     Roles to remove on mute.
    /// </summary>
    public string[] Uroles = [];

    /// <summary>
    ///     Initializes a new instance of <see cref="MuteService" />.
    /// </summary>
    /// <param name="client">The discord client</param>
    /// <param name="dbFactory">The database provider</param>
    /// <param name="guildSettings">Service for retrieving guildconfigs</param>
    /// <param name="eventHandler">Handler for async events (Hear that dnet? ASYNC, not GATEWAY THREAD)</param>
    /// <param name="strings">The localization service</param>
    /// <param name="banPrune">The service resolving how many days of messages a ban purges</param>
    /// <param name="logger">The logger instance for structured logging.</param>
    public MuteService(DiscordShardedClient client, IDataConnectionFactory dbFactory,
        GuildSettingsService guildSettings,
        EventHandler eventHandler, GeneratedBotStrings strings, BanPruneService banPrune,
        ILogger<MuteService> logger)
    {
        this.client = client;
        this.banPrune = banPrune;
        this.dbFactory = dbFactory;
        this.guildSettings = guildSettings;
        this.strings = strings;
        this.logger = logger;
        this.eventHandler = eventHandler;
        eventHandler.Subscribe("UserJoined", "MuteService", Client_UserJoined);
        UserMuted += OnUserMuted;
        UserUnmuted += OnUserUnmuted;
    }

    /// <summary>
    ///     Guild mute roles cache.
    /// </summary>
    public ConcurrentDictionary<ulong, string> GuildMuteRoles { get; } = new();

    /// <summary>
    ///     Muted users cache.
    /// </summary>
    public ConcurrentDictionary<ulong, ConcurrentHashSet<ulong>> MutedUsers { get; set; } = new();

    /// <summary>
    ///     Unmute timers cache.
    /// </summary>
    public ConcurrentDictionary<ulong, ConcurrentDictionary<(ulong, TimerType), Timer>> UnTimers { get; }
        = new();

    /// <summary>
    ///     Dispose
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    public async Task OnReadyAsync()
    {
        try
        {
            await using var dbContext = await dbFactory.CreateConnectionAsync();

            // Load muted users
            var mutedUsersList = await dbContext.MutedUserIds
                .Where(x => x.GuildId != null && x.GuildId != 0)
                .Select(x => new
                {
                    x.GuildId, x.UserId
                })
                .Distinct()
                .ToListAsync();

            MutedUsers = new ConcurrentDictionary<ulong, ConcurrentHashSet<ulong>>(
                mutedUsersList
                    .GroupBy(x =>
                    {
                        Debug.Assert(x.GuildId != null);
                        return x.GuildId.Value;
                    })
                    .ToDictionary(
                        g => g.Key,
                        g => new ConcurrentHashSet<ulong>(g.Select(x => x.UserId).Distinct())
                    )
            );

            // Load unmute timers
            var unmuteTimers = await dbContext.UnmuteTimers
                .Where(x => x.GuildId != null && x.GuildId != 0)
                .Select(x => new
                {
                    x.GuildId, x.UserId, x.UnmuteAt
                })
                .ToListAsync();

            foreach (var timer in unmuteTimers)
            {
                var key = new TimerKey(timer.GuildId.Value, timer.UserId, TimerType.Mute);
                CreateIndividualTimer(key, timer.UnmuteAt);
            }

            // Load unban timers
            var unbanTimers = await dbContext.UnbanTimers
                .Where(x => x.GuildId != null && x.GuildId != 0)
                .Select(x => new
                {
                    x.GuildId, x.UserId, x.UnbanAt
                })
                .ToListAsync();

            foreach (var timer in unbanTimers)
            {
                var key = new TimerKey(timer.GuildId.Value, timer.UserId, TimerType.Ban);
                CreateIndividualTimer(key, timer.UnbanAt);
            }

            // Load unrole timers
            var unroleTimers = await dbContext.UnroleTimers
                .Select(x => new
                {
                    x.GuildId, x.UserId, x.RoleId, x.UnbanAt
                })
                .ToListAsync();

            foreach (var timer in unroleTimers)
            {
                var key = new TimerKey(timer.GuildId.Value, timer.UserId, TimerType.AddRole, timer.RoleId);
                CreateIndividualTimer(key, timer.UnbanAt);
            }

            logger.LogInformation(
                "Loaded {UnmuteCount} unmute timers, {UnbanCount} unban timers, and {UnroleCount} unrole timers",
                unmuteTimers.Count, unbanTimers.Count, unroleTimers.Count);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error in MuteService.OnReadyAsync");
        }
    }

    /// <summary>
    ///     Event for when a user is muted.
    /// </summary>
    public event EventHandler.AsyncEventHandler<IGuildUser, IUser, MuteType, string> UserMuted;

    /// <summary>
    ///     Event for when a user is unmuted.
    /// </summary>
    public event EventHandler.AsyncEventHandler<IGuildUser, IUser, MuteType, string> UserUnmuted;


    /// <summary>
    ///     Creates an individual timer for a specific expiration time
    /// </summary>
    private void CreateIndividualTimer(TimerKey key, DateTime expireAt)
    {
        var delay = expireAt - DateTime.UtcNow;

        // If already expired, process immediately
        if (delay <= TimeSpan.Zero)
        {
            _ = ProcessIndividualItem(key);
            return;
        }

        // Create timer that fires once at the exact expiration time
        var timer = new Timer(async _ =>
        {
            await ProcessIndividualItem(key);

            // Remove the timer after processing
            if (activeTimers.TryRemove(key, out var timerToDispose))
            {
                timerToDispose.Dispose();
            }
        }, null, delay, Timeout.InfiniteTimeSpan);

        // Store the timer, disposing any existing one
        if (activeTimers.TryGetValue(key, out var existingTimer))
        {
            existingTimer.Dispose();
        }

        activeTimers[key] = timer;
    }

    /// <summary>
    ///     Processes a single timer item when it expires
    /// </summary>
    private async Task ProcessIndividualItem(TimerKey key)
    {
        try
        {
            switch (key.Type)
            {
                case TimerType.Mute:
                    await ProcessIndividualMuteItem(key);
                    break;
                case TimerType.Ban:
                    await ProcessIndividualBanItem(key);
                    break;
                case TimerType.AddRole:
                    await ProcessIndividualRoleItem(key);
                    break;
                default:
                    logger.LogWarning("Unknown timer type {TimerType} for key {Key}", key.Type, key);
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing individual timer item {Key}", key);
        }
    }

    private async Task ProcessIndividualMuteItem(TimerKey key)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        try
        {
            var guildId = key.GuildId;
            var userId = key.UserId;

            // Find the user
            var guild = client.GetGuild(guildId);
            if (guild != null)
            {
                // Unmute the user
                await UnmuteUser(guildId, userId, client.CurrentUser, reason: "Timed mute expired");
            }
            else
            {
                logger.LogInformation("Cleaning up mute timer for non-existent guild {GuildId}", guildId);
            }

            // Remove from database
            await dbContext.UnmuteTimers
                .Where(x => x.GuildId == guildId && x.UserId == userId)
                .DeleteAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error processing mute timer for guild {GuildId}, user {UserId}", key.GuildId,
                key.UserId);
        }
    }

    private async Task ProcessIndividualBanItem(TimerKey key)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        try
        {
            var guildId = key.GuildId;
            var userId = key.UserId;

            // Find the guild
            var guild = client.GetGuild(guildId);
            if (guild != null)
            {
                try
                {
                    // Unban the user
                    await guild.RemoveBanAsync(userId);
                    logger.LogDebug("Successfully unbanned user {UserId} from guild {GuildId}", userId, guildId);
                }
                catch (HttpException httpEx) when (httpEx.DiscordCode == DiscordErrorCode.UnknownBan)
                {
                    logger.LogInformation("User {UserId} was already unbanned from guild {GuildId}", userId, guildId);
                }
                catch (HttpException httpEx) when (httpEx.DiscordCode == DiscordErrorCode.MissingPermissions)
                {
                    logger.LogWarning("Missing permissions to unban user {UserId} from guild {GuildId}", userId,
                        guildId);
                }
            }
            else
            {
                logger.LogInformation("Cleaning up ban timer for non-existent guild {GuildId}", guildId);
            }

            // Remove from database
            await dbContext.UnbanTimers
                .Where(x => x.GuildId == guildId && x.UserId == userId)
                .DeleteAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error processing ban timer for guild {GuildId}, user {UserId}", key.GuildId,
                key.UserId);
        }
    }

    private async Task ProcessIndividualRoleItem(TimerKey key)
    {
        try
        {
            var guildId = key.GuildId;
            var userId = key.UserId;
            var roleId = key.RoleId.Value; // Safe to use .Value since we filtered for role items

            var guild = client.GetGuild(guildId);

            // Guild doesn't exist - safe to clean up
            if (guild == null)
            {
                logger.LogInformation("Cleaning up role timer for non-existent guild {GuildId}", guildId);
                await CleanupRoleTimer(guildId, userId, roleId);
                return;
            }

            var user = guild.GetUser(userId);

            // User not in guild - safe to clean up
            if (user == null)
            {
                logger.LogInformation("Cleaning up role timer for user {UserId} who left guild {GuildId}", userId,
                    guildId);
                await CleanupRoleTimer(guildId, userId, roleId);
                return;
            }

            var role = guild.GetRole(roleId);

            // Role doesn't exist - safe to clean up
            if (role == null)
            {
                logger.LogInformation("Cleaning up role timer for deleted role {RoleId} in guild {GuildId}", roleId,
                    guildId);
                await CleanupRoleTimer(guildId, userId, roleId);
                return;
            }

            // User doesn't have the role - goal already achieved, safe to clean up
            if (!user.Roles.Contains(role))
            {
                logger.LogDebug("Role {RoleId} already removed from user {UserId} in guild {GuildId}", roleId, userId,
                    guildId);
                await CleanupRoleTimer(guildId, userId, roleId);
                return;
            }

            // Check bot permissions
            var botUser = guild.GetUser(client.CurrentUser.Id);
            if (botUser == null || !botUser.GuildPermissions.ManageRoles)
            {
                logger.LogWarning("Bot lacks ManageRoles permission in guild {GuildId}, will retry in 1 hour", guildId);
                CreateIndividualTimer(key, DateTime.UtcNow.AddHours(1)); // Retry in 1 hour
                return;
            }

            // Check role hierarchy
            var botHighestRole = botUser.Roles.Where(r => r.Id != guild.EveryoneRole.Id).MaxBy(r => r.Position);
            if (botHighestRole == null || role.Position >= botHighestRole.Position)
            {
                logger.LogWarning(
                    "Bot cannot manage role {RoleName} (position {RolePosition}) in guild {GuildId}, will retry in 1 hour",
                    role.Name, role.Position, guildId);
                CreateIndividualTimer(key, DateTime.UtcNow.AddHours(1)); // Retry in 1 hour
                return;
            }

            // Actually remove the role
            try
            {
                await user.RemoveRoleAsync(role, new RequestOptions
                {
                    AuditLogReason = "Timed role expired"
                });

                logger.LogDebug("Successfully removed timed role {RoleName} from {UserName} in guild {GuildId}",
                    role.Name, user.DisplayName, guildId);

                // Success - clean up
                await CleanupRoleTimer(guildId, userId, roleId);
            }
            catch (HttpException httpEx) when (httpEx.DiscordCode == DiscordErrorCode.MissingPermissions)
            {
                logger.LogWarning(
                    "Missing permissions to remove role {RoleName} from user {UserId} in guild {GuildId}, will retry in 1 hour",
                    role.Name, userId, guildId);
                CreateIndividualTimer(key, DateTime.UtcNow.AddHours(1)); // Retry in 1 hour
            }
            catch (HttpException httpEx) when (httpEx.DiscordCode == DiscordErrorCode.UnknownRole)
            {
                logger.LogInformation("Role {RoleId} was deleted while processing timer in guild {GuildId}", roleId,
                    guildId);
                await CleanupRoleTimer(guildId, userId, roleId);
            }
            catch (HttpException httpEx) when (httpEx.DiscordCode == DiscordErrorCode.UnknownMember)
            {
                logger.LogInformation("User {UserId} left guild {GuildId} while processing timer", userId, guildId);
                await CleanupRoleTimer(guildId, userId, roleId);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Unexpected error processing role timer for guild {GuildId}, user {UserId}, role {RoleId}",
                key.GuildId, key.UserId, key.RoleId);
            // For unexpected errors, retry in 1 hour
            CreateIndividualTimer(key, DateTime.UtcNow.AddHours(1));
        }
    }

    private async Task CleanupRoleTimer(ulong guildId, ulong userId, ulong roleId)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();
        await dbContext.UnroleTimers
            .Where(x => x.GuildId == guildId && x.UserId == userId && x.RoleId == roleId)
            .DeleteAsync();
    }


    private async Task OnUserMuted(IGuildUser user, IUser mod, MuteType type, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return;

        await user.SendMessageAsync(embed: new EmbedBuilder()
            .WithDescription(strings.UserMutedDm(user.Guild.Id, user.Guild))
            .AddField("Mute Type", type.ToString())
            .AddField("Moderator", mod.ToString())
            .AddField("Reason", reason)
            .Build());
    }

    private async Task OnUserUnmuted(IGuildUser user, IUser mod, MuteType type, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return;

        await user.SendMessageAsync(embed: new EmbedBuilder()
            .WithDescription(strings.UserUnmutedDm(user.Guild.Id, user.Guild.Name))
            .AddField("Unmute Type", type.ToString())
            .AddField("Moderator", mod.ToString())
            .AddField("Reason", reason)
            .Build());
    }

    private async Task Client_UserJoined(IGuildUser? usr)
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
            logger.LogWarning(ex, "Error in MuteService UserJoined event");
        }
    }

    /// <summary>
    ///     Sets the mute role for a guild.
    /// </summary>
    /// <param name="guildId">The id of the guild to set the role in</param>
    /// <param name="name">The name of the role (What in your right fucking mind possessed you to make it this way kwoth???)</param>
    public async Task SetMuteRoleAsync(ulong guildId, string name)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        var config = await guildSettings.GetGuildConfig(guildId);
        config.MuteRoleName = name;
        GuildMuteRoles.AddOrUpdate(guildId, name, (_, _) => name);
        await guildSettings.UpdateGuildConfig(guildId, config);
    }

    /// <summary>
    ///     Flow for muting a user
    /// </summary>
    /// <param name="usr">The user to mute </param>
    /// <param name="mod">The mod who muted the user</param>
    /// <param name="type">The type of mute</param>
    /// <param name="reason">The mute reason</param>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public async Task MuteUser(IGuildUser? usr, IUser mod, MuteType type = MuteType.All, string reason = "")
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

                await using var dbContext = await dbFactory.CreateConnectionAsync();

                // Get user roles if needed for persistence
                var roles = usr.GetRoles().Where(p => p.Tags == null).Except([
                    usr.Guild.EveryoneRole
                ]);
                var enumerable = roles as IRole[] ?? roles.ToArray();
                var uroles = string.Join(" ", enumerable.Select(x => x.Id));

                // Remove any existing entry
                var existingMute = await dbContext.MutedUserIds
                    .FirstOrDefaultAsync(x => x.GuildId == usr.Guild.Id && x.UserId == usr.Id);

                if (existingMute != null)
                    await dbContext.MutedUserIds.Where(x => x.Id == existingMute.Id).DeleteAsync();

                // Create new entry based on settings
                var removeOnMute = await GetRemoveOnMute(usr.Guild.Id);
                var mutedUser = new MutedUserId
                {
                    GuildId = usr.Guild.Id, UserId = usr.Id, Roles = removeOnMute == 1 ? uroles : null
                };

                await dbContext.InsertAsync(mutedUser);

                if (MutedUsers.TryGetValue(usr.Guild.Id, out var muted))
                    muted.Add(usr.Id);

                // Remove any existing unmute timers
                var timersToRemove = dbContext.UnmuteTimers
                    .Where(x => x.GuildId == usr.Guild.Id && x.UserId == usr.Id);

                await timersToRemove.DeleteAsync();

                // Apply mute role
                var muteRole = await GetMuteRole(usr.Guild).ConfigureAwait(false);
                if (!usr.RoleIds.Contains(muteRole.Id))
                {
                    if (removeOnMute == 1)
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
    ///     gets whether roles should be removed on mute
    /// </summary>
    /// <param name="id">The server id</param>
    /// <returns></returns>
    public async Task<int> GetRemoveOnMute(ulong id)
    {
        return (await guildSettings.GetGuildConfig(id)).Removeroles;
    }

    /// <summary>
    ///     Sets whether roles should be removed on mute
    /// </summary>
    /// <param name="guild">The server to set this setting</param>
    /// <param name="yesnt">nosnt</param>
    public async Task Removeonmute(IGuild guild, string yesnt)
    {
        var yesno = -1;

        yesno = yesnt switch
        {
            "y" => 1,
            "n" => 0,
            _ => yesno
        };

        var gc = await guildSettings.GetGuildConfig(guild.Id);
        gc.Removeroles = yesno;
        await guildSettings.UpdateGuildConfig(guild.Id, gc);
    }

    /// <summary>
    ///     Flow for unmuting a user
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

                await using var dbContext = await dbFactory.CreateConnectionAsync();

                // Find the muted user directly
                var mutedUser = await dbContext.MutedUserIds
                    .FirstOrDefaultAsync(x => x.GuildId == guildId && x.UserId == usrId);

                if (usr != null && await GetRemoveOnMute(usr.Guild.Id) == 1 && mutedUser?.Roles != null)
                {
                    try
                    {
                        Uroles = mutedUser.Roles.Split(' ');

                        // Restore roles
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
                    catch (Exception)
                    {
                        // ignored
                    }
                }

                // Remove muted user entry
                if (mutedUser != null)
                    await dbContext.MutedUserIds.Where(x => x.Id == mutedUser.Id).DeleteAsync();

                if (MutedUsers.TryGetValue(guildId, out var muted))
                    muted.TryRemove(usrId);

                // Remove unmute timers by ID
                var timersToRemove = await dbContext.UnmuteTimers
                    .Where(x => x.GuildId == guildId && x.UserId == usrId)
                    .Select(x => x.Id)
                    .ToListAsync();

                foreach (var id in timersToRemove)
                {
                    await dbContext.UnmuteTimers.Where(x => x.Id == id).DeleteAsync();
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
    ///     Gets the mute role for a guild.
    /// </summary>
    /// <param name="guild">The guildid to get the mute role from</param>
    /// <returns>The mute <see cref="IRole" /></returns>
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
    ///     Mutes a user for a specified amount of time.
    /// </summary>
    /// <param name="user">The user to mute</param>
    /// <param name="mod">The mod who muted the user</param>
    /// <param name="after">The time to mute the user for</param>
    /// <param name="muteType">The type of mute</param>
    /// <param name="reason">The reason for the mute</param>
    public async Task TimedMute(IGuildUser? user, IUser mod, TimeSpan after, MuteType muteType = MuteType.All,
        string reason = "")
    {
        await MuteUser(user, mod, muteType, reason).ConfigureAwait(false);

        await using var dbContext = await dbFactory.CreateConnectionAsync();

        // Add unmute timer directly
        var unmuteTimer = new UnmuteTimer
        {
            GuildId = user.GuildId, UserId = user.Id, UnmuteAt = DateTime.UtcNow + after
        };

        await dbContext.InsertAsync(unmuteTimer);

        StartUn_Timer(user.GuildId, user.Id, after, TimerType.Mute);
    }

    /// <summary>
    ///     Bans a user for a specified amount of time.
    /// </summary>
    /// <param name="guild">The guild to ban the user from</param>
    /// <param name="user">The user to ban</param>
    /// <param name="after">The time to ban the user for</param>
    /// <param name="reason">The reason for the ban</param>
    /// <param name="todelete">The time to delete the ban message</param>
    public async Task TimedBan(IGuild guild, IUser? user, TimeSpan after, string reason, TimeSpan todelete = default)
    {
        var pruneDays = todelete == default
            ? await banPrune.GetPruneDaysAsync(guild.Id, BanPruneAction.TempBan).ConfigureAwait(false)
            : todelete.Days;

        await guild.AddBanAsync(user.Id, pruneDays, options: new RequestOptions
        {
            AuditLogReason = reason
        }).ConfigureAwait(false);

        await using var dbContext = await dbFactory.CreateConnectionAsync();

        // Add unban timer directly
        var unbanTimer = new UnbanTimer
        {
            GuildId = guild.Id, UserId = user.Id, UnbanAt = DateTime.UtcNow + after
        };

        await dbContext.InsertAsync(unbanTimer);
        StartUn_Timer(guild.Id, user.Id, after, TimerType.Ban);
    }

    /// <summary>
    ///     Adds a role to a user for a specified amount of time.
    /// </summary>
    /// <param name="user">The user to add the role to</param>
    /// <param name="after">The time to add the role for</param>
    /// <param name="reason">The reason for adding the role</param>
    /// <param name="role">The role to add</param>
    public async Task TimedRole(IGuildUser? user, TimeSpan after, string reason, IRole role)
    {
        await user.AddRoleAsync(role, new RequestOptions
        {
            AuditLogReason = reason
        }).ConfigureAwait(false);

        await using var dbContext = await dbFactory.CreateConnectionAsync();

        // Add unrole timer directly
        var unroleTimer = new UnroleTimer
        {
            GuildId = user.GuildId, UserId = user.Id, UnbanAt = DateTime.UtcNow + after, RoleId = role.Id
        };

        await dbContext.InsertAsync(unroleTimer);

        StartUn_Timer(user.GuildId, user.Id, after, TimerType.AddRole, role.Id);
    }

    /// <summary>
    ///     Starts a timer to unmute a user.
    /// </summary>
    /// <param name="guildId">The guildId where the user is unmuted</param>
    /// <param name="userId">The user to unmute</param>
    /// <param name="after">The time to unmute the user after</param>
    /// <param name="type">The type of timer</param>
    /// <param name="roleId">The role to remove</param>
    public void StartUn_Timer(ulong guildId, ulong userId, TimeSpan after, TimerType type, ulong? roleId = null)
    {
        var executeAt = DateTime.UtcNow + after;
        var key = new TimerKey(guildId, userId, type, roleId);
        CreateIndividualTimer(key, executeAt);
    }

    /// <summary>
    ///     Stops a timer for a user.
    /// </summary>
    /// <param name="guildId">The guildId where the timer is stopped</param>
    /// <param name="userId">The user to stop the timer for</param>
    /// <param name="type">The type of timer</param>
    /// <param name="roleId">Optional role ID for role-specific timers</param>
    public void StopTimer(ulong guildId, ulong userId, TimerType type, ulong? roleId = null)
    {
        var key = new TimerKey(guildId, userId, type, roleId);

        // Cancel and dispose the timer
        if (activeTimers.TryRemove(key, out var timer))
        {
            timer.Dispose();
            logger.LogDebug("Stopped timer for {TimerType} - Guild: {GuildId}, User: {UserId}, Role: {RoleId}",
                type, guildId, userId, roleId);
        }
    }

    /// <summary>
    ///     Dispose
    /// </summary>
    /// <param name="disposing">Depose</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            eventHandler.Unsubscribe("UserJoined", "MuteService", Client_UserJoined);

            // Dispose all active timers
            foreach (var timer in activeTimers.Values)
            {
                timer.Dispose();
            }

            activeTimers.Clear();

            logger.LogInformation("MuteService disposed and all timers cleaned up");
        }
    }

    private record struct TimerKey(ulong GuildId, ulong UserId, TimerType Type, ulong? RoleId = null);
}