using System.Threading;
using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Common.TypeReaders.Models;
using Mewdeko.Modules.Administration.Common;
using Mewdeko.Modules.Moderation.Common;

namespace Mewdeko.Modules.Moderation.Services;

/// <summary>
///     Secondary service for user punishment.
/// </summary>
public class UserPunishService2 : INService, IDisposable
{
    private readonly IDataConnectionFactory dbFactory;
    private readonly GuildSettingsService guildSettings;
    private readonly ILogger<UserPunishService2> logger;
    private readonly MuteService mute;
    private Timer? warnExpirationTimer;


    /// <summary>
    ///     Initializes a new instance of <see cref="UserPunishService2" />.
    /// </summary>
    /// <param name="mute">The mute service</param>
    /// <param name="dbFactory">The database service</param>
    /// <param name="guildSettings">The guild settings service</param>
    /// <param name="logger">The logger instance for structured logging.</param>
    public UserPunishService2(MuteService mute, IDataConnectionFactory dbFactory,
        GuildSettingsService guildSettings, ILogger<UserPunishService2> logger)
    {
        this.mute = mute;
        this.dbFactory = dbFactory;
        this.guildSettings = guildSettings;
        this.logger = logger;
        warnExpirationTimer = new Timer(async _ => await CheckAllWarnExpiresAsync().ConfigureAwait(false), null,
            TimeSpan.FromSeconds(0), TimeSpan.FromHours(12));
    }

    /// <summary>
    ///     Disposes the mini warn service and cleans up resources.
    /// </summary>
    public void Dispose()
    {
        warnExpirationTimer?.Dispose();
        warnExpirationTimer = null;
    }

    /// <summary>
    ///     Gets the channel ID for the mini warnlog.
    /// </summary>
    /// <param name="id">The guild ID</param>
    /// <returns>ulong of the warnlog channel</returns>
    public async Task<ulong> GetMWarnlogChannel(ulong id)
    {
        return (await guildSettings.GetGuildConfig(id)).MiniWarnlogChannelId;
    }

    /// <summary>
    ///     Sets the channel ID for the mini warnlog.
    /// </summary>
    /// <param name="guild">The guild</param>
    /// <param name="channel">The channel</param>
    public async Task SetMWarnlogChannelId(IGuild guild, ITextChannel channel)
    {
        var gc = await guildSettings.GetGuildConfig(guild.Id);
        gc.MiniWarnlogChannelId = channel.Id;
        await guildSettings.UpdateGuildConfig(guild.Id, gc);
    }

    /// <summary>
    ///     Warns a user.
    /// </summary>
    /// <param name="guild">The guild</param>
    /// <param name="userId">The user ID</param>
    /// <param name="mod">The moderator</param>
    /// <param name="reason">The reason</param>
    /// <returns></returns>
    public async Task<WarningPunishment2?>? Warn(IGuild guild, ulong userId, IUser mod, string reason)
    {
        var modName = mod.ToString();

        if (string.IsNullOrWhiteSpace(reason))
            reason = "-";

        var guildId = guild.Id;

        var warn2 = new Warnings2
        {
            UserId = userId, GuildId = guildId, Reason = reason, Moderator = modName
        };

        var warnings = 1;
        List<WarningPunishment2> ps;

        await using var dbContext = await dbFactory.CreateConnectionAsync();

        // Direct query with GuildId filter
        ps = await dbContext.WarningPunishment2s
            .Where(x => x.GuildId == guildId)
            .ToListAsync();

        // Count warnings directly using GuildId filter
        warnings += await dbContext.Warnings2s
            .CountAsync(w => w.GuildId == guildId && w.UserId == userId && !w.Forgiven);

        await dbContext.InsertAsync(warn2);

        var p = ps.Find(x => x.Count == warnings);

        if (p == null) return null;
        {
            var user = await guild.GetUserAsync(userId).ConfigureAwait(false);
            if (user == null)
                return null;
            switch ((PunishmentAction)p.Punishment)
            {
                case PunishmentAction.Mute:
                    if (p.Time == 0)
                        await mute.MuteUser(user, mod).ConfigureAwait(false);
                    else
                        await mute.TimedMute(user, mod, TimeSpan.FromMinutes(p.Time)).ConfigureAwait(false);
                    break;
                case PunishmentAction.VoiceMute:
                    if (p.Time == 0)
                        await mute.MuteUser(user, mod, MuteType.Voice).ConfigureAwait(false);
                    else
                        await mute.TimedMute(user, mod, TimeSpan.FromMinutes(p.Time), MuteType.Voice)
                            .ConfigureAwait(false);
                    break;
                case PunishmentAction.ChatMute:
                    if (p.Time == 0)
                        await mute.MuteUser(user, mod, MuteType.Chat).ConfigureAwait(false);
                    else
                        await mute.TimedMute(user, mod, TimeSpan.FromMinutes(p.Time), MuteType.Chat)
                            .ConfigureAwait(false);
                    break;
                case PunishmentAction.Kick:
                    await user.KickAsync("Warned too many times.").ConfigureAwait(false);
                    break;
                case PunishmentAction.Ban:
                    if (p.Time == 0)
                        await guild.AddBanAsync(user, options: new RequestOptions
                        {
                            AuditLogReason = "Warned too many times"
                        }).ConfigureAwait(false);
                    else
                        await mute.TimedBan(guild, user, TimeSpan.FromMinutes(p.Time), "Warned too many times.")
                            .ConfigureAwait(false);
                    break;
                case PunishmentAction.Softban:
                    await guild.AddBanAsync(user, 7, options: new RequestOptions
                    {
                        AuditLogReason = "Warned too many times"
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
                    await user.RemoveRolesAsync(user.GetRoles().Where(x => x.Id != guild.EveryoneRole.Id))
                        .ConfigureAwait(false);
                    break;
                case PunishmentAction.AddRole:
                    var role = guild.GetRole(p.RoleId.Value);
                    if (role is not null)
                        if (p.Time == 0)
                            await user.AddRoleAsync(role).ConfigureAwait(false);
                        else
                            await mute.TimedRole(user, TimeSpan.FromMinutes(p.Time), "Warned too many times.",
                                role).ConfigureAwait(false);
                    else
                        logger.LogWarning($"Warnpunish can't find role {p.RoleId.Value} on server {guild.Id}");

                    break;
            }

            return p;
        }
    }

    /// <summary>
    ///     Gets the number of warnings for a user.
    /// </summary>
    /// <param name="guild">The guild</param>
    /// <param name="userId">The user ID</param>
    /// <returns></returns>
    public async Task<int> GetWarnings(IGuild guild, ulong userId)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        return await dbContext.Warnings2s
            .CountAsync(w => !w.Forgiven && w.UserId == userId && w.GuildId == guild.Id);
    }

    /// <summary>
    ///     Checks all warnings for expiry.
    /// </summary>
    public async Task CheckAllWarnExpiresAsync()
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        var relevantGuildConfigsForClear = await dbContext.GuildConfigs
            .Where(gc => gc.MiniWarnExpireHours > 0 && gc.MiniWarnExpireAction == (int)WarnExpireAction.Clear)
            .ToListAsync();

        var cleared = 0;

        foreach (var gc in relevantGuildConfigsForClear)
        {
            var expireTime = DateTime.UtcNow.AddHours(-gc.MiniWarnExpireHours);
            cleared += await dbContext.Warnings2s
                .Where(w => w.GuildId == gc.GuildId && !w.Forgiven && w.DateAdded < expireTime)
                .Set(w => w.Forgiven, true)
                .Set(w => w.ForgivenBy, "Expiry")
                .UpdateAsync();
        }

        var relevantGuildConfigsForDelete = await dbContext.GuildConfigs
            .Where(gc => gc.MiniWarnExpireHours > 0 && gc.MiniWarnExpireAction == (int)WarnExpireAction.Delete)
            .ToListAsync();

        var deleted = 0;

        foreach (var gc in relevantGuildConfigsForDelete)
        {
            var expireTime = DateTime.UtcNow.AddHours(-gc.MiniWarnExpireHours);
            var warningsToDelete = await dbContext.Warnings2s
                .Where(w => w.GuildId == gc.GuildId && w.DateAdded < expireTime).DeleteAsync();

            deleted += warningsToDelete;
        }

        if (cleared > 0 || deleted > 0)
            logger.LogInformation($"Cleared {cleared} warnings and deleted {deleted} warnings due to expiry.");
    }

    /// <summary>
    ///     Checks the expiry of warnings for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    public async Task CheckWarnExpiresAsync(ulong guildId)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        var config = await guildSettings.GetGuildConfig(guildId);

        if (config.MiniWarnExpireHours == 0)
            return;

        var expiryDate = DateTime.UtcNow.AddHours(-config.MiniWarnExpireHours);

        switch ((WarnExpireAction)config.MiniWarnExpireAction)
        {
            case WarnExpireAction.Clear:
                await dbContext.Warnings2s
                    .Where(w => w.GuildId == guildId && !w.Forgiven && w.DateAdded < expiryDate)
                    .Set(w => w.Forgiven, true)
                    .Set(w => w.ForgivenBy, "Expiry")
                    .UpdateAsync();
                break;
            case WarnExpireAction.Delete:
                await dbContext.Warnings2s
                    .Where(w => w.GuildId == guildId && w.DateAdded < expiryDate).DeleteAsync();
                break;
        }
    }


    /// <summary>
    ///     Checks the expiry of warnings for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <param name="days">The number of days</param>
    /// <param name="action">Whether to delete the warnings</param>
    public async Task WarnExpireAsync(ulong guildId, int days, WarnExpireAction action)
    {
        var config = await guildSettings.GetGuildConfig(guildId);

        config.MiniWarnExpireHours = days * 24;
        config.MiniWarnExpireAction = (int)action;
        await guildSettings.UpdateGuildConfig(guildId, config);
        if (config.MiniWarnExpireHours == 0)
            return;

        await CheckWarnExpiresAsync(guildId).ConfigureAwait(false);
    }

    /// <summary>
    ///     Gets the amount of warnings every user has.
    /// </summary>
    /// <param name="gid"></param>
    /// <returns></returns>
    public async Task<IGrouping<ulong, Warnings2>[]> WarnlogAll(ulong gid)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        return await dbContext.Warnings2s.Where(x => x.GuildId == gid).GroupBy(x => x.UserId).ToArrayAsync();
    }

    /// <summary>
    ///     Gets the warnings for a user.
    /// </summary>
    /// <param name="gid">The guild ID</param>
    /// <param name="userId">The user ID</param>
    /// <returns></returns>
    public async Task<Warnings2[]> UserWarnings(ulong gid, ulong userId)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();
        return await dbContext.Warnings2s.Where(x => x.UserId == userId && x.GuildId == gid).ToArrayAsync();
    }

    /// <summary>
    ///     Clears a warning. If index is 0, clears all warnings.
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <param name="userId">The user ID</param>
    /// <param name="index">The warning index</param>
    /// <param name="moderator">The moderator</param>
    /// <returns></returns>
    public async Task<bool> WarnClearAsync(ulong guildId, ulong userId, int index, string moderator)
    {
        var toReturn = true;

        await using var dbContext = await dbFactory.CreateConnectionAsync();

        if (index == 0)
            await dbContext.Warnings2s.ForgiveAll(dbContext, guildId, userId, moderator).ConfigureAwait(false);
        else
            toReturn = await dbContext.Warnings2s.Forgive(dbContext, guildId, userId, moderator, index - 1);

        return toReturn;
    }

    /// <summary>
    ///     Sets what each warning count should do.
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <param name="number">The number of warnings</param>
    /// <param name="punish">The punishment</param>
    /// <param name="time">The time</param>
    /// <param name="role">The role</param>
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

        // Directly query WarnPunishments2 with GuildId filter
        await dbContext.WarningPunishment2s
            .Where(x => x.GuildId == guildId && x.Count == number).DeleteAsync();

        // Add new punishment with GuildId
        await dbContext.InsertAsync(new WarningPunishment2
        {
            GuildId = guildId,
            Count = number,
            Punishment = punish,
            Time = (int?)time?.Time.TotalMinutes ?? 0,
            RoleId = punish == (int)PunishmentAction.AddRole ? role?.Id : default
        });

        return true;
    }

    /// <summary>
    ///     Removes a warning punishment.
    /// </summary>
    /// <param name="guildId">The guild ID to remove the punishment from</param>
    /// <param name="number">The number of warnings</param>
    /// <returns></returns>
    public async Task<bool> WarnPunishRemove(ulong guildId, int number)
    {
        if (number <= 0)
            return false;

        await using var dbContext = await dbFactory.CreateConnectionAsync();

        // Directly query for the specific warn punishment
        var p = await dbContext.WarningPunishment2s
            .FirstOrDefaultAsync(x => x.GuildId == guildId && x.Count == number);

        if (p == null) return true;

        await dbContext.WarningPunishment2s
            .Where(x => x.GuildId == guildId && x.Count == number)
            .DeleteAsync();

        return true;
    }

    /// <summary>
    ///     Gets the warning punishments for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <returns></returns>
    public async Task<WarningPunishment2[]> WarnPunishList(ulong guildId)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        // Directly query WarnPunishments2 with GuildId filter
        return await dbContext.WarningPunishment2s
            .Where(x => x.GuildId == guildId)
            .OrderBy(x => x.Count)
            .ToArrayAsync();
    }
}