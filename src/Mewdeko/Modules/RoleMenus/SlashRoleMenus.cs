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
        RoleMenuButtonColor color = RoleMenuButtonColor.Grey)
    {
        var found = await FindMenuAsync(menu);
        if (found is null)
            return;

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
            ButtonStyle = (int)color
        });

        if (await SaveAsync(found, draft) is not { } updated)
            return;

        await ConfirmAsync(Strings.RolemenuOptionAdded(ctx.Guild.Id, role.Mention, updated.Name));
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
        string? placeholder = null)
    {
        var found = await FindMenuAsync(menu);
        if (found is null)
            return;

        var draft = RoleMenuDraft.From(found);
        var changed = false;

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
            if (mode.Value == RoleMenuMode.Multi && found.Mode == (int)RoleMenuMode.Exclusive && !max.HasValue)
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

        if (await SaveAsync(found, draft) is not { } updated)
            return;

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
        if (lines.Count == 0)
            lines.Add(Strings.RolemenuMessageSet(guildId, updated.Name));

        var summary = string.Join('\n', lines);
        await ConfirmAsync(summary);
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
