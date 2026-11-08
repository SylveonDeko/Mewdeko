using System.Threading;
using DataModel;
using Discord.Net;
using LinqToDB;
using LinqToDB.Async;
using LinqToDB.Data;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Modules.Moderation.Services;
using Mewdeko.Services.Strings;

namespace Mewdeko.Modules.Administration.Services;

/// <summary>
///     Why a set of join role timeout settings was refused.
/// </summary>
public enum JoinRoleTimeoutError
{
    /// <summary>
    ///     Nothing is wrong.
    /// </summary>
    None,

    /// <summary>
    ///     The timeout is outside 1 minute to 30 days.
    /// </summary>
    InvalidTimeout,

    /// <summary>
    ///     The warning time is not between 1 minute and the timeout.
    /// </summary>
    InvalidWarn,

    /// <summary>
    ///     The add role action has no role.
    /// </summary>
    MissingActionRole,

    /// <summary>
    ///     The timeout is on without any required role.
    /// </summary>
    MissingRequiredRoles
}

/// <summary>
///     Acts on members who still lack a required role some time after joining, such as kicking people who never
///     accepted the rules.
/// </summary>
public class JoinRoleTimeoutService : INService, IReadyExecutor
{
    /// <summary>
    ///     The longest timeout allowed, in minutes.
    /// </summary>
    public const int MaxTimeoutMinutes = 43200;

    private const int SeedCap = 50000;
    private const int BatchSize = 200;

    private readonly DiscordShardedClient client;
    private readonly IDataConnectionFactory dbFactory;
    private readonly ILogger<JoinRoleTimeoutService> logger;
    private readonly ModCaseService modCases;
    private readonly GuildOwnershipService ownership;
    private readonly ConcurrentDictionary<ulong, JoinRoleTimeoutSetting> settings = new();
    private readonly GeneratedBotStrings strings;

    /// <summary>
    ///     Initializes a new instance of the <see cref="JoinRoleTimeoutService" /> class.
    /// </summary>
    /// <param name="client">The Discord client.</param>
    /// <param name="dbFactory">The database connection factory.</param>
    /// <param name="eventHandler">The event handler.</param>
    /// <param name="strings">The generated bot strings.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="modCases">The service recording moderation cases.</param>
    /// <param name="ownership">The guild ownership service.</param>
    public JoinRoleTimeoutService(DiscordShardedClient client, IDataConnectionFactory dbFactory,
        EventHandler eventHandler, GeneratedBotStrings strings, ILogger<JoinRoleTimeoutService> logger,
        ModCaseService modCases, GuildOwnershipService ownership)
    {
        this.ownership = ownership;
        this.modCases = modCases;
        this.client = client;
        this.dbFactory = dbFactory;
        this.strings = strings;
        this.logger = logger;
        eventHandler.Subscribe("UserJoined", "JoinRoleTimeoutService", OnUserJoined);
        eventHandler.Subscribe("UserLeft", "JoinRoleTimeoutService", OnUserLeft);
        eventHandler.Subscribe("GuildMemberUpdated", "JoinRoleTimeoutService", OnGuildMemberUpdated);
    }

    /// <summary>
    ///     Starts the loop that warns and acts on members whose time is up.
    /// </summary>
    public Task OnReadyAsync()
    {
        _ = Task.Run(RunLoop);
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Splits a stored space separated list of IDs.
    /// </summary>
    /// <param name="value">The stored value.</param>
    public static List<ulong> ParseIds(string? value) =>
        (value ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(x => ulong.TryParse(x, out var id) ? id : 0)
        .Where(x => x != 0)
        .Distinct()
        .ToList();

    /// <summary>
    ///     Joins IDs into the stored space separated form.
    /// </summary>
    /// <param name="ids">The IDs.</param>
    public static string JoinIds(IEnumerable<ulong> ids) => string.Join(' ', ids.Distinct());

    /// <summary>
    ///     Writes minutes as days, hours and minutes.
    /// </summary>
    /// <param name="minutes">The number of minutes.</param>
    public static string FormatMinutes(int minutes)
    {
        var span = TimeSpan.FromMinutes(minutes);
        var parts = new List<string>();
        if (span.Days > 0)
            parts.Add($"{span.Days}d");
        if (span.Hours > 0)
            parts.Add($"{span.Hours}h");
        if (span.Minutes > 0)
            parts.Add($"{span.Minutes}m");
        return parts.Count == 0 ? "0m" : string.Join(' ', parts);
    }

    /// <summary>
    ///     Checks a set of settings.
    /// </summary>
    /// <param name="setting">The settings to check.</param>
    public static JoinRoleTimeoutError Validate(JoinRoleTimeoutSetting setting)
    {
        if (setting.TimeoutMinutes is < 1 or > MaxTimeoutMinutes)
            return JoinRoleTimeoutError.InvalidTimeout;
        if (setting.WarnBeforeMinutes is { } warn && (warn < 1 || warn >= setting.TimeoutMinutes))
            return JoinRoleTimeoutError.InvalidWarn;
        if (setting.Action == JoinRoleTimeoutAction.AddRole && setting.ActionRoleId is null)
            return JoinRoleTimeoutError.MissingActionRole;
        if (setting.Enabled && ParseIds(setting.RequiredRoleIds).Count == 0)
            return JoinRoleTimeoutError.MissingRequiredRoles;
        return JoinRoleTimeoutError.None;
    }

    /// <summary>
    ///     Gets a guild's settings, or the defaults when it has none.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    public async Task<JoinRoleTimeoutSetting> GetSettingsAsync(ulong guildId)
    {
        if (settings.TryGetValue(guildId, out var cached))
            return cached;

        await using var db = await dbFactory.CreateConnectionAsync();
        var row = await db.JoinRoleTimeoutSettings.FirstOrDefaultAsync(x => x.GuildId == guildId)
                  ?? new JoinRoleTimeoutSetting
                  {
                      GuildId = guildId, DateAdded = DateTime.UtcNow
                  };
        settings[guildId] = row;
        return row;
    }

    /// <summary>
    ///     Changes a guild's settings. Turning the timeout off clears the countdowns, and turning it on with existing
    ///     members included starts the countdown for members who already lack a required role.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="change">Applies the change to a copy of the settings.</param>
    /// <returns>The error, or none, and the settings as they are now.</returns>
    public async Task<(JoinRoleTimeoutError Error, JoinRoleTimeoutSetting Setting)> UpdateAsync(ulong guildId,
        Action<JoinRoleTimeoutSetting> change)
    {
        var current = await GetSettingsAsync(guildId);
        var copy = Clone(current);
        change(copy);

        var error = Validate(copy);
        if (error != JoinRoleTimeoutError.None)
            return (error, current);

        await using var db = await dbFactory.CreateConnectionAsync();
        var exists = await db.JoinRoleTimeoutSettings.AnyAsync(x => x.GuildId == guildId);
        if (exists)
            await db.UpdateAsync(copy);
        else
            await db.InsertAsync(copy);

        settings[guildId] = copy;

        if (current.Enabled && !copy.Enabled)
            await db.JoinRoleTimeoutPending.Where(x => x.GuildId == guildId).DeleteAsync();
        else if (copy.Enabled && copy.IncludeExistingMembers && (!current.Enabled || !current.IncludeExistingMembers))
            _ = Task.Run(() => SeedExistingAsync(guildId));

        return (JoinRoleTimeoutError.None, copy);
    }

    /// <summary>
    ///     Counts the members counting down in a guild and lists the ones due next.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="limit">The most members to list.</param>
    public async Task<(int Count, List<JoinRoleTimeoutPending> Next)> GetPendingAsync(ulong guildId, int limit)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var query = db.JoinRoleTimeoutPending.Where(x => x.GuildId == guildId);
        var count = await query.CountAsync();
        var next = await query.OrderBy(x => x.JoinedAt).Take(Math.Clamp(limit, 1, 100)).ToListAsync();
        return (count, next);
    }

    private static JoinRoleTimeoutSetting Clone(JoinRoleTimeoutSetting source) => new()
    {
        Id = source.Id,
        GuildId = source.GuildId,
        Enabled = source.Enabled,
        TimeoutMinutes = source.TimeoutMinutes,
        RequiredRoleIds = source.RequiredRoleIds,
        ExemptRoleIds = source.ExemptRoleIds,
        Action = source.Action,
        ActionRoleId = source.ActionRoleId,
        WarnBeforeMinutes = source.WarnBeforeMinutes,
        WarnMessage = source.WarnMessage,
        ActionMessage = source.ActionMessage,
        LogChannelId = source.LogChannelId,
        IncludeExistingMembers = source.IncludeExistingMembers,
        DateAdded = source.DateAdded
    };

    private static bool IsCleared(JoinRoleTimeoutSetting setting, IEnumerable<ulong> roleIds)
    {
        var roles = roleIds.ToHashSet();
        return ParseIds(setting.RequiredRoleIds).Any(roles.Contains) || ParseIds(setting.ExemptRoleIds).Any(roles.Contains);
    }

    private async Task AddPendingAsync(ulong guildId, ulong userId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        await db.JoinRoleTimeoutPending.Where(x => x.GuildId == guildId && x.UserId == userId).DeleteAsync();
        await db.InsertAsync(new JoinRoleTimeoutPending
        {
            GuildId = guildId, UserId = userId, JoinedAt = DateTime.UtcNow
        });
    }

    private async Task RemovePendingAsync(ulong guildId, ulong userId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        await db.JoinRoleTimeoutPending.Where(x => x.GuildId == guildId && x.UserId == userId).DeleteAsync();
    }

    private async Task OnUserJoined(IGuildUser user)
    {
        if (user.IsBot)
            return;

        try
        {
            var setting = await GetSettingsAsync(user.Guild.Id);
            if (!setting.Enabled || ParseIds(setting.RequiredRoleIds).Count == 0 || IsCleared(setting, user.RoleIds))
                return;

            await AddPendingAsync(user.Guild.Id, user.Id);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not start the join role timeout for {UserId} in {GuildId}", user.Id,
                user.Guild.Id);
        }
    }

    private async Task OnUserLeft(IGuild guild, IUser user)
    {
        try
        {
            if ((await GetSettingsAsync(guild.Id)).Enabled)
                await RemovePendingAsync(guild.Id, user.Id);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not clear the join role timeout for {UserId} in {GuildId}", user.Id,
                guild.Id);
        }
    }

    private async Task OnGuildMemberUpdated(Cacheable<SocketGuildUser, ulong> before, SocketGuildUser after)
    {
        if (after.IsBot)
            return;

        try
        {
            var setting = await GetSettingsAsync(after.Guild.Id);
            if (!setting.Enabled || !IsCleared(setting, after.Roles.Select(x => x.Id)))
                return;

            await RemovePendingAsync(after.Guild.Id, after.Id);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not update the join role timeout for {UserId} in {GuildId}", after.Id,
                after.Guild.Id);
        }
    }

    private async Task SeedExistingAsync(ulong guildId)
    {
        try
        {
            var guild = client.GetGuild(guildId);
            if (guild is null)
                return;

            var setting = await GetSettingsAsync(guildId);
            await guild.DownloadUsersAsync();

            var members = guild.Users
                .Where(x => !x.IsBot && !IsCleared(setting, x.Roles.Select(r => r.Id)))
                .Select(x => x.Id)
                .Take(SeedCap + 1)
                .ToList();
            if (members.Count > SeedCap)
            {
                logger.LogWarning("Join role timeout seeding in {GuildId} stopped at {Cap} members", guildId, SeedCap);
                members = members.Take(SeedCap).ToList();
            }

            await using var db = await dbFactory.CreateConnectionAsync();
            var existing = (await db.JoinRoleTimeoutPending.Where(x => x.GuildId == guildId).Select(x => x.UserId)
                .ToListAsync()).ToHashSet();
            var now = DateTime.UtcNow;
            var rows = members.Where(x => !existing.Contains(x)).Select(x => new JoinRoleTimeoutPending
            {
                GuildId = guildId, UserId = x, JoinedAt = now
            }).ToList();

            foreach (var chunk in rows.Chunk(1000))
                await db.BulkCopyAsync(chunk);

            logger.LogInformation("Join role timeout started counting down {Count} existing members in {GuildId}",
                rows.Count, guildId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Join role timeout seeding failed in {GuildId}", guildId);
        }
    }

    private async Task RunLoop()
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(60));
        while (await timer.WaitForNextTickAsync())
        {
            try
            {
                await ProcessAsync();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Join role timeout pass failed");
            }
        }
    }

    private async Task ProcessAsync()
    {
        List<JoinRoleTimeoutSetting> enabled;
        await using (var db = await dbFactory.CreateConnectionAsync())
            enabled = await db.JoinRoleTimeoutSettings.Where(x => x.Enabled).ToListAsync();

        foreach (var setting in enabled)
        {
            var guild = client.GetGuild(setting.GuildId);
            if (guild is null || !ownership.IsMine(guild.Id))
                continue;

            try
            {
                await ProcessGuildAsync(guild, setting);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Join role timeout failed in {GuildId}", setting.GuildId);
            }
        }
    }

    private async Task ProcessGuildAsync(SocketGuild guild, JoinRoleTimeoutSetting setting)
    {
        var now = DateTime.UtcNow;
        var dueBefore = now.AddMinutes(-setting.TimeoutMinutes);

        await using var db = await dbFactory.CreateConnectionAsync();
        var due = await db.JoinRoleTimeoutPending
            .Where(x => x.GuildId == guild.Id && x.JoinedAt <= dueBefore)
            .OrderBy(x => x.JoinedAt)
            .Take(BatchSize)
            .ToListAsync();

        foreach (var row in due)
        {
            await ActAsync(guild, setting, row);
            await RemovePendingAsync(guild.Id, row.UserId);
            await Task.Delay(250);
        }

        if (setting.WarnBeforeMinutes is not { } warn)
            return;

        var warnBefore = now.AddMinutes(-(setting.TimeoutMinutes - warn));
        var toWarn = await db.JoinRoleTimeoutPending
            .Where(x => x.GuildId == guild.Id && x.WarnedAt == null && x.JoinedAt <= warnBefore &&
                        x.JoinedAt > dueBefore)
            .OrderBy(x => x.JoinedAt)
            .Take(BatchSize)
            .ToListAsync();

        foreach (var row in toWarn)
        {
            await WarnAsync(guild, setting, row);
            await db.JoinRoleTimeoutPending.Where(x => x.Id == row.Id)
                .Set(x => x.WarnedAt, DateTime.UtcNow)
                .UpdateAsync();
            await Task.Delay(250);
        }
    }

    private async Task<IGuildUser?> FindMemberAsync(SocketGuild guild, ulong userId)
    {
        if (guild.GetUser(userId) is { } cached)
            return cached;

        try
        {
            return await client.Rest.GetGuildUserAsync(guild.Id, userId);
        }
        catch (HttpException)
        {
            return null;
        }
    }

    private async Task WarnAsync(SocketGuild guild, JoinRoleTimeoutSetting setting, JoinRoleTimeoutPending row)
    {
        var member = await FindMemberAsync(guild, row.UserId);
        if (member is null || IsCleared(setting, member.RoleIds))
        {
            await RemovePendingAsync(guild.Id, row.UserId);
            return;
        }

        var left = row.JoinedAt.AddMinutes(setting.TimeoutMinutes) - DateTime.UtcNow;
        var leftText = FormatMinutes(Math.Max(1, (int)Math.Ceiling(left.TotalMinutes)));
        var text = setting.WarnMessage is { Length: > 0 } custom
            ? custom
            : strings.JrtDefaultWarn(guild.Id, leftText, guild.Name);

        if (await TrySendAsync(guild, member, text, ("%timeleft%", leftText)))
            await LogAsync(guild, setting, strings.JrtLogWarned(guild.Id, member.Mention, member.Id, leftText));
    }

    private async Task ActAsync(SocketGuild guild, JoinRoleTimeoutSetting setting, JoinRoleTimeoutPending row)
    {
        var member = await FindMemberAsync(guild, row.UserId);
        if (member is null || IsCleared(setting, member.RoleIds))
            return;

        var failure = CheckAction(guild, setting, member);
        if (failure is null)
        {
            var text = setting.ActionMessage is { Length: > 0 } custom ? custom : DefaultActionText(guild, setting);
            var reason = strings.JrtReason(guild.Id, FormatMinutes(setting.TimeoutMinutes));
            var options = new RequestOptions
            {
                AuditLogReason = reason
            };
            var dmSent = false;

            async Task<bool> SendDmAsync(ModCaseAction? action, TimeSpan? duration, int? caseNumber)
            {
                dmSent = true;
                if (setting.ActionMessage is not { Length: > 0 } && action is { } recorded)
                {
                    var custom = await modCases.SendPunishDmAsync(guild, member, client.CurrentUser, recorded, reason,
                        duration, caseNumber).ConfigureAwait(false);
                    if (custom is not null)
                        return custom == false;
                }

                await TrySendAsync(guild, member, text);
                return false;
            }

            try
            {
                switch (setting.Action)
                {
                    case JoinRoleTimeoutAction.Kick:
                        await modCases.PunishAsync(guild, member, client.CurrentUser, ModCaseAction.Kick, reason, null,
                            () => member.KickAsync(reason, options),
                            n => SendDmAsync(ModCaseAction.Kick, null, n)).ConfigureAwait(false);
                        break;
                    case JoinRoleTimeoutAction.Ban:
                        await modCases.PunishAsync(guild, member, client.CurrentUser, ModCaseAction.Ban, reason, null,
                            () => guild.AddBanAsync(member.Id, 0, reason, options),
                            n => SendDmAsync(ModCaseAction.Ban, null, n)).ConfigureAwait(false);
                        break;
                    case JoinRoleTimeoutAction.AddRole:
                        await member.AddRoleAsync(setting.ActionRoleId!.Value, options);
                        break;
                    case JoinRoleTimeoutAction.RemoveRoles:
                    {
                        var botTop = guild.CurrentUser.Hierarchy;
                        var removable = member.RoleIds
                            .Where(x => x != guild.Id)
                            .Select(guild.GetRole)
                            .Where(x => x is { IsManaged: false } && x.Position < botTop)
                            .ToList();
                        if (removable.Count > 0)
                            await member.RemoveRolesAsync(removable, options);
                        await modCases.RecordAsync(guild.Id, member.Id, client.CurrentUser.Id,
                            ModCaseAction.RemoveRoles, reason).ConfigureAwait(false);
                        break;
                    }
                    case JoinRoleTimeoutAction.Timeout:
                        await modCases.PunishAsync(guild, member, client.CurrentUser, ModCaseAction.Timeout, reason,
                            TimeSpan.FromDays(28), () => member.SetTimeOutAsync(TimeSpan.FromDays(28), options),
                            n => SendDmAsync(ModCaseAction.Timeout, TimeSpan.FromDays(28), n)).ConfigureAwait(false);
                        break;
                }

                if (!dmSent)
                    await TrySendAsync(guild, member, text);

                await LogAsync(guild, setting, DoneText(guild, setting.Action, member));
                return;
            }
            catch (HttpException ex)
            {
                failure = strings.JrtFailHttp(guild.Id, ex.Reason ?? ex.Message);
            }
        }

        logger.LogWarning("Join role timeout could not act on {UserId} in {GuildId}: {Reason}", member.Id, guild.Id,
            failure);
        await LogAsync(guild, setting, strings.JrtLogFailed(guild.Id, member.Mention, member.Id, failure));
    }

    private string? CheckAction(SocketGuild guild, JoinRoleTimeoutSetting setting, IGuildUser member)
    {
        if (member.Id == guild.OwnerId)
            return strings.JrtFailOwner(guild.Id);

        var me = guild.CurrentUser;
        var memberTop = member.RoleIds.Select(guild.GetRole).Where(x => x is not null).Select(x => x.Position)
            .DefaultIfEmpty(0).Max();
        if (setting.Action != JoinRoleTimeoutAction.AddRole && memberTop >= me.Hierarchy)
            return strings.JrtFailHierarchy(guild.Id);

        var permissions = me.GuildPermissions;
        switch (setting.Action)
        {
            case JoinRoleTimeoutAction.Kick when !permissions.KickMembers:
                return strings.JrtFailPermission(guild.Id, "Kick Members");
            case JoinRoleTimeoutAction.Ban when !permissions.BanMembers:
                return strings.JrtFailPermission(guild.Id, "Ban Members");
            case JoinRoleTimeoutAction.Timeout when !permissions.ModerateMembers:
                return strings.JrtFailPermission(guild.Id, "Moderate Members");
            case JoinRoleTimeoutAction.AddRole or JoinRoleTimeoutAction.RemoveRoles when !permissions.ManageRoles:
                return strings.JrtFailPermission(guild.Id, "Manage Roles");
        }

        if (setting.Action == JoinRoleTimeoutAction.AddRole)
        {
            var role = setting.ActionRoleId is { } id ? guild.GetRole(id) : null;
            if (role is null || role.IsManaged || role.Position >= me.Hierarchy)
                return strings.JrtFailRole(guild.Id);
        }

        return null;
    }

    private string DefaultActionText(SocketGuild guild, JoinRoleTimeoutSetting setting) => setting.Action switch
    {
        JoinRoleTimeoutAction.Kick => strings.JrtDefaultActionKick(guild.Id, guild.Name),
        JoinRoleTimeoutAction.Ban => strings.JrtDefaultActionBan(guild.Id, guild.Name),
        JoinRoleTimeoutAction.AddRole => strings.JrtDefaultActionAddrole(guild.Id, guild.Name),
        JoinRoleTimeoutAction.RemoveRoles => strings.JrtDefaultActionRemoveroles(guild.Id, guild.Name),
        _ => strings.JrtDefaultActionTimeout(guild.Id, guild.Name)
    };

    private string DoneText(SocketGuild guild, JoinRoleTimeoutAction action, IGuildUser member) => action switch
    {
        JoinRoleTimeoutAction.Kick => strings.JrtLogDoneKick(guild.Id, member.Mention, member.Id),
        JoinRoleTimeoutAction.Ban => strings.JrtLogDoneBan(guild.Id, member.Mention, member.Id),
        JoinRoleTimeoutAction.AddRole => strings.JrtLogDoneAddrole(guild.Id, member.Mention, member.Id),
        JoinRoleTimeoutAction.RemoveRoles => strings.JrtLogDoneRemoveroles(guild.Id, member.Mention, member.Id),
        _ => strings.JrtLogDoneTimeout(guild.Id, member.Mention, member.Id)
    };

    private async Task<bool> TrySendAsync(SocketGuild guild, IGuildUser member, string raw,
        (string Key, string Value)? extra = null)
    {
        try
        {
            var builder = new ReplacementBuilder().WithUser(member).WithServer(client, guild).WithClient(client);
            if (extra is { } pair)
                builder.WithOverride(pair.Key, () => pair.Value);
            var text = builder.Build().Replace(raw) ?? raw;

            if (SmartEmbed.TryParse(text, guild.Id, out var embeds, out var plain, out var components))
                await member.SendMessageAsync(plain, embeds: embeds, components: components?.Build());
            else
                await member.SendMessageAsync(text);
            return true;
        }
        catch (HttpException ex)
        {
            logger.LogDebug(ex, "Could not send the join role timeout message to {UserId}", member.Id);
            return false;
        }
    }

    private async Task LogAsync(SocketGuild guild, JoinRoleTimeoutSetting setting, string description)
    {
        if (setting.LogChannelId is not { } channelId || guild.GetTextChannel(channelId) is not { } channel)
            return;

        try
        {
            await channel.SendMessageAsync(embed: new EmbedBuilder()
                .WithOkColor(guild.Id)
                .WithTitle(strings.JrtLogTitle(guild.Id))
                .WithDescription(description)
                .WithCurrentTimestamp()
                .Build(), allowedMentions: AllowedMentions.None);
        }
        catch (HttpException ex)
        {
            logger.LogDebug(ex, "Could not write to the join role timeout log channel in {GuildId}", guild.Id);
        }
    }
}
