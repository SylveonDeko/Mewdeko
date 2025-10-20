using System.Text.Json;
using System.Threading;
using DataModel;
using Discord.Commands;
using LinqToDB;
using LinqToDB.Async;
using LinqToDB.Data;
using Mewdeko.Common.TypeReaders.Models;
using Mewdeko.Modules.Administration.Common;
using Mewdeko.Modules.Moderation.Common;
using Mewdeko.Modules.Permissions.Services;
using Embed = Discord.Embed;

namespace Mewdeko.Modules.Moderation.Services;

/// <summary>
///     Service for user punishments.
/// </summary>
public class UserPunishService : INService, IDisposable
{
    private readonly BlacklistService blacklistService;
    private readonly DiscordShardedClient client;
    private readonly IDataConnectionFactory dbFactory;
    private readonly GuildSettingsService guildSettings;
    private readonly ILogger<UserPunishService> logger;
    private readonly Dictionary<ulong, MassNick> massNicks = new();
    private readonly MuteService mute;
    private Timer? warnExpirationTimer;


    /// <summary>
    ///     Constructs a new instance of the UserPunishService class.
    /// </summary>
    /// <param name="mute">An instance of the MuteService class.</param>
    /// <param name="dbFactory">An instance of the dbContext class.</param>
    /// <param name="blacklistService">An instance of the BlacklistService class.</param>
    /// <param name="client">An instance of the DiscordShardedClient class.</param>
    /// <param name="guildSettings">An instance of the GuildSettingsService class.</param>
    /// <param name="logger">The logger instance for structured logging.</param>
    public UserPunishService(MuteService mute, IDataConnectionFactory dbFactory, BlacklistService blacklistService,
        DiscordShardedClient client,
        GuildSettingsService guildSettings, ILogger<UserPunishService> logger)
    {
        this.mute = mute;
        this.dbFactory = dbFactory;
        this.blacklistService = blacklistService;
        this.client = client;
        this.guildSettings = guildSettings;
        this.logger = logger;
        // Initializes a new Timer that checks all warning expirations every 12 hours
        warnExpirationTimer = new Timer(async _ => await CheckAllWarnExpiresAsync().ConfigureAwait(false), null,
            TimeSpan.FromSeconds(0), TimeSpan.FromHours(12));
    }

    /// <summary>
    ///     Disposes the user punish service and cleans up resources.
    /// </summary>
    public void Dispose()
    {
        warnExpirationTimer?.Dispose();
        warnExpirationTimer = null;
    }

    /// <summary>
    ///     Adds a new MassNick to the collection if it doesn't already exist for the given guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="user">The user who started the operation.</param>
    /// <param name="total">The total number of operations to be performed.</param>
    /// <param name="operationType">The type of operation to be performed.</param>
    /// <param name="returnMassNick">
    ///     The MassNick object that was added to the collection. If a MassNick already exists for the
    ///     guild, this will be null.
    /// </param>
    /// <returns>True if a new MassNick was added to the collection, false otherwise.</returns>
    public bool AddMassNick(ulong guildId, IUser user, int total, string operationType, out MassNick returnMassNick)
    {
        if (massNicks.TryGetValue(guildId, out _))
        {
            returnMassNick = null;
            return false;
        }

        var massNick = new MassNick
        {
            StartedBy = user, Total = total, OperationType = operationType, StartedAt = DateTime.UtcNow
        };
        massNicks.Add(guildId, massNick);
        returnMassNick = massNick;
        return true;
    }

    /// <summary>
    ///     Retrieves the MassNick object associated with the specified guild ID.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <returns>The MassNick object if found; otherwise, null.</returns>
    public MassNick? GetMassNick(ulong guildId)
    {
        return !massNicks.TryGetValue(guildId, out var massNick) ? null : massNick;
    }

    /// <summary>
    ///     Removes the MassNick object associated with the specified guild ID.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <returns>True if the MassNick object was successfully removed; otherwise, false.</returns>
    public bool RemoveMassNick(ulong guildId)
    {
        return massNicks.Remove(guildId);
    }

    /// <summary>
    ///     Updates the MassNick object associated with the specified guild ID.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="failed">Indicates whether the operation failed.</param>
    /// <param name="changed">Indicates whether the operation changed the state.</param>
    /// <param name="stopped">Indicates whether the operation was stopped. Defaults to false.</param>
    public void UpdateMassNick(ulong guildId, bool failed, bool changed, bool stopped = false)
    {
        if (!massNicks.TryGetValue(guildId, out var massNick))
            return;

        if (failed)
            massNick.Failed++;
        if (changed)
            massNick.Changed++;
        if (stopped)
            massNick.Stopped = true;

        massNicks[guildId] = massNick;
    }

    /// <summary>
    ///     Retrieves the ID of the Warnlog channel for the specified guild.
    /// </summary>
    /// <param name="id">The ID of the guild.</param>
    /// <returns>The ID of the Warnlog channel.</returns>
    public async Task<ulong> GetWarnlogChannel(ulong id)
    {
        return (await guildSettings.GetGuildConfig(id)).WarnlogChannelId;
    }

    /// <summary>
    ///     Sets the ID of the Warnlog channel for the specified guild.
    /// </summary>
    /// <param name="guild">The guild for which to set the Warnlog channel.</param>
    /// <param name="channel">The channel to set as the Warnlog channel.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task SetWarnlogChannelId(IGuild guild, ITextChannel channel)
    {
        var gc = await guildSettings.GetGuildConfig(guild.Id);
        gc.WarnlogChannelId = channel.Id;
        await guildSettings.UpdateGuildConfig(guild.Id, gc);
    }

    /// <summary>
    ///     Issues a warning to a user in a guild.
    /// </summary>
    /// <param name="guild">The guild where the warning is issued.</param>
    /// <param name="userId">The ID of the user to be warned.</param>
    /// <param name="mod">The user who issues the warning.</param>
    /// <param name="reason">The reason for the warning.</param>
    /// <returns>A WarningPunishment object if a punishment is applied due to the warning; otherwise, null.</returns>
    public async Task<WarningPunishment>? Warn(IGuild guild, ulong userId, IUser mod, string reason)
    {
        var modName = mod.ToString();

        if (string.IsNullOrWhiteSpace(reason))
            reason = "-";

        var guildId = guild.Id;

        // Create a new warning
        var warn = new Warning
        {
            UserId = userId,
            GuildId = guildId,
            Forgiven = false,
            Reason = reason,
            Moderator = modName
        };

        var warnings = 1;

        await using var dbContext = await dbFactory.CreateConnectionAsync();

        // Get the list of punishments for the guild
        var ps = await dbContext.WarningPunishments
            .Where(x => x.GuildId == guildId)
            .ToListAsync();

        // Count the number of warnings for the user
        warnings += await dbContext.Warnings
            .CountAsync(w => w.GuildId == guildId && w.UserId == userId && !w.Forgiven);

        // Add the new warning to the database
        await dbContext.InsertAsync(warn);

        // Find a punishment that matches the number of warnings
        var p = ps.Find(x => x.Count == warnings);

        if (p == null) return null;
        var user = await guild.GetUserAsync(userId).ConfigureAwait(false);
        if (user == null)
            return null;

        // Apply the punishment
        await ApplyPunishment(guild, user, mod, (PunishmentAction)p.Punishment, p.Time, p.RoleId,
                "Warned too many times.")
            .ConfigureAwait(false);
        return p;
    }

    /// <summary>
    ///     Applies a punishment to a user in a guild.
    /// </summary>
    /// <param name="guild">The guild where the punishment is applied.</param>
    /// <param name="user">The user to be punished.</param>
    /// <param name="mod">The user who issues the punishment.</param>
    /// <param name="p">The punishment to be applied.</param>
    /// <param name="minutes">The duration of the punishment in minutes.</param>
    /// <param name="roleId">The ID of the role to be applied.</param>
    /// <param name="reason">The reason for the punishment.</param>
    public async Task ApplyPunishment(IGuild guild, IGuildUser? user, IUser mod, PunishmentAction p, int minutes,
        ulong? roleId, string? reason)
    {
        reason ??= "None Specified";
        switch (p)
        {
            case PunishmentAction.Mute:
                if (minutes == 0)
                {
                    await mute.MuteUser(user, mod, reason: reason).ConfigureAwait(false);
                }
                else
                {
                    await mute.TimedMute(user, mod, TimeSpan.FromMinutes(minutes), reason: reason)
                        .ConfigureAwait(false);
                }

                break;
            case PunishmentAction.VoiceMute:
                if (minutes == 0)
                {
                    await mute.MuteUser(user, mod, MuteType.Voice, reason).ConfigureAwait(false);
                }
                else
                {
                    await mute.TimedMute(user, mod, TimeSpan.FromMinutes(minutes), MuteType.Voice, reason)
                        .ConfigureAwait(false);
                }

                break;
            case PunishmentAction.ChatMute:
                if (minutes == 0)
                {
                    await mute.MuteUser(user, mod, MuteType.Chat, reason).ConfigureAwait(false);
                }
                else
                {
                    await mute.TimedMute(user, mod, TimeSpan.FromMinutes(minutes), MuteType.Chat, reason)
                        .ConfigureAwait(false);
                }

                break;
            case PunishmentAction.Kick:
                await user.KickAsync(reason).ConfigureAwait(false);
                break;
            case PunishmentAction.Ban:
                if (minutes == 0)
                {
                    await guild.AddBanAsync(user, options: new RequestOptions
                    {
                        AuditLogReason = reason
                    }).ConfigureAwait(false);
                }
                else
                {
                    await mute.TimedBan(user.Guild, user, TimeSpan.FromMinutes(minutes), reason)
                        .ConfigureAwait(false);
                }

                break;
            case PunishmentAction.Softban:
                await guild.AddBanAsync(user, 7, options: new RequestOptions
                {
                    AuditLogReason = $"Softban | {reason}"
                }).ConfigureAwait(false);
                try
                {
                    await guild.RemoveBanAsync(user).ConfigureAwait(false);
                }
                catch
                {
                    await guild.RemoveBanAsync(user).ConfigureAwait(false);
                }

                break;
            case PunishmentAction.RemoveRoles:
                await user.RemoveRolesAsync(user.GetRoles().Where(x => !x.IsManaged && x != x.Guild.EveryoneRole))
                    .ConfigureAwait(false);
                break;
            case PunishmentAction.AddRole:
                if (roleId is null)
                    return;
                var role = guild.GetRole(roleId.Value);
                if (role is not null)
                {
                    if (minutes == 0)
                    {
                        await user.AddRoleAsync(role).ConfigureAwait(false);
                    }
                    else
                    {
                        await mute.TimedRole(user, TimeSpan.FromMinutes(minutes), reason, role)
                            .ConfigureAwait(false);
                    }
                }
                else
                {
                    logger.LogWarning($"Can't find role {roleId.Value} on server {guild.Id} to apply punishment.");
                }

                break;
            case PunishmentAction.Warn:
                await Warn(guild, user.Id, client.CurrentUser, reason).ConfigureAwait(false);
                break;
            case PunishmentAction.Timeout:
                try
                {
                    await user.SetTimeOutAsync(TimeSpan.FromMinutes(minutes), new RequestOptions
                    {
                        AuditLogReason = reason
                    }).ConfigureAwait(false);
                }
                catch
                {
                    logger.LogWarning(
                        $"Unable to apply timeout to user {user} in Guild {guild} due to missing permissions");
                }

                break;
        }
    }

    /// <summary>
    ///     Checks all warnings for expiry.
    /// </summary>
    private async Task CheckAllWarnExpiresAsync()
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        // Get only guild configs that have warn expiry configured
        var relevantGuildConfigs = await dbContext.GuildConfigs
            .Where(g => g.WarnExpireHours > 0)
            .Select(g => new
            {
                g.GuildId, g.WarnExpireHours, g.WarnExpireAction
            })
            .ToListAsync();

        // Process warnings to be forgiven (WarnExpireAction.Clear)
        var warningIdsToClear = new List<int>();
        var now = DateTime.UtcNow;

        foreach (var config in relevantGuildConfigs.Where(g => g.WarnExpireAction == (int)WarnExpireAction.Clear))
        {
            var expiredWarnings = await dbContext.Warnings
                .Where(w => w.GuildId == config.GuildId &&
                            !w.Forgiven &&
                            w.DateAdded < now.AddHours(-config.WarnExpireHours))
                .Select(w => w.Id)
                .ToListAsync();

            warningIdsToClear.AddRange(expiredWarnings);
        }

        // Process warnings to be deleted (WarnExpireAction.Delete)
        var warningIdsToDelete = new List<int>();

        foreach (var config in relevantGuildConfigs.Where(g => g.WarnExpireAction == (int)WarnExpireAction.Delete))
        {
            var expiredWarnings = await dbContext.Warnings
                .Where(w => w.GuildId == config.GuildId &&
                            w.DateAdded < now.AddHours(-config.WarnExpireHours))
                .Select(w => w.Id)
                .ToListAsync();

            warningIdsToDelete.AddRange(expiredWarnings);
        }

        // Now update only the warnings that need changes
        if (warningIdsToClear.Count != 0)
        {
            // Fetch only the warnings we need to update
            var warningsToClear = await dbContext.Warnings
                .Where(w => warningIdsToClear.Contains(w.Id))
                .ToListAsync();

            foreach (var warning in warningsToClear)
            {
                warning.Forgiven = true;
                warning.ForgivenBy = "Expiry";
            }
        }

        if (warningIdsToDelete.Count != 0)
        {
            // Fetch only the warnings we need to delete
            var warningsToDelete = dbContext.Warnings
                .Where(w => warningIdsToDelete.Contains(w.Id));

            await warningsToDelete.DeleteAsync();
        }

        if (warningIdsToClear.Count > 0 || warningIdsToDelete.Count > 0)
            logger.LogInformation("Cleared {Cleared} warnings and deleted {Deleted} warnings due to expiry",
                warningIdsToClear.Count, warningIdsToDelete.Count);
    }

    /// <summary>
    ///     Checks the expiry of warnings for a guild.
    /// </summary>
    /// <param name="guildId"></param>
    private async Task CheckWarnExpiresAsync(ulong guildId)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();
        var config = await guildSettings.GetGuildConfig(guildId);

        if (config.WarnExpireHours == 0)
            return;

        var interval = -config.WarnExpireHours;

        switch ((WarnExpireAction)config.WarnExpireAction)
        {
            case WarnExpireAction.Clear:
                await dbContext.ExecuteAsync($"""
                                              UPDATE "Warnings"
                                                          SET "Forgiven" = 1,
                                                              "ForgivenBy" = 'Expiry'
                                                          WHERE "GuildId"={guildId}
                                                              AND "Forgiven" = 0
                                                              AND "DateAdded" < NOW() - MAKE_INTERVAL(hours := {interval})
                                              """).ConfigureAwait(false);
                break;
            case WarnExpireAction.Delete:
                await dbContext.ExecuteAsync($"""
                                              DELETE FROM "Warnings"
                                                          WHERE "GuildId"={guildId}
                                                              AND "DateAdded" < NOW() - MAKE_INTERVAL(hours := {interval})
                                              """).ConfigureAwait(false);
                break;
        }
    }


    /// <summary>
    ///     Sets the expiry of warnings for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="days">The number of days before warnings expire.</param>
    /// <param name="action">Indicates whether to delete warnings after expiry.</param>
    public async Task WarnExpireAsync(ulong guildId, int days, WarnExpireAction action)
    {
        try
        {
            await using var dbContext = await dbFactory.CreateConnectionAsync();
            var config = await guildSettings.GetGuildConfig(guildId);

            config.WarnExpireHours = days * 24;
            config.WarnExpireAction = (int)action;

            // no need to check for warn expires
            if (config.WarnExpireHours == 0)
                return;

            await CheckWarnExpiresAsync(guildId).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    /// <summary>
    ///     Retrieves the warnings for a guild.
    /// </summary>
    /// <param name="gid">The ID of the guild.</param>
    /// <returns></returns>
    public async Task<IGrouping<ulong, Warning>[]> WarnlogAll(ulong gid)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();
        // Use AsEnumerable() to move GroupBy to client-side
        return dbContext.Warnings.Where(x => x.GuildId == gid).AsEnumerable().GroupBy(x => x.UserId).ToArray();
    }

    /// <summary>
    ///     Retrieves all warnings for a guild without grouping.
    /// </summary>
    /// <param name="gid">The ID of the guild.</param>
    /// <returns></returns>
    public async Task<Warning[]> GetAllWarnings(ulong gid)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();
        return await dbContext.Warnings
            .Where(x => x.GuildId == gid)
            .OrderByDescending(x => x.DateAdded)
            .ToArrayAsync();
    }

    /// <summary>
    ///     Retrieves the warnings for a user in a guild.
    /// </summary>
    /// <param name="gid">The ID of the guild.</param>
    /// <param name="userId">The ID of the user.</param>
    /// <returns></returns>
    public async Task<Warning[]> UserWarnings(ulong gid, ulong userId)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();
        return await dbContext.Warnings.Where(x => x.UserId == userId && x.GuildId == gid).ToArrayAsync();
    }

    /// <summary>
    ///     Clears all warnings or a specific warning for a user in a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="index">The index of the warning to clear.</param>
    /// <param name="moderator">The user who clears the warning.</param>
    /// <returns></returns>
    public async Task<bool> WarnClearAsync(ulong guildId, ulong userId, int index, string moderator)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();
        var toReturn = true;

        if (index == 0)
            await dbContext.Warnings.ForgiveAll(dbContext, guildId, userId, moderator).ConfigureAwait(false);
        else
            toReturn = await dbContext.Warnings.Forgive(dbContext, guildId, userId, moderator, index - 1);

        return toReturn;
    }

    /// <summary>
    ///     Sets what each warn number should do.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="number">The number of warnings.</param>
    /// <param name="punish">The punishment to apply.</param>
    /// <param name="time">The duration of the punishment.</param>
    /// <param name="role">The role to apply.</param>
    /// <returns></returns>
    public async Task<bool> WarnPunish(ulong guildId, int number, int punish, StoopidTime? time,
        IRole? role = null)
    {
        // these 3 don't make sense with time
        if ((PunishmentAction)punish is PunishmentAction.Softban or PunishmentAction.Kick
            or PunishmentAction.RemoveRoles && time != null)
            return false;
        if (number <= 0 || time != null && time.Time > TimeSpan.FromDays(49))
            return false;

        await using var dbContext = await dbFactory.CreateConnectionAsync();

        // Directly query WarnPunishments with GuildId filter
        var toDelete = dbContext.WarningPunishments
            .Where(x => x.GuildId == guildId && x.Count == number);

        await toDelete.DeleteAsync();

        // Add new punishment with GuildId
        await dbContext.InsertAsync(new WarningPunishment
        {
            GuildId = guildId,
            Count = number,
            Punishment = punish,
            Time = (int?)time?.Time.TotalMinutes ?? 0,
            RoleId = punish == (int)PunishmentAction.AddRole ? role?.Id : null
        });

        return true;
    }

    /// <summary>
    ///     Removes a warning punishment.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="number">The number of warnings.</param>
    /// <returns></returns>
    public async Task<bool> WarnPunishRemove(ulong guildId, int number)
    {
        if (number <= 0)
            return false;

        await using var dbContext = await dbFactory.CreateConnectionAsync();

        // Directly query for the specific warn punishment
        var p = await dbContext.WarningPunishments
            .FirstOrDefaultAsync(x => x.GuildId == guildId && x.Count == number);

        if (p != null)
        {
            await dbContext.WarningPunishments.Select(x => p).DeleteAsync();
        }

        return true;
    }


    /// <summary>
    ///     Retrieves the list of warning punishments for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <returns></returns>
    public async Task<WarningPunishment[]> WarnPunishList(ulong guildId)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        // Directly query WarnPunishments with GuildId filter
        return await dbContext.WarningPunishments
            .Where(x => x.GuildId == guildId)
            .OrderBy(x => x.Count)
            .ToArrayAsync();
    }

    /// <summary>
    ///     Mass bans users from a guild. And blacklists them.
    /// </summary>
    /// <param name="guild">The guild to ban users from.</param>
    /// <param name="people">The list of users to ban.</param>
    /// <returns></returns>
    public (IEnumerable<(string Original, ulong? id, string Reason)> Bans, int Missing) MassKill(SocketGuild guild,
        string people)
    {
        var gusers = guild.Users;
        //get user objects and reasons
        var bans = people.Split("\n")
            .Select(x =>
            {
                var split = x.Trim().Split(" ");

                var reason = string.Join(" ", split.Skip(1));

                if (ulong.TryParse(split[0], out var id))
                    return (Original: split[0], Id: id, Reason: reason);

                return (Original: split[0],
                    gusers
                        .FirstOrDefault(u => u.ToString().ToLowerInvariant() == x)
                        ?.Id,
                    Reason: reason);
            })
            .ToArray();

        //if user is null, means that person couldn't be found
        var missing = bans
            .Count(x => !x.Id.HasValue);

        //get only data for found users
        var found = bans
            .Where(x => x.Id.HasValue)
            .Select(x => x.Id.Value)
            .ToList();

        _ = blacklistService.BlacklistUsers(found);

        return (bans, missing);
    }

    /// <summary>
    ///     Gets the dm ban message for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <returns></returns>
    public async Task<string?> GetBanTemplate(ulong guildId)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();
        var template = dbContext.BanTemplates
            .AsQueryable()
            .FirstOrDefault(x => x.GuildId == guildId);
        return template?.Text;
    }


    /// <summary>
    ///     Sets the dm ban message for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="text">The message to set.</param>
    public async Task SetBanTemplate(ulong guildId, string? text)
    {
        try
        {
            await using var dbContext = await dbFactory.CreateConnectionAsync();
            var template = dbContext.BanTemplates
                .AsQueryable()
                .FirstOrDefault(x => x.GuildId == guildId);

            if (text == null)
            {
                if (template is null)
                    return;

                await dbContext.BanTemplates.Select(x => template).DeleteAsync();
            }
            else if (template == null)
            {
                await dbContext.InsertAsync(new BanTemplate
                {
                    GuildId = guildId, Text = text
                });
            }
            else
            {
                template.Text = text;
                await dbContext.UpdateAsync(template);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unable to set ban template for guild {GuildId}", guildId);
        }
    }

    /// <summary>
    ///     Gets the dm ban embed for a user.
    /// </summary>
    /// <param name="context">The context of the command.</param>
    /// <param name="target">The user to ban.</param>
    /// <param name="defaultMessage">The default message to send.</param>
    /// <param name="banReason">The reason for the ban.</param>
    /// <param name="duration">The duration of the ban.</param>
    /// <returns></returns>
    public Task<(Embed[]?, string?, ComponentBuilder?)> GetBanUserDmEmbed(ICommandContext context, IGuildUser? target,
        string? defaultMessage,
        string? banReason, TimeSpan? duration)
    {
        return GetBanUserDmEmbed(
            (DiscordShardedClient)context.Client,
            (SocketGuild)context.Guild,
            (IGuildUser)context.User,
            target,
            defaultMessage,
            banReason,
            duration);
    }

    /// <summary>
    ///     Gets the dm ban embed for a user.
    /// </summary>
    /// <param name="context">The context of the command.</param>
    /// <param name="target">The user to ban.</param>
    /// <param name="defaultMessage">The default message to send.</param>
    /// <param name="banReason">The reason for the ban.</param>
    /// <param name="duration">The duration of the ban.</param>
    /// <returns></returns>
    public Task<(Embed[]?, string?, ComponentBuilder?)> GetBanUserDmEmbed(IInteractionContext context,
        IGuildUser? target, string? defaultMessage,
        string? banReason, TimeSpan? duration)
    {
        return GetBanUserDmEmbed(
            (DiscordShardedClient)context.Client,
            (SocketGuild)context.Guild,
            (IGuildUser)context.User,
            target,
            defaultMessage,
            banReason,
            duration);
    }

    /// <summary>
    ///     Gets the dm ban embed for a user.
    /// </summary>
    /// <param name="DiscordShardedClient">The DiscordShardedClient instance.</param>
    /// <param name="guild">The guild where the ban is issued.</param>
    /// <param name="moderator">The user who issues the ban.</param>
    /// <param name="target">The user to ban.</param>
    /// <param name="defaultMessage">The default message to send.</param>
    /// <param name="banReason">The reason for the ban.</param>
    /// <param name="duration">The duration of the ban.</param>
    /// <returns></returns>
    public async Task<(Embed[], string?, ComponentBuilder?)> GetBanUserDmEmbed(
        DiscordShardedClient DiscordShardedClient,
        SocketGuild guild,
        IGuildUser moderator, IGuildUser? target, string? defaultMessage, string? banReason, TimeSpan? duration)
    {
        var template = await GetBanTemplate(guild.Id);

        banReason = string.IsNullOrWhiteSpace(banReason)
            ? "-"
            : banReason;

        var replacer = new ReplacementBuilder()
            .WithServer(DiscordShardedClient, guild)
            .WithOverride("%ban.mod%", moderator.ToString)
            .WithOverride("%ban.mod.fullname%", moderator.ToString)
            .WithOverride("%ban.mod.name%", () => moderator.Username)
            .WithOverride("%ban.mod.discrim%", () => moderator.Discriminator)
            .WithOverride("%ban.user%", target.ToString)
            .WithOverride("%ban.user.fullname%", target.ToString)
            .WithOverride("%ban.user.name%", () => target.Username)
            .WithOverride("%ban.user.discrim%", () => target.Discriminator)
            .WithOverride("%reason%", () => banReason)
            .WithOverride("%ban.reason%", () => banReason)
            .WithOverride("%ban.duration%", () => duration?.ToString(@"d\.hh\:mm") ?? "perma")
            .Build();
        Embed[] embed;
        ComponentBuilder components;
        string plainText;
        // if template isn't set, use the old message style
        if (string.IsNullOrWhiteSpace(template))
        {
            template = JsonSerializer.Serialize(new
            {
                color = Mewdeko.ErrorColor.RawValue, description = defaultMessage
            });

            SmartEmbed.TryParse(replacer.Replace(template), guild?.Id, out embed, out plainText, out components);
        }
        // if template is set to "-" do not dm the user
        else if (template == "-")
        {
            return (null, null, null);
        }
        // otherwise, treat template as a regular string with replacements
        else
        {
            if (SmartEmbed.TryParse(replacer.Replace(template), guild?.Id, out embed, out plainText, out components)
                && (embed is not null || components is not null || plainText is not null))
                return (embed, plainText, components);
            return (
            [
                new EmbedBuilder().WithErrorColor().WithDescription(replacer.Replace(template)).Build()
            ], null, null);
        }

        return (embed, plainText, components);
    }

    /// <summary>
    ///     The massnick object for the list of massnicks.
    /// </summary>
    public class MassNick
    {
        /// <summary>
        ///     The user who started the operation.
        /// </summary>
        public IUser StartedBy { get; set; }

        /// <summary>
        ///     The number of successful changes.
        /// </summary>
        public int Changed { get; set; }

        /// <summary>
        ///     The number of failed changes.
        /// </summary>
        public int Failed { get; set; }

        /// <summary>
        ///     The total number of operations to be performed.
        /// </summary>
        public int Total { get; set; }

        /// <summary>
        ///     Indicates whether the operation was stopped.
        /// </summary>
        public bool Stopped { get; set; }

        /// <summary>
        ///     The time the operation started.
        /// </summary>
        public DateTime StartedAt { get; set; }

        /// <summary>
        ///     The type of operation to be performed.
        /// </summary>
        public string OperationType { get; set; }
    }
}