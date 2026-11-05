using DataModel;
using Discord.Interactions;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Common.Autocompleters;
using Mewdeko.Modules.RoleMenus.Common;
using Mewdeko.Modules.RoleMenus.Services;

namespace Mewdeko.Modules.RoleMenus;

/// <summary>
///     Slash commands for role menus, plus the handlers for the dropdowns and buttons on posted menus.
/// </summary>
[Group("rolemenu", "Menus that let members pick their own roles")]
public class SlashRoleMenus : MewdekoSlashModuleBase<RoleMenuService>
{
    /// <summary>
    ///     Lists this server's role menus.
    /// </summary>
    [SlashCommand("list", "Lists this server's role menus")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageRoles)]
    [RequireBotPermission(GuildPermission.ManageRoles)]
    [CheckPermissions]
    public async Task List()
    {
        var list = await Service.GetMenusAsync(ctx.Guild.Id);
        if (list.Count == 0)
        {
            var prefix = await Service.GetPrefixAsync(ctx.Guild);
            await ConfirmAsync(Strings.RolemenuListEmpty(ctx.Guild.Id, prefix));
            return;
        }

        var embed = Service.BuildListEmbed((SocketGuild)ctx.Guild, list);
        await RespondAsync(embed: embed.Build(), ephemeral: true);
    }

    /// <summary>
    ///     Shows one role menu's settings and options.
    /// </summary>
    /// <param name="menu">The menu ID.</param>
    [SlashCommand("info", "Shows one role menu's settings and options")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageRoles)]
    [RequireBotPermission(GuildPermission.ManageRoles)]
    [CheckPermissions]
    public async Task Info(
        [Summary("menu", "The role menu")] [Autocomplete(typeof(RoleMenuAutocompleter))]
        int menu)
    {
        var found = await FindMenuAsync(menu);
        if (found is null)
            return;

        var embed = Service.BuildInfoEmbed((SocketGuild)ctx.Guild, found);
        await RespondAsync(embed: embed.Build(), ephemeral: true);
    }

    /// <summary>
    ///     Posts a new role menu with its first role.
    /// </summary>
    /// <param name="channel">The channel to post in.</param>
    /// <param name="style">Dropdown or buttons.</param>
    /// <param name="role">The first role to offer.</param>
    /// <param name="name">The menu name.</param>
    /// <param name="mode">Pick any or pick one.</param>
    [SlashCommand("create", "Posts a new role menu with its first role")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageRoles)]
    [RequireBotPermission(GuildPermission.ManageRoles)]
    [CheckPermissions]
    public async Task Create(ITextChannel channel, RoleMenuStyle style, IRole role, string? name = null,
        RoleMenuMode mode = RoleMenuMode.Multi)
    {
        await DeferAsync(true);

        var draft = new RoleMenuDraft
        {
            Name = name,
            ChannelId = channel.Id,
            Style = style,
            Mode = mode,
            MaxRoles = mode == RoleMenuMode.Exclusive ? 1 : 0,
            ReplyMode = RoleMenuReplyMode.Private,
            Options =
            [
                new RoleMenuOptionDraft
                {
                    RoleId = role.Id
                }
            ]
        };

        var result = await Service.CreateAsync(ctx.Guild.Id, (IGuildUser)ctx.User, draft);
        if (!result.Success || result.Menu is null)
        {
            await ErrorAsync(Service.DescribeError(ctx.Guild.Id, result, null, channel.Id));
            return;
        }

        await ConfirmAsync(Strings.RolemenuCreated(ctx.Guild.Id, result.Menu.Name, result.Menu.Id,
            RoleMenuService.GetJumpUrl(result.Menu) ?? channel.Mention));
    }

    /// <summary>
    ///     Adds a role to a menu.
    /// </summary>
    /// <param name="menu">The menu ID.</param>
    /// <param name="role">The role to add.</param>
    /// <param name="emoji">An optional emoji.</param>
    /// <param name="label">The name shown on the menu. Defaults to the role name.</param>
    /// <param name="description">Short text under the option.</param>
    /// <param name="color">Button color, for button menus.</param>
    /// <param name="temporary">How long this role lasts before it is taken back, such as 12h.</param>
    /// <param name="maxHolders">The most members who can hold this role through the menu, 0 for no limit.</param>
    [SlashCommand("add", "Adds a role to a menu")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageRoles)]
    [RequireBotPermission(GuildPermission.ManageRoles)]
    [CheckPermissions]
    public async Task Add(
        [Summary("menu", "The role menu")] [Autocomplete(typeof(RoleMenuAutocompleter))]
        int menu,
        IRole role,
        string? emoji = null,
        string? label = null,
        string? description = null,
        RoleMenuButtonColor color = RoleMenuButtonColor.Grey,
        string? temporary = null,
        [Summary("max-holders", "The most members who can hold this role, 0 for no limit")] [MinValue(0)]
        int maxHolders = 0)
    {
        var found = await FindMenuAsync(menu);
        if (found is null)
            return;

        if (!RoleMenuService.TryParseDuration(temporary, out var tempSeconds))
        {
            await ErrorAsync(Strings.RolemenuDurationInvalid(ctx.Guild.Id));
            return;
        }

        if (found.Options.Any(o => o.RoleId == role.Id))
        {
            await ErrorAsync(Strings.RolemenuOptionExists(ctx.Guild.Id, role.Mention));
            return;
        }

        if (found.Options.Count >= RoleMenuService.MaxOptions)
        {
            await ErrorAsync(Strings.RolemenuTooManyOptions(ctx.Guild.Id));
            return;
        }

        await DeferAsync(true);

        var draft = RoleMenuDraft.From(found);
        draft.Options.Add(new RoleMenuOptionDraft
        {
            RoleId = role.Id,
            Label = label,
            Emoji = emoji,
            Description = description,
            ButtonStyle = (int)color,
            TempRoleSeconds = tempSeconds,
            MaxHolders = maxHolders
        });

        if (await SaveAsync(found, draft) is not { } updated)
            return;

        await ConfirmAsync(Strings.RolemenuOptionAdded(ctx.Guild.Id, role.Mention, updated.Name));
    }

    /// <summary>
    ///     Changes the temporary role time and holder limit of a role on a menu.
    /// </summary>
    /// <param name="menu">The menu ID.</param>
    /// <param name="role">The role on the menu.</param>
    /// <param name="temporary">How long this role lasts, such as 12h. "off" follows the menu's setting.</param>
    /// <param name="maxHolders">The most members who can hold this role through the menu, 0 for no limit.</param>
    [SlashCommand("option", "Changes how long a role lasts and how many members can hold it")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageRoles)]
    [RequireBotPermission(GuildPermission.ManageRoles)]
    [CheckPermissions]
    public async Task Option(
        [Summary("menu", "The role menu")] [Autocomplete(typeof(RoleMenuAutocompleter))]
        int menu,
        IRole role,
        string? temporary = null,
        [Summary("max-holders", "The most members who can hold this role, 0 for no limit")] [MinValue(0)]
        int? maxHolders = null)
    {
        var found = await FindMenuAsync(menu);
        if (found is null)
            return;

        var draft = RoleMenuDraft.From(found);
        var option = draft.Options.FirstOrDefault(o => o.RoleId == role.Id);
        if (option is null)
        {
            await ErrorAsync(Strings.RolemenuOptionMissing(ctx.Guild.Id, role.Mention));
            return;
        }

        var tempSeconds = 0;
        if (temporary is not null && !RoleMenuService.TryParseDuration(temporary, out tempSeconds))
        {
            await ErrorAsync(Strings.RolemenuDurationInvalid(ctx.Guild.Id));
            return;
        }

        if (temporary is null && !maxHolders.HasValue)
        {
            await ConfirmAsync(Strings.RolemenuNoChange(ctx.Guild.Id));
            return;
        }

        await DeferAsync(true);

        if (temporary is not null)
            option.TempRoleSeconds = tempSeconds;
        if (maxHolders.HasValue)
            option.MaxHolders = maxHolders.Value;

        if (await SaveAsync(found, draft) is not { } updated)
            return;

        var guildId = ctx.Guild.Id;
        var lines = new List<string>();
        if (temporary is not null)
            lines.Add(tempSeconds == 0
                ? Strings.RolemenuOptionTempCleared(guildId, role.Mention, updated.Name)
                : Strings.RolemenuOptionTempSet(guildId, role.Mention, updated.Name,
                    Service.DurationText(guildId, tempSeconds)));
        if (maxHolders.HasValue)
            lines.Add(maxHolders.Value == 0
                ? Strings.RolemenuCapCleared(guildId, role.Mention, updated.Name)
                : Strings.RolemenuCapSet(guildId, role.Mention, updated.Name, maxHolders.Value));

        await ConfirmAsync(string.Join('\n', lines));
    }

    /// <summary>
    ///     Removes a role from a menu.
    /// </summary>
    /// <param name="menu">The menu ID.</param>
    /// <param name="role">The role to remove.</param>
    [SlashCommand("remove", "Removes a role from a menu")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageRoles)]
    [RequireBotPermission(GuildPermission.ManageRoles)]
    [CheckPermissions]
    public async Task Remove(
        [Summary("menu", "The role menu")] [Autocomplete(typeof(RoleMenuAutocompleter))]
        int menu,
        IRole role)
    {
        var found = await FindMenuAsync(menu);
        if (found is null)
            return;

        if (found.Options.All(o => o.RoleId != role.Id))
        {
            await ErrorAsync(Strings.RolemenuOptionMissing(ctx.Guild.Id, role.Mention));
            return;
        }

        if (found.Options.Count <= 1)
        {
            await ErrorAsync(Strings.RolemenuNeedsOption(ctx.Guild.Id));
            return;
        }

        await DeferAsync(true);

        var draft = RoleMenuDraft.From(found);
        draft.Options.RemoveAll(o => o.RoleId == role.Id);

        if (await SaveAsync(found, draft) is not { } updated)
            return;

        await ConfirmAsync(Strings.RolemenuOptionRemoved(ctx.Guild.Id, role.Mention, updated.Name));
    }

    /// <summary>
    ///     Changes how a menu looks and works. Only the supplied settings change.
    /// </summary>
    /// <param name="menu">The menu ID.</param>
    /// <param name="name">A new name.</param>
    /// <param name="style">Dropdown or buttons.</param>
    /// <param name="mode">Pick any or pick one.</param>
    /// <param name="min">The fewest roles a member must keep once they pick.</param>
    /// <param name="max">The most roles a member can hold, 0 for no limit.</param>
    /// <param name="requiredRole">A role members need to use the menu.</param>
    /// <param name="clearRequiredRole">Let anyone use the menu.</param>
    /// <param name="confirmation">Private confirmation or none.</param>
    /// <param name="placeholder">Dropdown hint text.</param>
    /// <param name="temporary">How long roles last before they are taken back, such as 12h. "off" keeps them.</param>
    /// <param name="expires">When the menu deletes itself and turns off, such as 7d. "off" never expires.</param>
    /// <param name="blockRole">A role that can't use the menu.</param>
    /// <param name="unblockRole">A role that can use the menu again.</param>
    /// <param name="clearBlockedRoles">Stop blocking any roles.</param>
    /// <param name="color">The colour of the default message, as a name or hex code. "off" goes back to the default.</param>
    /// <param name="buttonColor">The button colour for options with none of their own.</param>
    /// <param name="group">A group to link the menu into so menus share one limit. "off" unlinks it.</param>
    /// <param name="groupLimit">The most roles a member can hold across the group, 0 for no limit.</param>
    [SlashCommand("settings", "Changes how a menu looks and works")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageRoles)]
    [RequireBotPermission(GuildPermission.ManageRoles)]
    [CheckPermissions]
    public async Task Settings(
        [Summary("menu", "The role menu")] [Autocomplete(typeof(RoleMenuAutocompleter))]
        int menu,
        string? name = null,
        RoleMenuStyle? style = null,
        RoleMenuMode? mode = null,
        [MinValue(0)] [MaxValue(25)] int? min = null,
        [MinValue(0)] [MaxValue(25)] int? max = null,
        [Summary("required-role", "A role members need to use the menu")]
        IRole? requiredRole = null,
        [Summary("clear-required-role", "Let anyone use the menu")]
        bool clearRequiredRole = false,
        RoleMenuReplyMode? confirmation = null,
        string? placeholder = null,
        string? temporary = null,
        string? expires = null,
        [Summary("block-role", "A role that can't use the menu")]
        IRole? blockRole = null,
        [Summary("unblock-role", "A role that can use the menu again")]
        IRole? unblockRole = null,
        [Summary("clear-blocked-roles", "Stop blocking any roles")]
        bool clearBlockedRoles = false,
        string? color = null,
        [Summary("button-color", "The button colour for options with none of their own")]
        RoleMenuButtonColor? buttonColor = null,
        string? group = null,
        [Summary("group-limit", "The most roles a member can hold across the group, 0 for no limit")] [MinValue(0)]
        int? groupLimit = null)
    {
        var found = await FindMenuAsync(menu);
        if (found is null)
            return;

        var draft = RoleMenuDraft.From(found);
        var changed = false;

        var clearColor = color is not null && color.Trim().Equals("off", StringComparison.OrdinalIgnoreCase);
        var accent = 0;
        if (color is not null && !clearColor && !RoleMenuService.TryParseAccent(color, out accent))
        {
            await ErrorAsync(Strings.RolemenuColorInvalid(ctx.Guild.Id));
            return;
        }

        if (color is not null)
        {
            draft.AccentColor = clearColor ? null : accent;
            changed = true;
        }

        if (buttonColor.HasValue)
        {
            draft.ButtonColor = buttonColor.Value == RoleMenuButtonColor.Grey ? 0 : (int)buttonColor.Value;
            changed = true;
        }

        var clearGroup = group is not null && group.Trim().Equals("off", StringComparison.OrdinalIgnoreCase);
        RoleMenuGroup? linked = null;
        if (group is not null)
        {
            draft.GroupId = null;
            changed = true;
        }
        else if (groupLimit.HasValue && found.GroupId is null)
        {
            await ErrorAsync(Strings.RolemenuGroupInvalid(ctx.Guild.Id));
            return;
        }
        else if (groupLimit.HasValue)
        {
            changed = true;
        }

        var tempSeconds = 0;
        var expireSeconds = 0;
        if (temporary is not null && !RoleMenuService.TryParseDuration(temporary, out tempSeconds) ||
            expires is not null && !RoleMenuService.TryParseDuration(expires, out expireSeconds))
        {
            await ErrorAsync(Strings.RolemenuDurationInvalid(ctx.Guild.Id));
            return;
        }

        if (temporary is not null)
        {
            draft.TempRoleSeconds = tempSeconds;
            changed = true;
        }

        if (expires is not null)
        {
            draft.ExpiresAt = expireSeconds == 0 ? null : DateTime.UtcNow.AddSeconds(expireSeconds);
            changed = true;
        }

        if (clearBlockedRoles)
        {
            draft.BlockedRoleIds = [];
            changed = true;
        }

        if (blockRole is not null)
        {
            if (!draft.BlockedRoleIds.Contains(blockRole.Id))
                draft.BlockedRoleIds.Add(blockRole.Id);
            changed = true;
        }

        if (unblockRole is not null)
        {
            draft.BlockedRoleIds.Remove(unblockRole.Id);
            changed = true;
        }

        if (name is not null)
        {
            var trimmed = name.Trim();
            if (trimmed.Length is 0 or > RoleMenuService.NameLength)
            {
                await ErrorAsync(Strings.RolemenuNameInvalid(ctx.Guild.Id));
                return;
            }

            draft.Name = trimmed;
            changed = true;
        }

        if (style.HasValue)
        {
            draft.Style = style.Value;
            changed = true;
        }

        if (mode.HasValue)
        {
            if (mode.Value != RoleMenuMode.Exclusive && found.Mode == (int)RoleMenuMode.Exclusive && !max.HasValue)
                draft.MaxRoles = 0;
            draft.Mode = mode.Value;
            changed = true;
        }

        if (min.HasValue || max.HasValue)
        {
            if (min is < 0 || max is < 0)
            {
                await ErrorAsync(Strings.RolemenuLimitsInvalid(ctx.Guild.Id));
                return;
            }

            if (min.HasValue)
                draft.MinRoles = min.Value;
            if (max.HasValue)
                draft.MaxRoles = max.Value;
            changed = true;
        }

        if (clearRequiredRole)
        {
            draft.RequiredRoleId = null;
            changed = true;
        }
        else if (requiredRole is not null)
        {
            draft.RequiredRoleId = requiredRole.Id;
            changed = true;
        }

        if (confirmation.HasValue)
        {
            draft.ReplyMode = confirmation.Value;
            changed = true;
        }

        if (placeholder is not null)
        {
            draft.Placeholder = placeholder;
            changed = true;
        }

        if (!changed)
        {
            await ConfirmAsync(Strings.RolemenuNoChange(ctx.Guild.Id));
            return;
        }

        await DeferAsync(true);

        if (group is not null && !clearGroup)
        {
            linked = await Service.EnsureGroupAsync(ctx.Guild.Id, group);
            if (linked is null)
            {
                await ErrorAsync(Strings.RolemenuGroupInvalid(ctx.Guild.Id));
                return;
            }

            draft.GroupId = linked.Id;
        }

        if (await SaveAsync(found, draft) is not { } updated)
        {
            if (linked is not null)
                await Service.PruneUnusedGroupAsync(linked.Id);
            return;
        }

        var guildId = ctx.Guild.Id;
        var lines = new List<string>();
        if (name is not null)
            lines.Add(Strings.RolemenuNameSet(guildId, updated.Name));
        if (style.HasValue)
            lines.Add(Strings.RolemenuStyleSet(guildId, updated.Name,
                Service.StyleText(guildId, (RoleMenuStyle)updated.Style)));
        if (mode.HasValue)
            lines.Add(Strings.RolemenuModeSet(guildId, updated.Name,
                Service.ModeText(guildId, (RoleMenuMode)updated.Mode)));
        if (min.HasValue || max.HasValue || mode.HasValue)
            lines.Add(Strings.RolemenuLimitsSet(guildId, updated.Name, updated.MinRoles,
                Service.MaxText(guildId, updated.MaxRoles)));
        if (clearRequiredRole)
            lines.Add(Strings.RolemenuRequiredCleared(guildId, updated.Name));
        else if (requiredRole is not null)
            lines.Add(Strings.RolemenuRequiredSet(guildId, requiredRole.Mention, updated.Name));
        if (confirmation.HasValue)
            lines.Add(Strings.RolemenuReplySet(guildId, updated.Name,
                Service.ReplyText(guildId, (RoleMenuReplyMode)updated.ReplyMode)));
        if (temporary is not null)
            lines.Add(tempSeconds == 0
                ? Strings.RolemenuTempCleared(guildId, updated.Name)
                : Strings.RolemenuTempSet(guildId, updated.Name, Service.DurationText(guildId, tempSeconds)));
        if (expires is not null)
            lines.Add(updated.ExpiresAt is not { } expiresAt
                ? Strings.RolemenuExpireCleared(guildId, updated.Name)
                : Strings.RolemenuExpireSet(guildId, updated.Name,
                    TimestampTag.FromDateTime(DateTime.SpecifyKind(expiresAt, DateTimeKind.Utc),
                        TimestampTagStyles.Relative).ToString()));
        if (clearBlockedRoles || blockRole is not null || unblockRole is not null)
        {
            var blocked = RoleMenuService.ParseRoleIds(updated.BlockedRoleIds);
            lines.Add(blocked.Count == 0
                ? Strings.RolemenuBlockedCleared(guildId, updated.Name)
                : Strings.RolemenuBlockedSet(guildId, updated.Name, RoleMenuService.RoleMentions(blocked)));
        }

        if (color is not null)
            lines.Add(updated.AccentColor is { } accentValue
                ? Strings.RolemenuColorSet(guildId, updated.Name, RoleMenuService.ColorText(accentValue))
                : Strings.RolemenuColorCleared(guildId, updated.Name));
        if (buttonColor.HasValue)
            lines.Add(updated.ButtonColor == 0
                ? Strings.RolemenuButtonColorCleared(guildId, updated.Name)
                : Strings.RolemenuButtonColorSet(guildId, updated.Name,
                    RoleMenuService.ButtonColorText((RoleMenuButtonColor)updated.ButtonColor)));
        if (group is not null)
            lines.Add(linked is null
                ? Strings.RolemenuGroupCleared(guildId, updated.Name)
                : Strings.RolemenuGroupSet(guildId, updated.Name, linked.Name));
        if (groupLimit.HasValue && updated.GroupId is { } updatedGroupId &&
            await Service.SetGroupMaxAsync(guildId, updatedGroupId, groupLimit.Value) is { } limited)
            lines.Add(groupLimit.Value == 0
                ? Strings.RolemenuGroupMaxCleared(guildId, limited.Name)
                : Strings.RolemenuGroupMaxSet(guildId, limited.Name, groupLimit.Value));

        if (lines.Count == 0)
            lines.Add(Strings.RolemenuMessageSet(guildId, updated.Name));

        var summary = string.Join('\n', lines);
        await ConfirmAsync(summary);
    }

    /// <summary>
    ///     Lists this server's role menu groups.
    /// </summary>
    [SlashCommand("groups", "Lists the role menu groups")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageRoles)]
    [RequireBotPermission(GuildPermission.ManageRoles)]
    [CheckPermissions]
    public async Task Groups()
    {
        var list = Service.GetGroups(ctx.Guild.Id);
        if (list.Count == 0)
        {
            await ConfirmAsync(Strings.RolemenuGroupListEmpty(ctx.Guild.Id, await Service.GetPrefixAsync(ctx.Guild)));
            return;
        }

        await RespondAsync(embed: Service.BuildGroupListEmbed((SocketGuild)ctx.Guild, list).Build(),
            ephemeral: true);
    }

    /// <summary>
    ///     Deletes a group and unlinks its menus.
    /// </summary>
    /// <param name="name">The group name.</param>
    [SlashCommand("group-delete", "Deletes a role menu group and unlinks its menus")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageRoles)]
    [RequireBotPermission(GuildPermission.ManageRoles)]
    [CheckPermissions]
    public async Task GroupDelete(string name)
    {
        var group = Service.GetGroups(ctx.Guild.Id)
            .FirstOrDefault(x => string.Equals(x.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));
        if (group is null || !await Service.DeleteGroupAsync(ctx.Guild.Id, group.Id))
        {
            await ErrorAsync(Strings.RolemenuGroupNotFound(ctx.Guild.Id, name.Trim()));
            return;
        }

        await ConfirmAsync(Strings.RolemenuGroupDeleted(ctx.Guild.Id, group.Name));
    }

    /// <summary>
    ///     Creates a channel for role menus and can post a menu in it.
    /// </summary>
    /// <param name="name">The channel name.</param>
    /// <param name="category">The category to put it in.</param>
    /// <param name="style">Dropdown or buttons, used when a role is given.</param>
    /// <param name="role">A first role to offer in a menu posted there.</param>
    [SlashCommand("channel", "Creates a channel for role menus where members can react but not send messages")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageRoles | GuildPermission.ManageChannels)]
    [RequireBotPermission(GuildPermission.ManageRoles | GuildPermission.ManageChannels)]
    [CheckPermissions]
    public async Task Channel(string name, ICategoryChannel? category = null,
        RoleMenuStyle style = RoleMenuStyle.Dropdown, IRole? role = null)
    {
        await DeferAsync(true);

        var channel = await Service.CreateMenuChannelAsync((SocketGuild)ctx.Guild, name, category);
        if (channel is null)
        {
            await ErrorAsync(Strings.RolemenuChannelFailed(ctx.Guild.Id));
            return;
        }

        if (role is null)
        {
            await ConfirmAsync(Strings.RolemenuChannelCreated(ctx.Guild.Id, channel.Mention));
            return;
        }

        var result = await Service.CreateAsync(ctx.Guild.Id, (IGuildUser)ctx.User, new RoleMenuDraft
        {
            ChannelId = channel.Id,
            Style = style,
            Mode = RoleMenuMode.Multi,
            ReplyMode = RoleMenuReplyMode.Private,
            Options =
            [
                new RoleMenuOptionDraft
                {
                    RoleId = role.Id
                }
            ]
        });
        if (!result.Success || result.Menu is null)
        {
            await ErrorAsync(Service.DescribeError(ctx.Guild.Id, result, null, channel.Id));
            return;
        }

        await ConfirmAsync(Strings.RolemenuChannelCreatedMenu(ctx.Guild.Id, channel.Mention, result.Menu.Name,
            result.Menu.Id, RoleMenuService.GetJumpUrl(result.Menu) ?? channel.Mention));
    }

    /// <summary>
    ///     Sets or clears the message above a menu.
    /// </summary>
    /// <param name="menu">The menu ID.</param>
    /// <param name="text">Plain text or embed builder JSON.</param>
    /// <param name="clear">Go back to the default message.</param>
    [SlashCommand("message", "Sets the message above a menu, as text or embed builder JSON")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageRoles)]
    [RequireBotPermission(GuildPermission.ManageRoles)]
    [CheckPermissions]
    public async Task Message(
        [Summary("menu", "The role menu")] [Autocomplete(typeof(RoleMenuAutocompleter))]
        int menu,
        string? text = null,
        bool clear = false)
    {
        var found = await FindMenuAsync(menu);
        if (found is null)
            return;

        if (!clear && string.IsNullOrWhiteSpace(text))
        {
            if (string.IsNullOrWhiteSpace(found.Message))
            {
                await EphemeralReplyConfirmAsync(Strings.RolemenuMessageDefault(ctx.Guild.Id, found.Name));
                return;
            }

            var shown = Format.Code(found.Message.TrimTo(3800));
            await EphemeralReplyConfirmAsync(Strings.RolemenuMessageCurrent(ctx.Guild.Id, found.Name, shown));
            return;
        }

        await DeferAsync(true);

        var draft = RoleMenuDraft.From(found);
        draft.Message = clear ? null : text;

        if (await SaveAsync(found, draft) is not { } updated)
            return;

        await ConfirmAsync(clear
            ? Strings.RolemenuMessageCleared(ctx.Guild.Id, updated.Name)
            : Strings.RolemenuMessageSet(ctx.Guild.Id, updated.Name));
    }

    /// <summary>
    ///     Pauses a menu, or resumes it when it is paused.
    /// </summary>
    /// <param name="menu">The menu ID.</param>
    [SlashCommand("pause", "Pauses or resumes a menu")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageRoles)]
    [RequireBotPermission(GuildPermission.ManageRoles)]
    [CheckPermissions]
    public async Task Pause(
        [Summary("menu", "The role menu")] [Autocomplete(typeof(RoleMenuAutocompleter))]
        int menu)
    {
        var found = await FindMenuAsync(menu);
        if (found is null)
            return;

        await DeferAsync(true);

        var result = await Service.SetEnabledAsync(ctx.Guild.Id, menu, !found.Enabled);
        if (!result.Success || result.Menu is null)
        {
            await ErrorAsync(Service.DescribeError(ctx.Guild.Id, result, menu, found.ChannelId));
            return;
        }

        await ConfirmAsync(result.Menu.Enabled
            ? Strings.RolemenuResumed(ctx.Guild.Id, result.Menu.Name)
            : Strings.RolemenuPausedSet(ctx.Guild.Id, result.Menu.Name));
    }

    /// <summary>
    ///     Posts a fresh copy of a menu and deletes the old message.
    /// </summary>
    /// <param name="menu">The menu ID.</param>
    /// <param name="channel">Where to post it. Defaults to its current channel.</param>
    [SlashCommand("repost", "Posts a fresh copy of a menu")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageRoles)]
    [RequireBotPermission(GuildPermission.ManageRoles)]
    [CheckPermissions]
    public async Task Repost(
        [Summary("menu", "The role menu")] [Autocomplete(typeof(RoleMenuAutocompleter))]
        int menu,
        ITextChannel? channel = null)
    {
        var found = await FindMenuAsync(menu);
        if (found is null)
            return;

        await DeferAsync(true);

        var result = await Service.RepostAsync(ctx.Guild.Id, menu, channel?.Id);
        if (!result.Success || result.Menu is null)
        {
            await ErrorAsync(Service.DescribeError(ctx.Guild.Id, result, menu, channel?.Id ?? found.ChannelId));
            return;
        }

        await ConfirmAsync(Strings.RolemenuReposted(ctx.Guild.Id, result.Menu.Name,
            RoleMenuService.GetJumpUrl(result.Menu) ?? MentionUtils.MentionChannel(result.Menu.ChannelId)));
    }

    /// <summary>
    ///     Deletes a menu and its message after asking first.
    /// </summary>
    /// <param name="menu">The menu ID.</param>
    [SlashCommand("delete", "Deletes a menu and its message")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageRoles)]
    [RequireBotPermission(GuildPermission.ManageRoles)]
    [CheckPermissions]
    public async Task Delete(
        [Summary("menu", "The role menu")] [Autocomplete(typeof(RoleMenuAutocompleter))]
        int menu)
    {
        var found = await FindMenuAsync(menu);
        if (found is null)
            return;

        var prompt = Strings.RolemenuDeleteConfirm(ctx.Guild.Id, found.Name,
            MentionUtils.MentionChannel(found.ChannelId));
        if (!await PromptUserConfirmAsync(prompt, ctx.User.Id, true))
            return;

        var result = await Service.DeleteAsync(ctx.Guild.Id, menu);
        if (!result.Success)
        {
            await SendEphemeralFollowupErrorAsync(Service.DescribeError(ctx.Guild.Id, result, menu));
            return;
        }

        await ctx.Interaction.SendEphemeralFollowupConfirmAsync(Strings.RolemenuDeleted(ctx.Guild.Id, found.Name));
    }

    /// <summary>
    ///     Moves an older emoji role setup into a role menu.
    /// </summary>
    /// <param name="message">The message ID or link of the older setup.</param>
    /// <param name="style">Dropdown or buttons.</param>
    [SlashCommand("import", "Moves an older emoji role setup to a role menu")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageRoles)]
    [RequireBotPermission(GuildPermission.ManageRoles)]
    [CheckPermissions]
    public async Task Import(string message, RoleMenuStyle style)
    {
        var segment = message.Trim().TrimEnd('/').Split('/').LastOrDefault() ?? "";
        if (!ulong.TryParse(segment, out var messageId))
        {
            await ErrorAsync(Strings.RolemenuImportNotFound(ctx.Guild.Id, message));
            return;
        }

        var sources = await Service.GetImportSourcesAsync(ctx.Guild.Id);
        var source = sources.FirstOrDefault(x => x.MessageId == messageId);
        if (source is null)
        {
            await ErrorAsync(Strings.RolemenuImportNotFound(ctx.Guild.Id, messageId));
            return;
        }

        await DeferAsync(true);

        var result = await Service.ImportAsync(ctx.Guild.Id, (IGuildUser)ctx.User, new RoleMenuImportDraft
        {
            SourceId = source.Id,
            Style = style,
            CopyMessage = true,
            RetireOriginal = true
        });
        if (!result.Success || result.Menu is null)
        {
            await ErrorAsync(Service.DescribeError(ctx.Guild.Id, result, null, source.ChannelId));
            return;
        }

        await ConfirmAsync(Strings.RolemenuImported(ctx.Guild.Id, result.Menu.Name, result.Menu.Id,
            RoleMenuService.GetJumpUrl(result.Menu) ?? MentionUtils.MentionChannel(result.Menu.ChannelId)));
    }

    /// <summary>
    ///     Previews a premade role menu that creates its roles when posted.
    /// </summary>
    /// <param name="template">The premade menu.</param>
    /// <param name="channel">The channel to post in.</param>
    /// <param name="style">Dropdown, buttons, or reactions. Defaults to the template's own.</param>
    [SlashCommand("template", "Previews a premade role menu with its roles, ready to edit and post")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageRoles)]
    [RequireBotPermission(GuildPermission.ManageRoles)]
    [CheckPermissions]
    public async Task Template(RoleMenuTemplateKind template, ITextChannel channel, RoleMenuStyle? style = null)
    {
        var session = Service.StartTemplate(ctx.Guild.Id, ctx.User.Id, template, channel.Id, style);
        await RespondAsync(embed: Service.BuildTemplateEmbed(ctx.Guild.Id, session).Build(),
            components: Service.BuildTemplateComponents(ctx.Guild.Id, ctx.User.Id), ephemeral: true);
    }

    /// <summary>
    ///     Creates the roles of a template draft and posts its menu.
    /// </summary>
    /// <param name="userIdRaw">The ID of the person the draft belongs to.</param>
    [ComponentInteraction("rolemenu:tpl:post:*", true)]
    [RequireContext(ContextType.Guild)]
    public async Task HandleTemplatePost(string userIdRaw)
    {
        var session = await GetOwnTemplateAsync(userIdRaw);
        if (session is null)
            return;

        await DeferAsync();
        var result = await Service.PostTemplateAsync((SocketGuild)ctx.Guild, (IGuildUser)ctx.User, session);
        if (!result.Success || result.Menu is null)
        {
            await FollowupAsync(Service.DescribeError(ctx.Guild.Id, result, null, session.ChannelId),
                ephemeral: true);
            return;
        }

        var text = Strings.RolemenuCreated(ctx.Guild.Id, result.Menu.Name, result.Menu.Id,
            RoleMenuService.GetJumpUrl(result.Menu) ?? MentionUtils.MentionChannel(result.Menu.ChannelId));
        await ((SocketMessageComponent)ctx.Interaction).ModifyOriginalResponseAsync(p =>
        {
            p.Content = text;
            p.Embeds = Array.Empty<Discord.Embed>();
            p.Components = new ComponentBuilder().Build();
        });
    }

    /// <summary>
    ///     Opens the edit box for a template draft.
    /// </summary>
    /// <param name="userIdRaw">The ID of the person the draft belongs to.</param>
    [ComponentInteraction("rolemenu:tpl:edit:*", true)]
    [RequireContext(ContextType.Guild)]
    public async Task HandleTemplateEdit(string userIdRaw)
    {
        var session = await GetOwnTemplateAsync(userIdRaw);
        if (session is null)
            return;

        var guildId = ctx.Guild.Id;
        var modal = new ModalBuilder()
            .WithTitle(Strings.RolemenuTemplateEditTitle(guildId))
            .WithCustomId($"rolemenu:tpl:modal:{ctx.User.Id}")
            .AddTextInput(Strings.RolemenuTemplateNameLabel(guildId), "name", TextInputStyle.Short,
                value: session.Name, minLength: 1, maxLength: RoleMenuService.NameLength)
            .AddTextInput(Strings.RolemenuTemplateMessageLabel(guildId), "message", TextInputStyle.Paragraph,
                required: false, value: session.Message, maxLength: 4000)
            .AddTextInput(Strings.RolemenuTemplateRolesLabel(guildId), "entries", TextInputStyle.Paragraph,
                value: RoleMenuService.EntriesToText(session.Entries), minLength: 1, maxLength: 4000);
        await RespondWithModalAsync(modal.Build());
    }

    /// <summary>
    ///     Throws away a template draft.
    /// </summary>
    /// <param name="userIdRaw">The ID of the person the draft belongs to.</param>
    [ComponentInteraction("rolemenu:tpl:cancel:*", true)]
    [RequireContext(ContextType.Guild)]
    public async Task HandleTemplateCancel(string userIdRaw)
    {
        if (await GetOwnTemplateAsync(userIdRaw) is null)
            return;

        Service.DropTemplateSession(ctx.Guild.Id, ctx.User.Id);
        await ((SocketMessageComponent)ctx.Interaction).UpdateAsync(p =>
        {
            p.Content = Strings.RolemenuTemplateCancelled(ctx.Guild.Id);
            p.Embeds = Array.Empty<Discord.Embed>();
            p.Components = new ComponentBuilder().Build();
        });
    }

    /// <summary>
    ///     Applies the edit box to a template draft and shows the preview again.
    /// </summary>
    /// <param name="userIdRaw">The ID of the person the draft belongs to.</param>
    /// <param name="modal">What was typed.</param>
    [ModalInteraction("rolemenu:tpl:modal:*", true)]
    [RequireContext(ContextType.Guild)]
    public async Task HandleTemplateModal(string userIdRaw, RoleMenuTemplateModal modal)
    {
        var session = await GetOwnTemplateAsync(userIdRaw);
        if (session is null)
            return;

        if (!Service.TryEditTemplate(session, modal.Name, modal.Message, modal.Entries))
        {
            await RespondAsync(Strings.RolemenuTemplateInvalid(ctx.Guild.Id, RoleMenuService.LabelLength),
                ephemeral: true);
            return;
        }

        await ((SocketModal)ctx.Interaction).UpdateAsync(p =>
        {
            p.Embed = Service.BuildTemplateEmbed(ctx.Guild.Id, session).Build();
            p.Components = Service.BuildTemplateComponents(ctx.Guild.Id, ctx.User.Id);
        });
    }

    /// <summary>
    ///     Gets the template draft a button or edit box belongs to, replying when it is someone else's or has expired.
    /// </summary>
    /// <param name="userIdRaw">The ID of the person the draft belongs to.</param>
    /// <returns>The draft, or null after replying.</returns>
    private async Task<RoleMenuTemplateSession?> GetOwnTemplateAsync(string userIdRaw)
    {
        if (!ulong.TryParse(userIdRaw, out var userId) || userId != ctx.User.Id ||
            !((IGuildUser)ctx.User).GuildPermissions.ManageRoles)
        {
            await RespondAsync(Strings.RolemenuTemplateNotYours(ctx.Guild.Id), ephemeral: true);
            return null;
        }

        var session = Service.GetTemplateSession(ctx.Guild.Id, userId);
        if (session is null)
            await RespondAsync(Strings.RolemenuTemplateExpired(ctx.Guild.Id), ephemeral: true);
        return session;
    }

    /// <summary>
    ///     Handles a click on a role menu button.
    /// </summary>
    /// <param name="menuIdRaw">The menu ID from the button.</param>
    /// <param name="optionIdRaw">The option ID from the button.</param>
    [ComponentInteraction("rolemenu:btn:*:*", true)]
    [RequireContext(ContextType.Guild)]
    public async Task HandleRoleMenuButton(string menuIdRaw, string optionIdRaw)
    {
        if (!int.TryParse(menuIdRaw, out var menuId) || !int.TryParse(optionIdRaw, out var optionId))
            return;

        await Service.HandleButtonAsync((SocketMessageComponent)ctx.Interaction, menuId, optionId);
    }

    /// <summary>
    ///     Handles a pick on a role menu dropdown.
    /// </summary>
    /// <param name="menuIdRaw">The menu ID from the dropdown.</param>
    [ComponentInteraction("rolemenu:sel:*", true)]
    [RequireContext(ContextType.Guild)]
    public async Task HandleRoleMenuSelect(string menuIdRaw)
    {
        if (!int.TryParse(menuIdRaw, out var menuId))
            return;

        await Service.HandleSelectAsync((SocketMessageComponent)ctx.Interaction, menuId);
    }

    /// <summary>
    ///     Finds a menu in this server, replying with an error when it does not exist.
    /// </summary>
    /// <param name="menuId">The menu ID.</param>
    /// <returns>The menu, or null after replying.</returns>
    private async Task<RoleMenu?> FindMenuAsync(int menuId)
    {
        var menu = await Service.GetGuildMenuAsync(ctx.Guild.Id, menuId);
        if (menu is null)
            await ErrorAsync(Strings.RolemenuNotFound(ctx.Guild.Id, menuId));
        return menu;
    }

    /// <summary>
    ///     Saves a changed draft, replying with an error when the change is refused.
    /// </summary>
    /// <param name="menu">The menu being changed.</param>
    /// <param name="draft">The changed draft.</param>
    /// <returns>The saved menu, or null after replying.</returns>
    private async Task<RoleMenu?> SaveAsync(RoleMenu menu, RoleMenuDraft draft)
    {
        var result = await Service.UpdateAsync(ctx.Guild.Id, menu.Id, (IGuildUser)ctx.User, draft);
        if (result.Success && result.Menu is not null)
            return result.Menu;

        await ErrorAsync(Service.DescribeError(ctx.Guild.Id, result, menu.Id, draft.ChannelId));
        return null;
    }
}
