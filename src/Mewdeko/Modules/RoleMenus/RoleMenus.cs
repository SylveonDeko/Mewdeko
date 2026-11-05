using DataModel;
using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Common.TypeReaders.Models;
using Mewdeko.Modules.RoleMenus.Common;
using Mewdeko.Modules.RoleMenus.Services;

namespace Mewdeko.Modules.RoleMenus;

/// <summary>
///     Text commands for menus that let members pick their own roles.
/// </summary>
public class RoleMenus : MewdekoModuleBase<RoleMenuService>
{
    /// <summary>
    ///     Lists every role menu in this server.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageRoles)]
    [BotPerm(GuildPermission.ManageRoles)]
    public async Task RoleMenuList()
    {
        var list = await Service.GetMenusAsync(ctx.Guild.Id);
        if (list.Count == 0)
        {
            var prefix = await Service.GetPrefixAsync(ctx.Guild);
            await ConfirmAsync(Strings.RolemenuListEmpty(ctx.Guild.Id, prefix));
            return;
        }

        var embed = Service.BuildListEmbed((SocketGuild)ctx.Guild, list);
        await ctx.Channel.SendMessageAsync(embed: embed.Build());
    }

    /// <summary>
    ///     Shows one role menu's settings and options.
    /// </summary>
    /// <param name="menuId">The menu ID.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageRoles)]
    [BotPerm(GuildPermission.ManageRoles)]
    public async Task RoleMenuInfo(int menuId)
    {
        var menu = await FindMenuAsync(menuId);
        if (menu is null)
            return;

        var embed = Service.BuildInfoEmbed((SocketGuild)ctx.Guild, menu);
        await ctx.Channel.SendMessageAsync(embed: embed.Build());
    }

    /// <summary>
    ///     Posts a new role menu with one option per role.
    /// </summary>
    /// <param name="channel">The channel to post in.</param>
    /// <param name="style">Dropdown or buttons.</param>
    /// <param name="roles">The roles to offer.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageRoles)]
    [BotPerm(GuildPermission.ManageRoles)]
    public async Task RoleMenuCreate(ITextChannel channel, RoleMenuStyle style, params IRole[] roles)
    {
        if (roles.Length == 0)
        {
            await ErrorAsync(Strings.RolemenuNeedsOption(ctx.Guild.Id));
            return;
        }

        var draft = new RoleMenuDraft
        {
            ChannelId = channel.Id,
            Style = style,
            Mode = RoleMenuMode.Multi,
            ReplyMode = RoleMenuReplyMode.Private,
            Options = roles
                .DistinctBy(r => r.Id)
                .Select(r => new RoleMenuOptionDraft
                {
                    RoleId = r.Id
                })
                .ToList()
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
    ///     Previews a premade role menu that creates its roles when posted.
    /// </summary>
    /// <param name="template">The premade menu.</param>
    /// <param name="channel">The channel to post in.</param>
    /// <param name="style">Dropdown, buttons, or reactions. Defaults to the template's own.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageRoles)]
    [BotPerm(GuildPermission.ManageRoles)]
    public async Task RoleMenuUseTemplate(RoleMenuTemplateKind template, ITextChannel channel,
        RoleMenuStyle? style = null)
    {
        var session = Service.StartTemplate(ctx.Guild.Id, ctx.User.Id, template, channel.Id, style);
        await ctx.Channel.SendMessageAsync(embed: Service.BuildTemplateEmbed(ctx.Guild.Id, session).Build(),
            components: Service.BuildTemplateComponents(ctx.Guild.Id, ctx.User.Id));
    }

    /// <summary>
    ///     Adds a role to a menu, with an optional emoji and name.
    /// </summary>
    /// <param name="menuId">The menu ID.</param>
    /// <param name="role">The role to add.</param>
    /// <param name="emojiAndLabel">An optional emoji followed by the name shown on the menu.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageRoles)]
    [BotPerm(GuildPermission.ManageRoles)]
    public async Task RoleMenuAdd(int menuId, IRole role, [Remainder] string? emojiAndLabel = null)
    {
        var menu = await FindMenuAsync(menuId);
        if (menu is null)
            return;

        if (menu.Options.Any(o => o.RoleId == role.Id))
        {
            await ErrorAsync(Strings.RolemenuOptionExists(ctx.Guild.Id, role.Mention));
            return;
        }

        if (menu.Options.Count >= RoleMenuService.MaxOptions)
        {
            await ErrorAsync(Strings.RolemenuTooManyOptions(ctx.Guild.Id));
            return;
        }

        string? emoji = null;
        var label = emojiAndLabel?.Trim();
        if (!string.IsNullOrEmpty(label))
        {
            var parts = label.Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0 && parts[0].TryToIEmote(out _))
            {
                emoji = parts[0];
                label = parts.Length > 1 ? parts[1].Trim() : null;
            }
        }

        var draft = RoleMenuDraft.From(menu);
        draft.Options.Add(new RoleMenuOptionDraft
        {
            RoleId = role.Id, Label = label, Emoji = emoji
        });

        if (await SaveAsync(menu, draft) is not { } updated)
            return;

        await ConfirmAsync(Strings.RolemenuOptionAdded(ctx.Guild.Id, role.Mention, updated.Name));
    }

    /// <summary>
    ///     Removes a role from a menu.
    /// </summary>
    /// <param name="menuId">The menu ID.</param>
    /// <param name="role">The role to remove.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageRoles)]
    [BotPerm(GuildPermission.ManageRoles)]
    public async Task RoleMenuRemove(int menuId, IRole role)
    {
        var menu = await FindMenuAsync(menuId);
        if (menu is null)
            return;

        if (menu.Options.All(o => o.RoleId != role.Id))
        {
            await ErrorAsync(Strings.RolemenuOptionMissing(ctx.Guild.Id, role.Mention));
            return;
        }

        if (menu.Options.Count <= 1)
        {
            await ErrorAsync(Strings.RolemenuNeedsOption(ctx.Guild.Id));
            return;
        }

        var draft = RoleMenuDraft.From(menu);
        draft.Options.RemoveAll(o => o.RoleId == role.Id);

        if (await SaveAsync(menu, draft) is not { } updated)
            return;

        await ConfirmAsync(Strings.RolemenuOptionRemoved(ctx.Guild.Id, role.Mention, updated.Name));
    }

    /// <summary>
    ///     Renames a menu.
    /// </summary>
    /// <param name="menuId">The menu ID.</param>
    /// <param name="name">The new name.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageRoles)]
    [BotPerm(GuildPermission.ManageRoles)]
    public async Task RoleMenuName(int menuId, [Remainder] string name)
    {
        var menu = await FindMenuAsync(menuId);
        if (menu is null)
            return;

        var trimmed = name.Trim();
        if (trimmed.Length is 0 or > RoleMenuService.NameLength)
        {
            await ErrorAsync(Strings.RolemenuNameInvalid(ctx.Guild.Id));
            return;
        }

        var draft = RoleMenuDraft.From(menu);
        draft.Name = trimmed;

        if (await SaveAsync(menu, draft) is not { } updated)
            return;

        await ConfirmAsync(Strings.RolemenuNameSet(ctx.Guild.Id, updated.Name));
    }

    /// <summary>
    ///     Shows, sets, or clears the message above a menu.
    /// </summary>
    /// <param name="menuId">The menu ID.</param>
    /// <param name="message">The message, "clear", or nothing to view.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageRoles)]
    [BotPerm(GuildPermission.ManageRoles)]
    public async Task RoleMenuMessage(int menuId, [Remainder] string? message = null)
    {
        var menu = await FindMenuAsync(menuId);
        if (menu is null)
            return;

        if (string.IsNullOrWhiteSpace(message))
        {
            if (string.IsNullOrWhiteSpace(menu.Message))
            {
                await ConfirmAsync(Strings.RolemenuMessageDefault(ctx.Guild.Id, menu.Name));
                return;
            }

            var shown = Format.Code(menu.Message.TrimTo(3800));
            await ConfirmAsync(Strings.RolemenuMessageCurrent(ctx.Guild.Id, menu.Name, shown));
            return;
        }

        var clearing = message.Trim().Equals("clear", StringComparison.OrdinalIgnoreCase);
        var draft = RoleMenuDraft.From(menu);
        draft.Message = clearing ? null : message;

        if (await SaveAsync(menu, draft) is not { } updated)
            return;

        await ConfirmAsync(clearing
            ? Strings.RolemenuMessageCleared(ctx.Guild.Id, updated.Name)
            : Strings.RolemenuMessageSet(ctx.Guild.Id, updated.Name));
    }

    /// <summary>
    ///     Switches a menu between a dropdown and buttons.
    /// </summary>
    /// <param name="menuId">The menu ID.</param>
    /// <param name="style">Dropdown or buttons.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageRoles)]
    [BotPerm(GuildPermission.ManageRoles)]
    public async Task RoleMenuSetStyle(int menuId, RoleMenuStyle style)
    {
        var menu = await FindMenuAsync(menuId);
        if (menu is null)
            return;

        var draft = RoleMenuDraft.From(menu);
        draft.Style = style;

        if (await SaveAsync(menu, draft) is not { } updated)
            return;

        await ConfirmAsync(Strings.RolemenuStyleSet(ctx.Guild.Id, updated.Name,
            Service.StyleText(ctx.Guild.Id, (RoleMenuStyle)updated.Style)));
    }

    /// <summary>
    ///     Sets whether members pick one role or any number.
    /// </summary>
    /// <param name="menuId">The menu ID.</param>
    /// <param name="mode">Exclusive for pick one, multi for pick any.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageRoles)]
    [BotPerm(GuildPermission.ManageRoles)]
    public async Task RoleMenuSetMode(int menuId, RoleMenuMode mode)
    {
        var menu = await FindMenuAsync(menuId);
        if (menu is null)
            return;

        var draft = RoleMenuDraft.From(menu);
        draft.Mode = mode;
        if (mode != RoleMenuMode.Exclusive && menu.Mode == (int)RoleMenuMode.Exclusive)
            draft.MaxRoles = 0;

        if (await SaveAsync(menu, draft) is not { } updated)
            return;

        await ConfirmAsync(Strings.RolemenuModeSet(ctx.Guild.Id, updated.Name,
            Service.ModeText(ctx.Guild.Id, (RoleMenuMode)updated.Mode)));
    }

    /// <summary>
    ///     Sets how many roles from a menu a member must keep and can hold.
    /// </summary>
    /// <param name="menuId">The menu ID.</param>
    /// <param name="min">The fewest roles a member must keep once they pick.</param>
    /// <param name="max">The most roles a member can hold, 0 for no limit.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageRoles)]
    [BotPerm(GuildPermission.ManageRoles)]
    public async Task RoleMenuLimits(int menuId, int min, int max)
    {
        if (min < 0 || max < 0)
        {
            await ErrorAsync(Strings.RolemenuLimitsInvalid(ctx.Guild.Id));
            return;
        }

        var menu = await FindMenuAsync(menuId);
        if (menu is null)
            return;

        var draft = RoleMenuDraft.From(menu);
        draft.MinRoles = min;
        draft.MaxRoles = max;

        if (await SaveAsync(menu, draft) is not { } updated)
            return;

        await ConfirmAsync(Strings.RolemenuLimitsSet(ctx.Guild.Id, updated.Name, updated.MinRoles,
            Service.MaxText(ctx.Guild.Id, updated.MaxRoles)));
    }

    /// <summary>
    ///     Sets or clears the role needed to use a menu.
    /// </summary>
    /// <param name="menuId">The menu ID.</param>
    /// <param name="role">The required role. Omit to let anyone use the menu.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageRoles)]
    [BotPerm(GuildPermission.ManageRoles)]
    public async Task RoleMenuRequire(int menuId, IRole? role = null)
    {
        var menu = await FindMenuAsync(menuId);
        if (menu is null)
            return;

        var draft = RoleMenuDraft.From(menu);
        draft.RequiredRoleId = role?.Id;

        if (await SaveAsync(menu, draft) is not { } updated)
            return;

        if (role is null)
            await ConfirmAsync(Strings.RolemenuRequiredCleared(ctx.Guild.Id, updated.Name));
        else
            await ConfirmAsync(Strings.RolemenuRequiredSet(ctx.Guild.Id, role.Mention, updated.Name));
    }

    /// <summary>
    ///     Sets or clears how long roles from a menu last before they are taken back.
    /// </summary>
    /// <param name="menuId">The menu ID.</param>
    /// <param name="duration">How long roles last. Omit to keep them.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageRoles)]
    [BotPerm(GuildPermission.ManageRoles)]
    public async Task RoleMenuTemp(int menuId, StoopidTime? duration = null)
    {
        var menu = await FindMenuAsync(menuId);
        if (menu is null)
            return;

        var seconds = (int)Math.Min(duration?.Time.TotalSeconds ?? 0, RoleMenuService.MaxTempSeconds + 1);
        var draft = RoleMenuDraft.From(menu);
        draft.TempRoleSeconds = seconds;

        if (await SaveAsync(menu, draft) is not { } updated)
            return;

        await ConfirmAsync(seconds == 0
            ? Strings.RolemenuTempCleared(ctx.Guild.Id, updated.Name)
            : Strings.RolemenuTempSet(ctx.Guild.Id, updated.Name, Service.DurationText(ctx.Guild.Id, seconds)));
    }

    /// <summary>
    ///     Sets or clears how long one role on a menu lasts, overriding the menu's setting.
    /// </summary>
    /// <param name="menuId">The menu ID.</param>
    /// <param name="role">The role on the menu.</param>
    /// <param name="duration">How long the role lasts. Omit to follow the menu.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageRoles)]
    [BotPerm(GuildPermission.ManageRoles)]
    public async Task RoleMenuRoleTemp(int menuId, IRole role, StoopidTime? duration = null)
    {
        var menu = await FindMenuAsync(menuId);
        if (menu is null)
            return;

        var draft = RoleMenuDraft.From(menu);
        var option = draft.Options.FirstOrDefault(o => o.RoleId == role.Id);
        if (option is null)
        {
            await ErrorAsync(Strings.RolemenuOptionMissing(ctx.Guild.Id, role.Mention));
            return;
        }

        var seconds = (int)Math.Min(duration?.Time.TotalSeconds ?? 0, RoleMenuService.MaxTempSeconds + 1);
        option.TempRoleSeconds = seconds;

        if (await SaveAsync(menu, draft) is not { } updated)
            return;

        await ConfirmAsync(seconds == 0
            ? Strings.RolemenuOptionTempCleared(ctx.Guild.Id, role.Mention, updated.Name)
            : Strings.RolemenuOptionTempSet(ctx.Guild.Id, role.Mention, updated.Name,
                Service.DurationText(ctx.Guild.Id, seconds)));
    }

    /// <summary>
    ///     Sets or clears how many members can hold one role on a menu.
    /// </summary>
    /// <param name="menuId">The menu ID.</param>
    /// <param name="role">The role on the menu.</param>
    /// <param name="cap">The most members who can hold it, 0 for no limit.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageRoles)]
    [BotPerm(GuildPermission.ManageRoles)]
    public async Task RoleMenuCap(int menuId, IRole role, int cap = 0)
    {
        var menu = await FindMenuAsync(menuId);
        if (menu is null)
            return;

        var draft = RoleMenuDraft.From(menu);
        var option = draft.Options.FirstOrDefault(o => o.RoleId == role.Id);
        if (option is null)
        {
            await ErrorAsync(Strings.RolemenuOptionMissing(ctx.Guild.Id, role.Mention));
            return;
        }

        option.MaxHolders = Math.Max(cap, 0);

        if (await SaveAsync(menu, draft) is not { } updated)
            return;

        await ConfirmAsync(cap <= 0
            ? Strings.RolemenuCapCleared(ctx.Guild.Id, role.Mention, updated.Name)
            : Strings.RolemenuCapSet(ctx.Guild.Id, role.Mention, updated.Name, cap));
    }

    /// <summary>
    ///     Sets or clears the roles that stop a member from using a menu.
    /// </summary>
    /// <param name="menuId">The menu ID.</param>
    /// <param name="roles">The blocked roles. Omit to clear.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageRoles)]
    [BotPerm(GuildPermission.ManageRoles)]
    public async Task RoleMenuBlock(int menuId, params IRole[] roles)
    {
        var menu = await FindMenuAsync(menuId);
        if (menu is null)
            return;

        var draft = RoleMenuDraft.From(menu);
        draft.BlockedRoleIds = roles.Select(r => r.Id).Distinct().ToList();

        if (await SaveAsync(menu, draft) is not { } updated)
            return;

        await ConfirmAsync(roles.Length == 0
            ? Strings.RolemenuBlockedCleared(ctx.Guild.Id, updated.Name)
            : Strings.RolemenuBlockedSet(ctx.Guild.Id, updated.Name, string.Join(", ", roles.Select(r => r.Mention))));
    }

    /// <summary>
    ///     Sets or clears when a menu deletes its message and turns itself off.
    /// </summary>
    /// <param name="menuId">The menu ID.</param>
    /// <param name="duration">How long from now. Omit to never expire.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageRoles)]
    [BotPerm(GuildPermission.ManageRoles)]
    public async Task RoleMenuExpire(int menuId, StoopidTime? duration = null)
    {
        var menu = await FindMenuAsync(menuId);
        if (menu is null)
            return;

        var draft = RoleMenuDraft.From(menu);
        draft.ExpiresAt = duration is null ? null : DateTime.UtcNow.Add(duration.Time);

        if (await SaveAsync(menu, draft) is not { } updated)
            return;

        await ConfirmAsync(updated.ExpiresAt is not { } expiresAt
            ? Strings.RolemenuExpireCleared(ctx.Guild.Id, updated.Name)
            : Strings.RolemenuExpireSet(ctx.Guild.Id, updated.Name,
                TimestampTag.FromDateTime(DateTime.SpecifyKind(expiresAt, DateTimeKind.Utc),
                    TimestampTagStyles.Relative).ToString()));
    }

    /// <summary>
    ///     Links a menu into a group, or takes it out of its group.
    /// </summary>
    /// <param name="menuId">The menu ID.</param>
    /// <param name="name">The group name. Omit to unlink.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageRoles)]
    [BotPerm(GuildPermission.ManageRoles)]
    public async Task RoleMenuGroup(int menuId, [Remainder] string? name = null)
    {
        var menu = await FindMenuAsync(menuId);
        if (menu is null)
            return;

        var draft = RoleMenuDraft.From(menu);
        DataModel.RoleMenuGroup? group = null;
        if (name is not null)
        {
            group = await Service.EnsureGroupAsync(ctx.Guild.Id, name);
            if (group is null)
            {
                await ErrorAsync(Strings.RolemenuGroupInvalid(ctx.Guild.Id));
                return;
            }
        }

        draft.GroupId = group?.Id;

        if (await SaveAsync(menu, draft) is not { } updated)
        {
            if (group is not null)
                await Service.PruneUnusedGroupAsync(group.Id);
            return;
        }

        await ConfirmAsync(group is null
            ? Strings.RolemenuGroupCleared(ctx.Guild.Id, updated.Name)
            : Strings.RolemenuGroupSet(ctx.Guild.Id, updated.Name, group.Name));
    }

    /// <summary>
    ///     Sets how many roles a member can hold across a group's menus.
    /// </summary>
    /// <param name="max">The most roles, 1 for one at a time, 0 for no limit.</param>
    /// <param name="name">The group name.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageRoles)]
    [BotPerm(GuildPermission.ManageRoles)]
    public async Task RoleMenuGroupMax(int max, [Remainder] string name)
    {
        var group = Service.GetGroups(ctx.Guild.Id)
            .FirstOrDefault(x => string.Equals(x.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));
        if (group is null)
        {
            await ErrorAsync(Strings.RolemenuGroupNotFound(ctx.Guild.Id, name.Trim()));
            return;
        }

        if (await Service.SetGroupMaxAsync(ctx.Guild.Id, group.Id, max) is null)
        {
            await ErrorAsync(Strings.RolemenuLimitsInvalid(ctx.Guild.Id));
            return;
        }

        await ConfirmAsync(max == 0
            ? Strings.RolemenuGroupMaxCleared(ctx.Guild.Id, group.Name)
            : Strings.RolemenuGroupMaxSet(ctx.Guild.Id, group.Name, max));
    }

    /// <summary>
    ///     Deletes a group and unlinks its menus.
    /// </summary>
    /// <param name="name">The group name.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageRoles)]
    [BotPerm(GuildPermission.ManageRoles)]
    public async Task RoleMenuGroupDelete([Remainder] string name)
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
    ///     Lists this server's role menu groups.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageRoles)]
    public async Task RoleMenuGroups()
    {
        var list = Service.GetGroups(ctx.Guild.Id);
        if (list.Count == 0)
        {
            await ConfirmAsync(Strings.RolemenuGroupListEmpty(ctx.Guild.Id, await Service.GetPrefixAsync(ctx.Guild)));
            return;
        }

        await ctx.Channel.SendMessageAsync(embed: Service.BuildGroupListEmbed((SocketGuild)ctx.Guild, list).Build());
    }

    /// <summary>
    ///     Sets or clears the colour of a menu's default embed.
    /// </summary>
    /// <param name="menuId">The menu ID.</param>
    /// <param name="color">A name or hex code. Omit to go back to the default.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageRoles)]
    [BotPerm(GuildPermission.ManageRoles)]
    public async Task RoleMenuColor(int menuId, [Remainder] string? color = null)
    {
        var menu = await FindMenuAsync(menuId);
        if (menu is null)
            return;

        var draft = RoleMenuDraft.From(menu);
        if (color is null)
        {
            draft.AccentColor = null;
        }
        else if (RoleMenuService.TryParseAccent(color, out var rgb))
        {
            draft.AccentColor = rgb;
        }
        else
        {
            await ErrorAsync(Strings.RolemenuColorInvalid(ctx.Guild.Id));
            return;
        }

        if (await SaveAsync(menu, draft) is not { } updated)
            return;

        await ConfirmAsync(updated.AccentColor is { } accent
            ? Strings.RolemenuColorSet(ctx.Guild.Id, updated.Name, RoleMenuService.ColorText(accent))
            : Strings.RolemenuColorCleared(ctx.Guild.Id, updated.Name));
    }

    /// <summary>
    ///     Sets or clears the colour buttons use when an option has none of its own.
    /// </summary>
    /// <param name="menuId">The menu ID.</param>
    /// <param name="color">Blurple, grey, green or red. Omit to go back to grey.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageRoles)]
    [BotPerm(GuildPermission.ManageRoles)]
    public async Task RoleMenuButtonStyle(int menuId, RoleMenuButtonColor? color = null)
    {
        var menu = await FindMenuAsync(menuId);
        if (menu is null)
            return;

        var draft = RoleMenuDraft.From(menu);
        draft.ButtonColor = color is { } value && value != RoleMenuButtonColor.Grey ? (int)value : 0;

        if (await SaveAsync(menu, draft) is not { } updated)
            return;

        await ConfirmAsync(updated.ButtonColor == 0
            ? Strings.RolemenuButtonColorCleared(ctx.Guild.Id, updated.Name)
            : Strings.RolemenuButtonColorSet(ctx.Guild.Id, updated.Name,
                RoleMenuService.ButtonColorText((RoleMenuButtonColor)updated.ButtonColor)));
    }

    /// <summary>
    ///     Creates a channel for role menus where members can read and react but not send messages, and can post a menu
    ///     in it.
    /// </summary>
    /// <param name="name">The channel name.</param>
    /// <param name="style">Dropdown or buttons, used when roles are given.</param>
    /// <param name="roles">Roles to offer in a menu posted there. Omit to only create the channel.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageRoles | GuildPermission.ManageChannels)]
    [BotPerm(GuildPermission.ManageRoles | GuildPermission.ManageChannels)]
    public async Task RoleMenuChannel(string name, RoleMenuStyle style = RoleMenuStyle.Dropdown,
        params IRole[] roles)
    {
        var channel = await Service.CreateMenuChannelAsync((SocketGuild)ctx.Guild, name,
            (ctx.Channel as INestedChannel)?.CategoryId is { } categoryId
                ? await ctx.Guild.GetCategoryChannelAsync(categoryId)
                : null);
        if (channel is null)
        {
            await ErrorAsync(Strings.RolemenuChannelFailed(ctx.Guild.Id));
            return;
        }

        if (roles.Length == 0)
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
            Options = roles
                .DistinctBy(r => r.Id)
                .Select(r => new RoleMenuOptionDraft
                {
                    RoleId = r.Id
                })
                .ToList()
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
    ///     Chooses whether members get a private note after using a menu.
    /// </summary>
    /// <param name="menuId">The menu ID.</param>
    /// <param name="mode">Private or silent.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageRoles)]
    [BotPerm(GuildPermission.ManageRoles)]
    public async Task RoleMenuSetReply(int menuId, RoleMenuReplyMode mode)
    {
        var menu = await FindMenuAsync(menuId);
        if (menu is null)
            return;

        var draft = RoleMenuDraft.From(menu);
        draft.ReplyMode = mode;

        if (await SaveAsync(menu, draft) is not { } updated)
            return;

        await ConfirmAsync(Strings.RolemenuReplySet(ctx.Guild.Id, updated.Name,
            Service.ReplyText(ctx.Guild.Id, (RoleMenuReplyMode)updated.ReplyMode)));
    }

    /// <summary>
    ///     Pauses a menu, or resumes it when it is paused.
    /// </summary>
    /// <param name="menuId">The menu ID.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageRoles)]
    [BotPerm(GuildPermission.ManageRoles)]
    public async Task RoleMenuPause(int menuId)
    {
        var menu = await FindMenuAsync(menuId);
        if (menu is null)
            return;

        var result = await Service.SetEnabledAsync(ctx.Guild.Id, menuId, !menu.Enabled);
        if (!result.Success || result.Menu is null)
        {
            await ErrorAsync(Service.DescribeError(ctx.Guild.Id, result, menuId, menu.ChannelId));
            return;
        }

        await ConfirmAsync(result.Menu.Enabled
            ? Strings.RolemenuResumed(ctx.Guild.Id, result.Menu.Name)
            : Strings.RolemenuPausedSet(ctx.Guild.Id, result.Menu.Name));
    }

    /// <summary>
    ///     Posts a fresh copy of a menu and deletes the old message.
    /// </summary>
    /// <param name="menuId">The menu ID.</param>
    /// <param name="channel">Where to post it. Defaults to its current channel.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageRoles)]
    [BotPerm(GuildPermission.ManageRoles)]
    public async Task RoleMenuRepost(int menuId, ITextChannel? channel = null)
    {
        var menu = await FindMenuAsync(menuId);
        if (menu is null)
            return;

        var result = await Service.RepostAsync(ctx.Guild.Id, menuId, channel?.Id);
        if (!result.Success || result.Menu is null)
        {
            await ErrorAsync(Service.DescribeError(ctx.Guild.Id, result, menuId, channel?.Id ?? menu.ChannelId));
            return;
        }

        await ConfirmAsync(Strings.RolemenuReposted(ctx.Guild.Id, result.Menu.Name,
            RoleMenuService.GetJumpUrl(result.Menu) ?? MentionUtils.MentionChannel(result.Menu.ChannelId)));
    }

    /// <summary>
    ///     Deletes a menu and its message after asking first.
    /// </summary>
    /// <param name="menuId">The menu ID.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageRoles)]
    [BotPerm(GuildPermission.ManageRoles)]
    public async Task RoleMenuDelete(int menuId)
    {
        var menu = await FindMenuAsync(menuId);
        if (menu is null)
            return;

        var prompt = Strings.RolemenuDeleteConfirm(ctx.Guild.Id, menu.Name,
            MentionUtils.MentionChannel(menu.ChannelId));
        if (!await PromptUserConfirmAsync(prompt, ctx.User.Id))
            return;

        var result = await Service.DeleteAsync(ctx.Guild.Id, menuId);
        if (!result.Success)
        {
            await ErrorAsync(Service.DescribeError(ctx.Guild.Id, result, menuId));
            return;
        }

        await ConfirmAsync(Strings.RolemenuDeleted(ctx.Guild.Id, menu.Name));
    }

    /// <summary>
    ///     Moves an older emoji role setup on a message into a new role menu.
    /// </summary>
    /// <param name="messageId">The ID of the message with the older setup.</param>
    /// <param name="style">Dropdown or buttons.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageRoles)]
    [BotPerm(GuildPermission.ManageRoles)]
    public async Task RoleMenuImport(ulong messageId, RoleMenuStyle style = RoleMenuStyle.Dropdown)
    {
        var sources = await Service.GetImportSourcesAsync(ctx.Guild.Id);
        var source = sources.FirstOrDefault(x => x.MessageId == messageId);
        if (source is null)
        {
            await ErrorAsync(Strings.RolemenuImportNotFound(ctx.Guild.Id, messageId));
            return;
        }

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
