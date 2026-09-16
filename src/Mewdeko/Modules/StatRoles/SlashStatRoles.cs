using DataModel;
using Discord.Interactions;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Modules.StatRoles.Common;
using Mewdeko.Modules.StatRoles.Services;

namespace Mewdeko.Modules.StatRoles;

/// <summary>
///     Slash commands for stat roles: roles granted and removed on a schedule based on activity.
/// </summary>
[Group("statrole", "Roles granted and removed based on activity over time")]
public class SlashStatRoles : MewdekoSlashModuleBase<StatRoleService>
{
    /// <summary>
    ///     Creates a stat role.
    /// </summary>
    /// <param name="role">The role to manage.</param>
    /// <param name="stat">What to measure.</param>
    /// <param name="limit">How members qualify.</param>
    /// <param name="minimum">Threshold minimum, or per day minimum for streaks.</param>
    /// <param name="maximum">Threshold maximum, or omitted for none.</param>
    /// <param name="lookbackDays">The window in days, or 0 for all time.</param>
    /// <param name="topStart">Best rank or percentile that qualifies.</param>
    /// <param name="topEnd">Worst rank or percentile that qualifies.</param>
    /// <param name="requiredDays">Days that must meet the per day minimum, for streaks.</param>
    /// <param name="name">A display name.</param>
    /// <param name="activityName">For ActivityMinutes: the game or app to measure, or omitted for any.</param>
    [SlashCommand("create", "Creates a stat role")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageRoles)]
    [RequireBotPermission(GuildPermission.ManageRoles)]
    public async Task Create(IRole role, StatRoleStat stat, StatRoleLimit limit = StatRoleLimit.Threshold,
        [MinValue(0)] long minimum = 1, long? maximum = null, [MinValue(0)] [MaxValue(90)] int lookbackDays = 0,
        [MinValue(1)] int topStart = 1, [MinValue(1)] int topEnd = 10, [MinValue(1)] int requiredDays = 1,
        [MaxLength(64)] string? name = null, [MaxLength(128)] string? activityName = null)
    {
        if (role.Position >= ((SocketGuild)ctx.Guild).CurrentUser.Hierarchy)
        {
            await ReplyErrorAsync(Strings.StatRoleHierarchy(ctx.Guild.Id, role.Mention));
            return;
        }

        if (limit == StatRoleLimit.DailyStreak &&
            stat is not (StatRoleStat.Messages or StatRoleStat.VoiceMinutes or StatRoleStat.ActivityMinutes))
        {
            await ReplyErrorAsync(Strings.StatRoleStreakStat(ctx.Guild.Id));
            return;
        }

        var created = await Service.CreateAsync(new StatRole
        {
            GuildId = ctx.Guild.Id,
            RoleId = role.Id,
            Name = name ?? role.Name,
            StatType = (int)stat,
            LimitType = (int)limit,
            Minimum = minimum,
            Maximum = maximum,
            LookbackDays = limit == StatRoleLimit.DailyStreak ? Math.Max(1, lookbackDays) : lookbackDays,
            TopStart = topStart,
            TopEnd = topEnd,
            RequiredDays = requiredDays,
            ActivityName = string.IsNullOrWhiteSpace(activityName) ? null : activityName
        });

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
    [SlashCommand("list", "Lists stat roles")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task List()
    {
        var roles = await Service.GetAsync(ctx.Guild.Id);
        if (roles.Count == 0)
        {
            await ReplyErrorAsync(Strings.StatRoleNone(ctx.Guild.Id));
            return;
        }

        await RespondAsync(embed: StatRoleEmbeds.List(Strings, ctx.Guild.Id, roles).Build());
    }

    /// <summary>
    ///     Shows a stat role's full configuration.
    /// </summary>
    /// <param name="id">The stat role ID.</param>
    [SlashCommand("info", "Shows a stat role's configuration")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Info(int id)
    {
        var role = await Service.GetAsync(ctx.Guild.Id, id);
        if (role == null)
        {
            await ReplyErrorAsync(Strings.StatRoleNotFound(ctx.Guild.Id));
            return;
        }

        await RespondAsync(embed: StatRoleEmbeds.Info(Strings, ctx.Guild.Id, role).Build());
    }

    /// <summary>
    ///     Deletes a stat role. Members keep whatever they currently hold.
    /// </summary>
    /// <param name="id">The stat role ID.</param>
    [SlashCommand("remove", "Deletes a stat role")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageRoles)]
    public async Task Remove(int id)
    {
        await (await Service.DeleteAsync(ctx.Guild.Id, id)
            ? ReplyConfirmAsync(Strings.StatRoleRemoved(ctx.Guild.Id, id))
            : ReplyErrorAsync(Strings.StatRoleNotFound(ctx.Guild.Id)));
    }

    /// <summary>
    ///     Enables or disables a stat role.
    /// </summary>
    /// <param name="id">The stat role ID.</param>
    /// <param name="enabled">The new state.</param>
    [SlashCommand("toggle", "Enables or disables a stat role")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageRoles)]
    public async Task Toggle(int id, bool enabled)
    {
        var role = await Service.GetAsync(ctx.Guild.Id, id);
        if (role == null)
        {
            await ReplyErrorAsync(Strings.StatRoleNotFound(ctx.Guild.Id));
            return;
        }

        role.Enabled = enabled;
        await Service.UpdateAsync(role);
        await ReplyConfirmAsync(enabled
            ? Strings.StatRoleEnabled(ctx.Guild.Id, id)
            : Strings.StatRoleDisabled(ctx.Guild.Id, id));
    }

    /// <summary>
    ///     Shows who would gain and lose the role without changing anything.
    /// </summary>
    /// <param name="id">The stat role ID.</param>
    [SlashCommand("preview", "Shows who would gain and lose the role")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageRoles)]
    [InteractionRatelimit(30)]
    public async Task Preview(int id)
    {
        var role = await Service.GetAsync(ctx.Guild.Id, id);
        if (role == null)
        {
            await ReplyErrorAsync(Strings.StatRoleNotFound(ctx.Guild.Id));
            return;
        }

        await DeferAsync();
        var result = await Service.RunAsync((SocketGuild)ctx.Guild, role, true);
        await FollowupAsync(embed: StatRoleEmbeds.Result(Strings, ctx.Guild.Id, role, result, true).Build());
    }

    /// <summary>
    ///     Evaluates and applies a stat role right now.
    /// </summary>
    /// <param name="id">The stat role ID.</param>
    [SlashCommand("run", "Evaluates and applies a stat role now")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageRoles)]
    [RequireBotPermission(GuildPermission.ManageRoles)]
    public async Task Run(int id)
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

        await DeferAsync();
        var result = await Service.RunAsync((SocketGuild)ctx.Guild, role, false);
        await FollowupAsync(embed: StatRoleEmbeds.Result(Strings, ctx.Guild.Id, role, result, false).Build());
    }

    /// <summary>
    ///     Stat role settings.
    /// </summary>
    [Group("set", "Change a stat role's settings")]
    public class StatRoleSetCommands : MewdekoSlashSubmodule<StatRoleService>
    {
        /// <summary>
        ///     Changes the condition values.
        /// </summary>
        /// <param name="id">The stat role ID.</param>
        /// <param name="minimum">Threshold or per day minimum.</param>
        /// <param name="maximum">Threshold maximum, or -1 to clear.</param>
        /// <param name="lookbackDays">The window in days, or 0 for all time.</param>
        /// <param name="topStart">Best rank or percentile that qualifies.</param>
        /// <param name="topEnd">Worst rank or percentile that qualifies.</param>
        /// <param name="requiredDays">Days that must meet the per day minimum.</param>
        [SlashCommand("condition", "Changes the condition values")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageRoles)]
        public async Task Condition(int id, long? minimum = null, long? maximum = null,
            [MinValue(0)] [MaxValue(90)] int? lookbackDays = null, [MinValue(1)] int? topStart = null,
            [MinValue(1)] int? topEnd = null, [MinValue(1)] int? requiredDays = null)
        {
            var role = await Service.GetAsync(ctx.Guild.Id, id);
            if (role == null)
            {
                await ReplyErrorAsync(Strings.StatRoleNotFound(ctx.Guild.Id));
                return;
            }

            if (minimum.HasValue) role.Minimum = minimum.Value;
            if (maximum.HasValue) role.Maximum = maximum.Value < 0 ? null : maximum.Value;
            if (lookbackDays.HasValue) role.LookbackDays = lookbackDays.Value;
            if (topStart.HasValue) role.TopStart = topStart.Value;
            if (topEnd.HasValue) role.TopEnd = topEnd.Value;
            if (requiredDays.HasValue) role.RequiredDays = requiredDays.Value;

            await Service.UpdateAsync(role);
            await ReplyConfirmAsync(Strings.StatRoleUpdated(ctx.Guild.Id, id, StatRoleEmbeds.Condition(role)));
        }

        /// <summary>
        ///     Changes behaviour flags.
        /// </summary>
        /// <param name="id">The stat role ID.</param>
        /// <param name="permanent">Whether the role is never removed once earned.</param>
        /// <param name="invert">Whether members who do NOT qualify get the role.</param>
        /// <param name="bots">Whether bots are considered.</param>
        /// <param name="intervalMinutes">How often the role is evaluated.</param>
        /// <param name="group">A group name; only the highest tier in a group is kept. "none" clears it.</param>
        /// <param name="name">A display name.</param>
        /// <param name="activityName">For ActivityMinutes: the game or app to measure. "any" clears it.</param>
        [SlashCommand("options", "Changes permanence, inversion, bots, interval, group, name and game")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageRoles)]
        public async Task Options(int id, bool? permanent = null, bool? invert = null, bool? bots = null,
            [MinValue(10)] int? intervalMinutes = null, [MaxLength(32)] string? group = null,
            [MaxLength(64)] string? name = null, [MaxLength(128)] string? activityName = null)
        {
            var role = await Service.GetAsync(ctx.Guild.Id, id);
            if (role == null)
            {
                await ReplyErrorAsync(Strings.StatRoleNotFound(ctx.Guild.Id));
                return;
            }

            if (permanent.HasValue) role.Permanent = permanent.Value;
            if (invert.HasValue) role.Invert = invert.Value;
            if (bots.HasValue) role.ApplyToBots = bots.Value;
            if (intervalMinutes.HasValue) role.IntervalMinutes = intervalMinutes.Value;
            if (group != null) role.GroupName = group.Equals("none", StringComparison.OrdinalIgnoreCase) ? null : group;
            if (name != null) role.Name = name;
            if (activityName != null)
                role.ActivityName =
                    activityName.Equals("any", StringComparison.OrdinalIgnoreCase) ? null : activityName;

            await Service.UpdateAsync(role);
            await ReplyConfirmAsync(Strings.StatRoleUpdated(ctx.Guild.Id, id, StatRoleEmbeds.Condition(role)));
        }

        /// <summary>
        ///     Changes notifications.
        /// </summary>
        /// <param name="id">The stat role ID.</param>
        /// <param name="channel">The channel to announce in, or omitted to keep the current one.</param>
        /// <param name="clearChannel">True to stop announcing in a channel.</param>
        /// <param name="dm">Whether members are messaged directly.</param>
        /// <param name="message">The template; supports %user%, %role%, %action%, %value%, %stat%. "none" resets.</param>
        [SlashCommand("notify", "Changes where and how role changes are announced")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageRoles)]
        public async Task Notify(int id, ITextChannel? channel = null, bool clearChannel = false, bool? dm = null,
            string? message = null)
        {
            var role = await Service.GetAsync(ctx.Guild.Id, id);
            if (role == null)
            {
                await ReplyErrorAsync(Strings.StatRoleNotFound(ctx.Guild.Id));
                return;
            }

            if (clearChannel) role.NotifyChannelId = null;
            else if (channel != null) role.NotifyChannelId = channel.Id;
            if (dm.HasValue) role.NotifyDm = dm.Value;
            if (message != null)
                role.NotifyMessage = message.Equals("none", StringComparison.OrdinalIgnoreCase) ? null : message;

            await Service.UpdateAsync(role);
            await ReplyConfirmAsync(Strings.StatRoleUpdated(ctx.Guild.Id, id, StatRoleEmbeds.Condition(role)));
        }
    }

    /// <summary>
    ///     Stat role filters.
    /// </summary>
    [Group("filter", "Limit a stat role to channels, roles or exclude members")]
    public class StatRoleFilterCommands : MewdekoSlashSubmodule<StatRoleService>
    {
        /// <summary>
        ///     Toggles a channel in the stat role's channel filter.
        /// </summary>
        /// <param name="id">The stat role ID.</param>
        /// <param name="channel">The channel.</param>
        [SlashCommand("channel", "Toggles a channel the stat is limited to")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageRoles)]
        public async Task Channel(int id, IGuildChannel channel)
        {
            await ToggleAsync(id, r => r.ChannelFilter, (r, v) => r.ChannelFilter = v, channel.Id,
                $"<#{channel.Id}>");
        }

        /// <summary>
        ///     Toggles a role in the whitelist (members need one) or blacklist (members must have none).
        /// </summary>
        /// <param name="id">The stat role ID.</param>
        /// <param name="role">The role.</param>
        /// <param name="blacklist">True to toggle the blacklist instead of the whitelist.</param>
        [SlashCommand("role", "Toggles a required or excluded role")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageRoles)]
        public async Task Role(int id, IRole role, bool blacklist = false)
        {
            if (blacklist)
                await ToggleAsync(id, r => r.RoleBlacklist, (r, v) => r.RoleBlacklist = v, role.Id, role.Mention);
            else
                await ToggleAsync(id, r => r.RoleWhitelist, (r, v) => r.RoleWhitelist = v, role.Id, role.Mention);
        }

        /// <summary>
        ///     Toggles a member the stat role never touches.
        /// </summary>
        /// <param name="id">The stat role ID.</param>
        /// <param name="user">The member.</param>
        [SlashCommand("ignore", "Toggles a member the stat role never touches")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageRoles)]
        public async Task Ignore(int id, IGuildUser user)
        {
            await ToggleAsync(id, r => r.IgnoredUsers, (r, v) => r.IgnoredUsers = v, user.Id, user.Mention);
        }

        private async Task ToggleAsync(int id, Func<StatRole, string?> read, Action<StatRole, string?> write,
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
}