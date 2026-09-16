using DataModel;
using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.StatRoles.Common;
using Mewdeko.Modules.StatRoles.Services;

namespace Mewdeko.Modules.StatRoles;

/// <summary>
///     Roles that are granted and removed on a schedule based on activity: messages, voice time, invites, time in
///     the server or account age, judged by thresholds, top ranks, top percentages or daily streaks.
/// </summary>
public class StatRoles : MewdekoModuleBase<StatRoleService>
{
    /// <summary>
    ///     Creates a threshold stat role: members with at least the minimum get the role, everyone else loses it.
    /// </summary>
    /// <param name="role">The role to manage.</param>
    /// <param name="stat">What to measure.</param>
    /// <param name="minimum">The minimum value.</param>
    /// <param name="lookbackDays">The window in days, or 0 for all time.</param>
    /// <param name="name">A display name.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageRoles)]
    [BotPerm(GuildPermission.ManageRoles)]
    public async Task StatRoleAdd(IRole role, StatRoleStat stat, long minimum, int lookbackDays = 0,
        [Remainder] string? name = null)
    {
        await CreateAsync(new StatRole
        {
            GuildId = ctx.Guild.Id,
            RoleId = role.Id,
            Name = name ?? role.Name,
            StatType = (int)stat,
            LimitType = (int)StatRoleLimit.Threshold,
            Minimum = minimum,
            LookbackDays = lookbackDays
        }, role);
    }

    /// <summary>
    ///     Creates a top rank stat role: the members ranked 1 through the given place get the role.
    /// </summary>
    /// <param name="role">The role to manage.</param>
    /// <param name="stat">What to measure.</param>
    /// <param name="topEnd">The worst rank that still qualifies.</param>
    /// <param name="lookbackDays">The window in days, or 0 for all time.</param>
    /// <param name="name">A display name.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageRoles)]
    [BotPerm(GuildPermission.ManageRoles)]
    public async Task StatRoleTop(IRole role, StatRoleStat stat, int topEnd, int lookbackDays = 0,
        [Remainder] string? name = null)
    {
        await CreateAsync(new StatRole
        {
            GuildId = ctx.Guild.Id,
            RoleId = role.Id,
            Name = name ?? role.Name,
            StatType = (int)stat,
            LimitType = (int)StatRoleLimit.TopRank,
            TopStart = 1,
            TopEnd = topEnd,
            LookbackDays = lookbackDays
        }, role);
    }

    /// <summary>
    ///     Creates a top percent stat role: members in the top given percent get the role.
    /// </summary>
    /// <param name="role">The role to manage.</param>
    /// <param name="stat">What to measure.</param>
    /// <param name="topPercent">The percentile cutoff, 1 to 100.</param>
    /// <param name="lookbackDays">The window in days, or 0 for all time.</param>
    /// <param name="name">A display name.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageRoles)]
    [BotPerm(GuildPermission.ManageRoles)]
    public async Task StatRolePercent(IRole role, StatRoleStat stat, int topPercent, int lookbackDays = 0,
        [Remainder] string? name = null)
    {
        await CreateAsync(new StatRole
        {
            GuildId = ctx.Guild.Id,
            RoleId = role.Id,
            Name = name ?? role.Name,
            StatType = (int)stat,
            LimitType = (int)StatRoleLimit.TopPercent,
            TopStart = 1,
            TopEnd = Math.Clamp(topPercent, 1, 100),
            LookbackDays = lookbackDays
        }, role);
    }

    /// <summary>
    ///     Creates a daily streak stat role: members who hit the per day minimum on enough days in the window get
    ///     the role.
    /// </summary>
    /// <param name="role">The role to manage.</param>
    /// <param name="stat">Messages or voice minutes.</param>
    /// <param name="perDay">The per day minimum.</param>
    /// <param name="requiredDays">How many days in the window must meet it.</param>
    /// <param name="lookbackDays">The window in days.</param>
    /// <param name="name">A display name.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageRoles)]
    [BotPerm(GuildPermission.ManageRoles)]
    public async Task StatRoleStreak(IRole role, StatRoleStat stat, long perDay, int requiredDays,
        int lookbackDays = 7, [Remainder] string? name = null)
    {
        if (stat is not (StatRoleStat.Messages or StatRoleStat.VoiceMinutes or StatRoleStat.ActivityMinutes))
        {
            await ReplyErrorAsync(Strings.StatRoleStreakStat(ctx.Guild.Id));
            return;
        }

        await CreateAsync(new StatRole
        {
            GuildId = ctx.Guild.Id,
            RoleId = role.Id,
            Name = name ?? role.Name,
            StatType = (int)stat,
            LimitType = (int)StatRoleLimit.DailyStreak,
            Minimum = perDay,
            RequiredDays = requiredDays,
            LookbackDays = Math.Max(1, lookbackDays)
        }, role);
    }

    private async Task CreateAsync(StatRole statRole, IRole role)
    {
        if (role.Position >= ((SocketGuild)ctx.Guild).CurrentUser.Hierarchy)
        {
            await ReplyErrorAsync(Strings.StatRoleHierarchy(ctx.Guild.Id, role.Mention));
            return;
        }

        var created = await Service.CreateAsync(statRole);
        if (created == null)
        {
            await ReplyErrorAsync(Strings.StatRoleExists(ctx.Guild.Id, role.Mention));
            return;
        }

        await ReplyConfirmAsync(Strings.StatRoleCreated(ctx.Guild.Id, created.Id, role.Mention,
            StatRoleEmbeds.Condition(created)));
    }

    /// <summary>
    ///     Lists stat roles.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task StatRoleList()
    {
        var roles = await Service.GetAsync(ctx.Guild.Id);
        if (roles.Count == 0)
        {
            await ReplyErrorAsync(Strings.StatRoleNone(ctx.Guild.Id));
            return;
        }

        await ctx.Channel.SendMessageAsync(embed: StatRoleEmbeds.List(Strings, ctx.Guild.Id, roles).Build());
    }

    /// <summary>
    ///     Shows a stat role's full configuration.
    /// </summary>
    /// <param name="id">The stat role ID.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task StatRoleInfo(int id)
    {
        var role = await Service.GetAsync(ctx.Guild.Id, id);
        if (role == null)
        {
            await ReplyErrorAsync(Strings.StatRoleNotFound(ctx.Guild.Id));
            return;
        }

        await ctx.Channel.SendMessageAsync(embed: StatRoleEmbeds.Info(Strings, ctx.Guild.Id, role).Build());
    }

    /// <summary>
    ///     Deletes a stat role. Members keep whatever they currently hold.
    /// </summary>
    /// <param name="id">The stat role ID.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageRoles)]
    public async Task StatRoleRemove(int id)
    {
        await (await Service.DeleteAsync(ctx.Guild.Id, id)
            ? ReplyConfirmAsync(Strings.StatRoleRemoved(ctx.Guild.Id, id))
            : ReplyErrorAsync(Strings.StatRoleNotFound(ctx.Guild.Id)));
    }

    /// <summary>
    ///     Enables or disables a stat role.
    /// </summary>
    /// <param name="id">The stat role ID.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageRoles)]
    public async Task StatRoleToggle(int id)
    {
        var role = await Service.GetAsync(ctx.Guild.Id, id);
        if (role == null)
        {
            await ReplyErrorAsync(Strings.StatRoleNotFound(ctx.Guild.Id));
            return;
        }

        role.Enabled = !role.Enabled;
        await Service.UpdateAsync(role);
        await ReplyConfirmAsync(role.Enabled
            ? Strings.StatRoleEnabled(ctx.Guild.Id, id)
            : Strings.StatRoleDisabled(ctx.Guild.Id, id));
    }

    /// <summary>
    ///     Shows who would gain and lose the role without changing anything.
    /// </summary>
    /// <param name="id">The stat role ID.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageRoles)]
    [Ratelimit(30)]
    public async Task StatRolePreview(int id)
    {
        var role = await Service.GetAsync(ctx.Guild.Id, id);
        if (role == null)
        {
            await ReplyErrorAsync(Strings.StatRoleNotFound(ctx.Guild.Id));
            return;
        }

        var result = await Service.RunAsync((SocketGuild)ctx.Guild, role, true);
        await ctx.Channel.SendMessageAsync(embed: StatRoleEmbeds.Result(Strings, ctx.Guild.Id, role, result, true)
            .Build());
    }

    /// <summary>
    ///     Evaluates and applies a stat role right now.
    /// </summary>
    /// <param name="id">The stat role ID.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageRoles)]
    [BotPerm(GuildPermission.ManageRoles)]
    public async Task StatRoleRun(int id)
    {
        var role = await Service.GetAsync(ctx.Guild.Id, id);
        if (role == null)
        {
            await ReplyErrorAsync(Strings.StatRoleNotFound(ctx.Guild.Id));
            return;
        }

        if (!Service.TryReserveManualRun(ctx.Guild.Id))
        {
            await ReplyErrorAsync(Strings.StatRoleRunCooldown(ctx.Guild.Id));
            return;
        }

        var result = await Service.RunAsync((SocketGuild)ctx.Guild, role, false);
        await ctx.Channel.SendMessageAsync(embed: StatRoleEmbeds.Result(Strings, ctx.Guild.Id, role, result, false)
            .Build());
    }

    /// <summary>
    ///     Changes one setting on a stat role. Settings: name, min, max, lookback, topstart, topend, days, permanent,
    ///     invert, bots, group, interval, notifychannel, notifydm, message, activity.
    /// </summary>
    /// <param name="id">The stat role ID.</param>
    /// <param name="setting">The setting name.</param>
    /// <param name="value">The new value.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageRoles)]
    public async Task StatRoleSet(int id, string setting, [Remainder] string value)
    {
        var role = await Service.GetAsync(ctx.Guild.Id, id);
        if (role == null)
        {
            await ReplyErrorAsync(Strings.StatRoleNotFound(ctx.Guild.Id));
            return;
        }

        var ok = true;
        switch (setting.ToLowerInvariant())
        {
            case "name":
                role.Name = value.Length > 64 ? value[..64] : value;
                break;
            case "min":
            case "minimum":
                ok = long.TryParse(value, out var min);
                if (ok) role.Minimum = min;
                break;
            case "max":
            case "maximum":
                if (value.Equals("none", StringComparison.OrdinalIgnoreCase))
                    role.Maximum = null;
                else
                {
                    ok = long.TryParse(value, out var max);
                    if (ok) role.Maximum = max;
                }

                break;
            case "lookback":
                ok = int.TryParse(value, out var lookback);
                if (ok) role.LookbackDays = lookback;
                break;
            case "topstart":
                ok = int.TryParse(value, out var topStart);
                if (ok) role.TopStart = topStart;
                break;
            case "topend":
                ok = int.TryParse(value, out var topEnd);
                if (ok) role.TopEnd = topEnd;
                break;
            case "days":
                ok = int.TryParse(value, out var days);
                if (ok) role.RequiredDays = days;
                break;
            case "permanent":
                ok = bool.TryParse(value, out var permanent);
                if (ok) role.Permanent = permanent;
                break;
            case "invert":
                ok = bool.TryParse(value, out var invert);
                if (ok) role.Invert = invert;
                break;
            case "bots":
                ok = bool.TryParse(value, out var bots);
                if (ok) role.ApplyToBots = bots;
                break;
            case "group":
                role.GroupName = value.Equals("none", StringComparison.OrdinalIgnoreCase) ? null : value;
                break;
            case "interval":
                ok = int.TryParse(value, out var interval);
                if (ok) role.IntervalMinutes = interval;
                break;
            case "notifychannel":
                if (value.Equals("none", StringComparison.OrdinalIgnoreCase))
                    role.NotifyChannelId = null;
                else
                {
                    ok = MentionUtils.TryParseChannel(value, out var channelId) || ulong.TryParse(value, out channelId);
                    if (ok) role.NotifyChannelId = channelId;
                }

                break;
            case "notifydm":
                ok = bool.TryParse(value, out var dm);
                if (ok) role.NotifyDm = dm;
                break;
            case "message":
                role.NotifyMessage = value.Equals("none", StringComparison.OrdinalIgnoreCase) ? null : value;
                break;
            case "activity":
            case "game":
                role.ActivityName = value.Equals("any", StringComparison.OrdinalIgnoreCase) ? null : value;
                break;
            default:
                await ReplyErrorAsync(Strings.StatRoleUnknownSetting(ctx.Guild.Id));
                return;
        }

        if (!ok)
        {
            await ReplyErrorAsync(Strings.StatRoleInvalidValue(ctx.Guild.Id));
            return;
        }

        await Service.UpdateAsync(role);
        await ReplyConfirmAsync(Strings.StatRoleUpdated(ctx.Guild.Id, id, StatRoleEmbeds.Condition(role)));
    }

    /// <summary>
    ///     Toggles a channel in the stat role's channel filter.
    /// </summary>
    /// <param name="id">The stat role ID.</param>
    /// <param name="channel">The channel.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageRoles)]
    public async Task StatRoleFilter(int id, IGuildChannel channel)
    {
        await ToggleListAsync(id, r => r.ChannelFilter, (r, v) => r.ChannelFilter = v, channel.Id, $"<#{channel.Id}>");
    }

    /// <summary>
    ///     Toggles a role in the stat role's whitelist, or blacklist when asked.
    /// </summary>
    /// <param name="id">The stat role ID.</param>
    /// <param name="role">The role.</param>
    /// <param name="blacklist">True to toggle the blacklist instead of the whitelist.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageRoles)]
    public async Task StatRoleFilter(int id, IRole role, bool blacklist = false)
    {
        if (blacklist)
            await ToggleListAsync(id, r => r.RoleBlacklist, (r, v) => r.RoleBlacklist = v, role.Id, role.Mention);
        else
            await ToggleListAsync(id, r => r.RoleWhitelist, (r, v) => r.RoleWhitelist = v, role.Id, role.Mention);
    }

    /// <summary>
    ///     Toggles a member in the stat role's ignore list.
    /// </summary>
    /// <param name="id">The stat role ID.</param>
    /// <param name="user">The member.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageRoles)]
    public async Task StatRoleFilter(int id, IGuildUser user)
    {
        await ToggleListAsync(id, r => r.IgnoredUsers, (r, v) => r.IgnoredUsers = v, user.Id, user.Mention);
    }

    private async Task ToggleListAsync(int id, Func<StatRole, string?> read, Action<StatRole, string?> write,
        ulong targetId, string mention)
    {
        var role = await Service.GetAsync(ctx.Guild.Id, id);
        if (role == null)
        {
            await ReplyErrorAsync(Strings.StatRoleNotFound(ctx.Guild.Id));
            return;
        }

        var ids = StatRoleService.ReadIds(read(role));
        var added = ids.Add(targetId);
        if (!added)
            ids.Remove(targetId);

        write(role, StatRoleService.WriteIds(ids));
        await Service.UpdateAsync(role);
        await ReplyConfirmAsync(added
            ? Strings.StatRoleFilterAdded(ctx.Guild.Id, mention, id)
            : Strings.StatRoleFilterRemoved(ctx.Guild.Id, mention, id));
    }
}