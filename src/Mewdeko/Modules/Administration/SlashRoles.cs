using System.Net;
using System.Text;
using DataModel;
using Discord.Interactions;
using Discord.Net;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Humanizer;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Modules.Administration.Services;
using Mewdeko.Modules.Moderation.Services;
using SkiaSharp;

namespace Mewdeko.Modules.Administration;

/// <summary>
///     Slash commands for managing roles, reaction roles, self-assignable roles, voice channel roles and auto-assign
///     roles.
/// </summary>
/// <param name="logger">The logger instance for structured logging.</param>
/// <param name="muteService">Service for managing timed roles and mutes.</param>
/// <param name="selfAssignedRoles">Service for self-assignable roles.</param>
[Group("roles", "Role management")]
public class SlashRoles(
    ILogger<SlashRoles> logger,
    MuteService muteService,
    SelfAssignedRolesService selfAssignedRoles)
    : MewdekoSlashModuleBase<RoleCommandsService>
{
    /// <summary>
    ///     Creates a new role with the specified name.
    /// </summary>
    /// <param name="roleName">The name of the role to create.</param>
    [SlashCommand("create", "Creates a new role")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageRoles)]
    [RequireBotPermission(GuildPermission.ManageRoles)]
    public async Task CreateRole([Summary("name", "The name of the new role")] string roleName)
    {
        if (string.IsNullOrWhiteSpace(roleName))
            return;

        var r = await ctx.Guild.CreateRoleAsync(roleName, isMentionable: false).ConfigureAwait(false);
        await ReplyConfirmAsync(Strings.Cr(ctx.Guild.Id, Format.Bold(r.Name))).ConfigureAwait(false);
    }

    /// <summary>
    ///     Deletes the specified role.
    /// </summary>
    /// <param name="role">The role to delete.</param>
    [SlashCommand("delete", "Deletes a role")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageRoles)]
    [RequireBotPermission(GuildPermission.ManageRoles)]
    public async Task DeleteRole([Summary("role", "The role to delete")] IRole role)
    {
        var guser = (IGuildUser)ctx.User;
        if (ctx.User.Id != guser.Guild.OwnerId
            && guser.GetRoles().Max(x => x.Position) <= role.Position)
        {
            await ReplyErrorAsync(Strings.Hierarchy(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await role.DeleteAsync().ConfigureAwait(false);
        await ReplyConfirmAsync(Strings.Dr(ctx.Guild.Id, Format.Bold(role.Name))).ConfigureAwait(false);
    }

    /// <summary>
    ///     Renames a role.
    /// </summary>
    /// <param name="roleToEdit">The role to rename.</param>
    /// <param name="newname">The new name for the role.</param>
    [SlashCommand("rename", "Renames a role")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageRoles)]
    [RequireBotPermission(GuildPermission.ManageRoles)]
    public async Task RenameRole(
        [Summary("role", "The role to rename")]
        IRole roleToEdit,
        [Summary("name", "The new name")] string newname)
    {
        var guser = (IGuildUser)ctx.User;
        if (ctx.User.Id != guser.Guild.OwnerId && guser.GetRoles().Max(x => x.Position) <= roleToEdit.Position)
        {
            await ReplyErrorAsync(Strings.Hierarchy(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        try
        {
            if (roleToEdit.Position > (await ctx.Guild.GetCurrentUserAsync().ConfigureAwait(false)).GetRoles()
                .Max(r => r.Position))
            {
                await ReplyErrorAsync(Strings.RenrolePerms(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await roleToEdit.ModifyAsync(g => g.Name = newname).ConfigureAwait(false);
            await ReplyConfirmAsync(Strings.Renrole(ctx.Guild.Id)).ConfigureAwait(false);
        }
        catch (Exception)
        {
            await ReplyErrorAsync(Strings.RenroleErr(ctx.Guild.Id)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Displays the hexadecimal color of the specified role, or changes it when a color is given.
    /// </summary>
    /// <param name="role">The role to display or change the color for.</param>
    /// <param name="color">The new color for the role, as a hex value like #ff0000. Omit to show the current color.</param>
    [SlashCommand("color", "Shows or changes the color of a role")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task RoleColor(
        [Summary("role", "The role")] IRole role,
        [Summary("color", "The new hex color, omit to show the current one")]
        string? color = null)
    {
        if (string.IsNullOrWhiteSpace(color))
        {
            await ctx.Interaction.SendConfirmAsync(Strings.Rolecolor(ctx.Guild.Id),
                    role.Colors.PrimaryColor.RawValue.ToString("x6"))
                .ConfigureAwait(false);
            return;
        }

        var guser = (IGuildUser)ctx.User;
        if (!guser.GuildPermissions.Has(GuildPermission.ManageRoles))
        {
            await ReplyErrorAsync(Strings.RcPerms(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        if (!SKColor.TryParse(color.Trim().TrimStart('#'), out var parsed))
        {
            await ReplyErrorAsync(Strings.RcPerms(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        try
        {
            await role.ModifyAsync(r => r.Color = new Color(parsed.Red, parsed.Green, parsed.Blue))
                .ConfigureAwait(false);
            await ReplyConfirmAsync(Strings.Rc(ctx.Guild.Id, Format.Bold(role.Name))).ConfigureAwait(false);
        }
        catch (Exception)
        {
            await ReplyErrorAsync(Strings.RcPerms(ctx.Guild.Id)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Toggles the hoist status of the specified role.
    /// </summary>
    /// <param name="role">The role to toggle the hoist status for.</param>
    [SlashCommand("hoist", "Toggles whether a role is shown separately in the member list")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageRoles)]
    [RequireBotPermission(GuildPermission.ManageRoles)]
    public async Task RoleHoist([Summary("role", "The role to toggle")] IRole role)
    {
        var newHoisted = !role.IsHoisted;
        await role.ModifyAsync(r => r.Hoist = newHoisted).ConfigureAwait(false);
        if (newHoisted)
        {
            await ReplyConfirmAsync(Strings.RolehoistEnabled(ctx.Guild.Id, Format.Bold(role.Name)))
                .ConfigureAwait(false);
        }
        else
        {
            await ReplyConfirmAsync(Strings.RolehoistDisabled(ctx.Guild.Id, Format.Bold(role.Name)))
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Adds a role to a user, permanently or for a specified time duration.
    /// </summary>
    /// <param name="targetUser">The user to add the role to.</param>
    /// <param name="roleToAdd">The role to add to the user.</param>
    /// <param name="time">The duration for which the role will be active. Omit to add it permanently.</param>
    [SlashCommand("set", "Adds a role to a user, optionally for a limited time")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageRoles)]
    [RequireBotPermission(GuildPermission.ManageRoles)]
    public async Task SetRole(
        [Summary("user", "The user to give the role to")]
        IGuildUser targetUser,
        [Summary("role", "The role to add")] IRole roleToAdd,
        [Summary("time", "How long the user keeps the role, for example 1d12h")]
        TimeSpan? time = null)
    {
        var runnerUser = (IGuildUser)ctx.User;
        var runnerMaxRolePosition = runnerUser.GetRoles().Max(x => x.Position);
        if (ctx.User.Id != ctx.Guild.OwnerId && runnerMaxRolePosition <= roleToAdd.Position)
        {
            await ReplyErrorAsync(Strings.Hierarchy(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        if (time is null)
        {
            try
            {
                await targetUser.AddRoleAsync(roleToAdd).ConfigureAwait(false);

                await ReplyConfirmAsync(Strings.Setrole(ctx.Guild.Id, Format.Bold(roleToAdd.Name),
                        Format.Bold(targetUser.ToString())))
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error in setrole command");
                await ReplyErrorAsync(Strings.SetroleErr(ctx.Guild.Id)).ConfigureAwait(false);
            }

            return;
        }

        try
        {
            await muteService.TimedRole(targetUser, time.Value, $"Timed role assignment by {ctx.User}", roleToAdd)
                .ConfigureAwait(false);

            await ReplyConfirmAsync(Strings.SetroleTime(ctx.Guild.Id, Format.Bold(roleToAdd.Name),
                    Format.Bold(targetUser.ToString()), time.Value.Humanize()))
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error in timed setrole command");
            await ReplyErrorAsync(Strings.SetroleErr(ctx.Guild.Id)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Removes a role from a user.
    /// </summary>
    /// <param name="targetUser">The user to remove the role from.</param>
    /// <param name="roleToRemove">The role to remove from the user.</param>
    [SlashCommand("remove", "Removes a role from a user")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageRoles)]
    [RequireBotPermission(GuildPermission.ManageRoles)]
    public async Task RemoveRole(
        [Summary("user", "The user to remove the role from")]
        IGuildUser targetUser,
        [Summary("role", "The role to remove")]
        IRole roleToRemove)
    {
        var runnerUser = (IGuildUser)ctx.User;
        if (ctx.User.Id != runnerUser.Guild.OwnerId &&
            runnerUser.GetRoles().Max(x => x.Position) <= roleToRemove.Position)
        {
            await ReplyErrorAsync(Strings.Hierarchy(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        try
        {
            await targetUser.RemoveRoleAsync(roleToRemove).ConfigureAwait(false);
            await ReplyConfirmAsync(Strings.Remrole(ctx.Guild.Id, Format.Bold(roleToRemove.Name),
                Format.Bold(targetUser.ToString()))).ConfigureAwait(false);
        }
        catch
        {
            await ReplyErrorAsync(Strings.RemroleErr(ctx.Guild.Id)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Removes all roles from a user except managed roles and the everyone role.
    /// </summary>
    /// <param name="user">The user from whom to remove all roles.</param>
    [SlashCommand("remove-all", "Removes every role from a user")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageRoles)]
    [RequireBotPermission(GuildPermission.ManageRoles)]
    public async Task RemoveAllRoles([Summary("user", "The user to strip of roles")] IGuildUser user)
    {
        var guser = (IGuildUser)ctx.User;

        var userRoles = user.GetRoles()
            .Where(x => !x.IsManaged && x != x.Guild.EveryoneRole)
            .ToList();

        if (userRoles.Count == 0)
        {
            await ReplyErrorAsync(Strings.RarErr(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        if (user.Id == ctx.Guild.OwnerId || ctx.User.Id != ctx.Guild.OwnerId &&
            guser.GetRoles().Max(x => x.Position) <=
            userRoles.Max(x => x.Position))
        {
            await ReplyErrorAsync(Strings.Hierarchy(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        try
        {
            await user.RemoveRolesAsync(userRoles).ConfigureAwait(false);
            await ReplyConfirmAsync(Strings.Rar(ctx.Guild.Id, Format.Bold(user.ToString()))).ConfigureAwait(false);
        }
        catch (Exception)
        {
            await ReplyErrorAsync(Strings.RarErr(ctx.Guild.Id)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Sets the level requirement for a self-assignable role.
    /// </summary>
    /// <param name="level">The level required to obtain the role.</param>
    /// <param name="role">The role to set the level requirement for.</param>
    [SlashCommand("level-req", "Sets the level required to self-assign a role")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageRoles)]
    [RequireBotPermission(GuildPermission.ManageRoles)]
    public async Task RoleLevelReq(
        [Summary("level", "The required level, 0 for none")]
        int level,
        [Summary("role", "The self-assignable role")]
        IRole role)
    {
        if (level < 0)
            return;

        var succ = await selfAssignedRoles.SetLevelReq(ctx.Guild.Id, role, level);

        if (!succ)
        {
            await ReplyErrorAsync(Strings.SelfAssignNot(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await ReplyConfirmAsync(Strings.SelfAssignLevelReq(ctx.Guild.Id,
            Format.Bold(role.Name),
            Format.Bold(level.ToString()))).ConfigureAwait(false);
    }

    /// <summary>
    ///     Reaction role commands.
    /// </summary>
    /// <param name="interactivity">Interactive service used for paginated embeds.</param>
    /// <param name="logger">The logger instance for structured logging.</param>
    [Group("reaction", "Roles given by reacting to a message")]
    public class RolesReaction(InteractiveService interactivity, ILogger<RolesReaction> logger)
        : MewdekoSlashSubmodule<RoleCommandsService>
    {
        /// <summary>
        ///     Assigns reaction roles to a message based on the provided role and emoji pairs.
        /// </summary>
        /// <param name="messageId">The ID of the message to which reactions will be added.</param>
        /// <param name="channel">The channel the message is in.</param>
        /// <param name="exclusive">Whether users may only hold one of the roles at a time.</param>
        /// <param name="input">Space separated pairs of role and emoji, for example @Red :red_circle: @Blue :blue_circle:.</param>
        [SlashCommand("add", "Adds reaction roles to a message")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageRoles)]
        [RequireBotPermission(GuildPermission.ManageRoles)]
        public async Task ReactionRoles(
            [Summary("message-id", "The id of the message to add reactions to")]
            ulong messageId,
            [Summary("channel", "The channel the message is in")]
            ITextChannel channel,
            [Summary("exclusive", "Whether users can only have one of these roles")]
            bool exclusive,
            [Summary("pairs", "Space separated role and emoji pairs, for example @Red :red_circle:")]
            string input)
        {
            await DeferAsync().ConfigureAwait(false);

            try
            {
                var target = await channel.GetMessageAsync(messageId).ConfigureAwait(false);
                if (target is null)
                {
                    await ReplyErrorAsync(Strings.ReactionRolesError(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
                }

                var tokens = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length % 2 != 0)
                {
                    await ReplyErrorAsync(Strings.ReactionRolesError(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
                }

                var runnerMax = ((IGuildUser)ctx.User).GetRoles().Select(r => r.Position).Max();
                var pairs = new List<(IRole role, IEmote emote)>();
                for (var i = 0; i < tokens.Length; i += 2)
                {
                    var role = ResolveRole(tokens[i]);
                    if (role is null)
                    {
                        logger.LogWarning("Role {Role} not found", tokens[i]);
                        continue;
                    }

                    if (role.Position > runnerMax && ctx.User.Id != ctx.Guild.OwnerId)
                        continue;

                    var emote = tokens[i + 1].ToIEmote();
                    if (emote is null)
                        continue;

                    pairs.Add((role, emote));
                }

                if (pairs.Count == 0)
                {
                    await ReplyErrorAsync(Strings.ReactionRolesError(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
                }

                foreach (var (_, emote) in pairs)
                {
                    try
                    {
                        await target.AddReactionAsync(emote, new RequestOptions
                        {
                            RetryMode = RetryMode.Retry502 | RetryMode.RetryRatelimit
                        }).ConfigureAwait(false);
                    }
                    catch (HttpException ex) when (ex.HttpCode == HttpStatusCode.BadRequest)
                    {
                        await ReplyErrorAsync(Strings.ReactionCantAccess(ctx.Guild.Id, Format.Code(emote.ToString())))
                            .ConfigureAwait(false);
                        return;
                    }

                    await Task.Delay(500).ConfigureAwait(false);
                }

                if (await Service.Add(ctx.Guild.Id, new ReactionRoleMessage
                    {
                        Exclusive = exclusive,
                        MessageId = target.Id,
                        ChannelId = target.Channel.Id,
                        ReactionRoles = pairs.Select(x => new ReactionRole
                        {
                            EmoteName = x.emote.ToString(), RoleId = x.role.Id
                        }).ToList()
                    }))
                {
                    await ReplyConfirmAsync(Strings.ReroRolesCount(ctx.Guild.Id, pairs.Count)).ConfigureAwait(false);
                }
                else
                {
                    await ReplyErrorAsync(Strings.ReactionRolesError(ctx.Guild.Id)).ConfigureAwait(false);
                }
            }
            catch (Exception e)
            {
                logger.LogError("There was an error adding a reaction role, see below exception: {Exception}", e);
                await ReplyErrorAsync(Strings.ReactionRolesError(ctx.Guild.Id)).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Displays a list of reaction roles configured in the server.
        /// </summary>
        [SlashCommand("list", "Lists the reaction roles on this server")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageRoles)]
        public async Task ReactionRolesList()
        {
            var (success, rrs) = await Service.Get(ctx.Guild.Id).ConfigureAwait(false);
            if (!success || rrs.Count == 0)
            {
                await ErrorAsync(Strings.NoReactionRoles(ctx.Guild.Id)).ConfigureAwait(false);
            }
            else
            {
                var paginator = new LazyPaginatorBuilder()
                    .AddUser(ctx.User)
                    .WithPageFactory(PageFactory)
                    .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                    .WithMaxPageIndex(rrs.Count - 1)
                    .WithDefaultEmotes()
                    .WithActionOnCancellation(ActionOnStop.DeleteMessage)
                    .Build();

                await interactivity.SendPaginatorAsync(paginator, (ctx.Interaction as SocketInteraction)!,
                    TimeSpan.FromMinutes(60)).ConfigureAwait(false);

                async Task<PageBuilder> PageFactory(int page)
                {
                    var rr = rrs.Skip(page).FirstOrDefault();
                    var g = ctx.Guild;
                    var ch = await g.GetTextChannelAsync(rr.ChannelId).ConfigureAwait(false);
                    IUserMessage? msg = null;
                    if (ch is not null)
                        msg = await ch.GetMessageAsync(rr.MessageId).ConfigureAwait(false) as IUserMessage;
                    var eb = new PageBuilder().WithOkColor();
                    return
                        eb.AddField("ID", rr.Index + 1)
                            .AddField(Strings.ReroRolesCount(ctx.Guild.Id, rr.ReactionRoles.Count()),
                                string.Join(",",
                                    rr.ReactionRoles.Select(x => $"{x.EmoteName} {g.GetRole(x.RoleId).Mention}")))
                            .AddField(Strings.UsersCanSelectMorethanOne(ctx.Guild.Id), rr.Exclusive)
                            .AddField(Strings.Wasdeleted(ctx.Guild.Id),
                                msg == null ? Strings.Yes(ctx.Guild.Id) : Strings.No(ctx.Guild.Id))
                            .AddField(Strings.Messagelink(ctx.Guild.Id),
                                (msg == null
                                    ? Strings.Messagewasdeleted(ctx.Guild.Id)
                                    : $"[{Strings.Hyatt(ctx.Guild.Id)}]({msg.GetJumpUrl()})")!);
                }
            }
        }

        /// <summary>
        ///     Removes a reaction role message based on its index in the list.
        /// </summary>
        /// <param name="index">The index of the reaction role message to remove, as shown in the list.</param>
        [SlashCommand("remove", "Removes a reaction role message by its list id")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageRoles)]
        public async Task ReactionRolesRemove([Summary("id", "The id shown in the reaction role list")] int index)
        {
            var (success, rrs) = await Service.Get(ctx.Guild.Id).ConfigureAwait(false);
            if (index < 1 ||
                !success ||
                rrs.Count == 0 || rrs.Count < index)
            {
                await ErrorAsync(Strings.NoReactionRoles(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            index--;
            await Service.Remove(ctx.Guild.Id, index);
            await ReplyConfirmAsync(Strings.ReactionRoleRemoved(ctx.Guild.Id, index + 1)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Resolves a role from a mention, an id, or a name.
        /// </summary>
        /// <param name="token">The mention, id, or name.</param>
        /// <returns>The role, or null when no role matches.</returns>
        private IRole? ResolveRole(string token)
        {
            if (MentionUtils.TryParseRole(token, out var mentionId))
                return ctx.Guild.GetRole(mentionId);

            if (ulong.TryParse(token, out var rawId))
                return ctx.Guild.GetRole(rawId);

            return ctx.Guild.Roles.FirstOrDefault(r =>
                       string.Equals(r.Name, token, StringComparison.OrdinalIgnoreCase)) ??
                   ctx.Guild.Roles.FirstOrDefault(r =>
                       r.Name.StartsWith(token, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    ///     Self-assignable role commands.
    /// </summary>
    /// <param name="interactivity">Interactive service used for paginated embeds.</param>
    [Group("self", "Roles members can give themselves")]
    public class RolesSelf(InteractiveService interactivity) : MewdekoSlashSubmodule<SelfAssignedRolesService>
    {
        /// <summary>
        ///     Adds the specified role to the self-assignable role list, optionally within a group.
        /// </summary>
        /// <param name="role">The role to add to the self-assignable role list.</param>
        /// <param name="group">The group number for organization.</param>
        [SlashCommand("add", "Makes a role self-assignable")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageRoles)]
        [RequireBotPermission(GuildPermission.ManageRoles)]
        public async Task Asar(
            [Summary("role", "The role to make self-assignable")]
            IRole role,
            [Summary("group", "The group number, defaults to 0")]
            int group = 0)
        {
            var guser = (IGuildUser)ctx.User;
            if (ctx.User.Id != guser.Guild.OwnerId && guser.GetRoles().Max(x => x.Position) <= role.Position)
            {
                await ReplyErrorAsync(Strings.Hierarchy(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var succ = await Service.AddNew(ctx.Guild.Id, role, group);

            if (succ)
            {
                await ReplyConfirmAsync(Strings.RoleAdded(ctx.Guild.Id, Format.Bold(role.Name),
                    Format.Bold(group.ToString()))).ConfigureAwait(false);
            }
            else
            {
                await ReplyErrorAsync(Strings.RoleInList(ctx.Guild.Id, Format.Bold(role.Name))).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Removes the specified role from the self-assignable roles list.
        /// </summary>
        /// <param name="role">The role to remove from the self-assignable roles list.</param>
        [SlashCommand("remove", "Makes a role no longer self-assignable")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageRoles)]
        public async Task Rsar([Summary("role", "The role to remove")] IRole role)
        {
            var guser = (IGuildUser)ctx.User;
            if (ctx.User.Id != guser.Guild.OwnerId && guser.GetRoles().Max(x => x.Position) <= role.Position)
            {
                await ReplyErrorAsync(Strings.Hierarchy(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var success = await Service.RemoveSar(role.Guild.Id, role.Id);
            if (!success)
                await ReplyErrorAsync(Strings.SelfAssignNot(ctx.Guild.Id)).ConfigureAwait(false);
            else
                await ReplyConfirmAsync(Strings.SelfAssignRem(ctx.Guild.Id, Format.Bold(role.Name)))
                    .ConfigureAwait(false);
        }

        /// <summary>
        ///     Lists the self-assignable roles configured for the guild.
        /// </summary>
        [SlashCommand("list", "Lists the self-assignable roles")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task Lsar()
        {
            var (exclusive, roles, groups) = await Service.GetRoles(ctx.Guild);
            var paginator = new LazyPaginatorBuilder()
                .AddUser(ctx.User)
                .WithPageFactory(PageFactory)
                .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                .WithMaxPageIndex(roles.Count() / 20)
                .WithDefaultEmotes()
                .WithActionOnCancellation(ActionOnStop.DeleteMessage)
                .Build();

            await interactivity.SendPaginatorAsync(paginator, (ctx.Interaction as SocketInteraction)!,
                TimeSpan.FromMinutes(60)).ConfigureAwait(false);

            async Task<PageBuilder> PageFactory(int page)
            {
                await Task.CompletedTask.ConfigureAwait(false);
                var rolesStr = new StringBuilder();
                var roleGroups = roles
                    .OrderBy(x => x.Model.Group)
                    .Skip(page * 20)
                    .Take(20)
                    .GroupBy(x => x.Model.Group)
                    .OrderBy(x => x.Key);

                foreach (var kvp in roleGroups)
                {
                    var groupNameText = Format.Bold(!groups.TryGetValue(kvp.Key, out var name)
                        ? Strings.SelfAssignGroup(ctx.Guild.Id, kvp.Key)
                        : $"{kvp.Key} - {name.TrimTo(25, true)}");

                    rolesStr.AppendLine($"\t\t\t\t ⟪{groupNameText}⟫");
                    foreach (var (model, role) in kvp.AsEnumerable())
                    {
                        if (role.Name is null)
                            continue;

                        if (model.LevelRequirement == 0)
                            rolesStr.AppendLine($"‌‌   {role.Name}");
                        else
                            rolesStr.AppendLine($"‌‌   {role.Name} (lvl {model.LevelRequirement}+)");
                    }

                    rolesStr.AppendLine();
                }

                return new PageBuilder().WithColor(Mewdeko.OkColor)
                    .WithTitle(Format.Bold(Strings.SelfAssignList(ctx.Guild.Id, roles.Count())))
                    .WithDescription(rolesStr.ToString())
                    .WithFooter(exclusive
                        ? Strings.SelfAssignAreExclusive(ctx.Guild.Id)
                        : Strings.SelfAssignAreNotExclusive(ctx.Guild.Id));
            }
        }

        /// <summary>
        ///     Sets or removes the name for the specified group of self-assignable roles.
        /// </summary>
        /// <param name="group">The group number for which to set or remove the name.</param>
        /// <param name="name">The name to set for the group. If not provided, the name for the group will be removed.</param>
        [SlashCommand("group-name", "Sets or clears the name of a self-assignable role group")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageRoles)]
        [RequireBotPermission(GuildPermission.ManageRoles)]
        public async Task Sargn(
            [Summary("group", "The group number")] int group,
            [Summary("name", "The new name, omit to clear it")]
            string? name = null)
        {
            var set = await Service.SetNameAsync(ctx.Guild.Id, group, name).ConfigureAwait(false);

            if (set)
            {
                await ReplyConfirmAsync(Strings.GroupNameAdded(ctx.Guild.Id, Format.Bold(group.ToString()),
                    Format.Bold(name))).ConfigureAwait(false);
            }
            else
            {
                await ReplyConfirmAsync(Strings.GroupNameRemoved(ctx.Guild.Id, Format.Bold(group.ToString())))
                    .ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Toggles the exclusivity of self-assignable roles.
        /// </summary>
        [SlashCommand("toggle-exclusive", "Toggles whether members may only hold one self-assignable role")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageRoles)]
        [RequireBotPermission(GuildPermission.ManageRoles)]
        public async Task Togglexclsar()
        {
            var areExclusive = await Service.ToggleEsar(ctx.Guild.Id);
            if (areExclusive)
                await ReplyConfirmAsync(Strings.SelfAssignExcl(ctx.Guild.Id)).ConfigureAwait(false);
            else
                await ReplyConfirmAsync(Strings.SelfAssignNoExcl(ctx.Guild.Id)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Toggles the automatic deletion of iam and iamnot responses.
        /// </summary>
        [SlashCommand("auto-delete", "Toggles auto deletion of iam and iamnot responses")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageMessages)]
        [RequireBotPermission(GuildPermission.ManageMessages)]
        public async Task AdSarm()
        {
            var newVal = await Service.ToggleAdSarm(ctx.Guild.Id);

            if (newVal)
                await ReplyConfirmAsync(Strings.AdsarmEnable(ctx.Guild.Id, "/roles self ")).ConfigureAwait(false);
            else
                await ReplyConfirmAsync(Strings.AdsarmDisable(ctx.Guild.Id, "/roles self ")).ConfigureAwait(false);
        }

        /// <summary>
        ///     Grants the caller a self-assignable role.
        /// </summary>
        /// <param name="role">The role to be assigned to the user.</param>
        [SlashCommand("iam", "Gives you a self-assignable role")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task Iam([Summary("role", "The role you want")] IRole role)
        {
            var guildUser = (IGuildUser)ctx.User;

            var (result, autoDelete, extra) = await Service.Assign(guildUser, role).ConfigureAwait(false);

            var text = result switch
            {
                SelfAssignedRolesService.AssignResult.ErrNotAssignable => Strings.SelfAssignNot(ctx.Guild.Id),
                SelfAssignedRolesService.AssignResult.ErrLvlReq =>
                    Strings.SelfAssignNotLevel(ctx.Guild.Id, Format.Bold(extra.ToString())),
                SelfAssignedRolesService.AssignResult.ErrAlreadyHave =>
                    Strings.SelfAssignAlready(ctx.Guild.Id, Format.Bold(role.Name)),
                SelfAssignedRolesService.AssignResult.ErrNotPerms => Strings.SelfAssignPerms(ctx.Guild.Id),
                _ => null
            };

            if (text is not null)
            {
                if (autoDelete)
                    await EphemeralReplyErrorAsync(text).ConfigureAwait(false);
                else
                    await ReplyErrorAsync(text).ConfigureAwait(false);
                return;
            }

            var success = Strings.SelfAssignSuccess(ctx.Guild.Id, Format.Bold(role.Name));
            if (autoDelete)
                await EphemeralReplyConfirmAsync(success).ConfigureAwait(false);
            else
                await ReplyConfirmAsync(success).ConfigureAwait(false);
        }

        /// <summary>
        ///     Removes a self-assigned role from the caller.
        /// </summary>
        /// <param name="role">The role to be removed from the user.</param>
        [SlashCommand("iamnot", "Removes a self-assignable role from you")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task Iamnot([Summary("role", "The role you no longer want")] IRole role)
        {
            var guildUser = (IGuildUser)ctx.User;

            var (result, autoDelete) = await Service.Remove(guildUser, role).ConfigureAwait(false);

            var text = result switch
            {
                SelfAssignedRolesService.RemoveResult.ErrNotAssignable => Strings.SelfAssignNot(ctx.Guild.Id),
                SelfAssignedRolesService.RemoveResult.ErrNotHave =>
                    Strings.SelfAssignNotHave(ctx.Guild.Id, Format.Bold(role.Name)),
                SelfAssignedRolesService.RemoveResult.ErrNotPerms => Strings.SelfAssignPerms(ctx.Guild.Id),
                _ => null
            };

            if (text is not null)
            {
                if (autoDelete)
                    await EphemeralReplyErrorAsync(text).ConfigureAwait(false);
                else
                    await ReplyErrorAsync(text).ConfigureAwait(false);
                return;
            }

            var success = Strings.SelfAssignRemove(ctx.Guild.Id, Format.Bold(role.Name));
            if (autoDelete)
                await EphemeralReplyConfirmAsync(success).ConfigureAwait(false);
            else
                await ReplyConfirmAsync(success).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Voice channel role commands.
    /// </summary>
    [Group("vc", "Roles given while in a voice channel")]
    public class RolesVc : MewdekoSlashSubmodule<VcRoleService>
    {
        /// <summary>
        ///     Binds a role to a voice channel, or unbinds the channel's role when no role is given. The channel defaults to
        ///     the one the caller is in.
        /// </summary>
        /// <param name="role">The role to bind to the voice channel. Omit to unbind.</param>
        /// <param name="channel">The voice channel. Defaults to the caller's current voice channel.</param>
        [SlashCommand("set", "Binds a role to a voice channel, or unbinds it when no role is given")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageRoles)]
        [RequireBotPermission(GuildPermission.ManageRoles)]
        public async Task VcRole(
            [Summary("role", "The role to give, omit to remove the binding")]
            IRole? role = null,
            [Summary("channel", "The voice channel, defaults to the one you are in")]
            IVoiceChannel? channel = null)
        {
            if (channel is null)
            {
                var user = (IGuildUser)ctx.User;
                var vc = user.VoiceChannel;

                if (vc == null || vc.GuildId != user.GuildId)
                {
                    await ReplyErrorAsync(Strings.MustBeInVoice(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
                }

                channel = vc;
            }

            if (role == null)
            {
                if (await Service.RemoveVcRole(ctx.Guild.Id, channel.Id))
                {
                    await ReplyConfirmAsync(Strings.VcroleRemoved(ctx.Guild.Id, Format.Bold(channel.Name)))
                        .ConfigureAwait(false);
                }
                else
                {
                    await ReplyErrorAsync(Strings.VcroleNotFound(ctx.Guild.Id)).ConfigureAwait(false);
                }
            }
            else
            {
                await Service.AddVcRole(ctx.Guild.Id, role, channel.Id);
                await ReplyConfirmAsync(Strings.VcroleAdded(ctx.Guild.Id, Format.Bold(channel.Name),
                        Format.Bold(role.Name)))
                    .ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Unbinds a role from a voice channel by the channel id, which also works for deleted channels.
        /// </summary>
        /// <param name="vcId">The voice channel id.</param>
        [SlashCommand("remove", "Unbinds the role from a voice channel by its id")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageRoles)]
        [RequireBotPermission(GuildPermission.ManageRoles)]
        public async Task VcRoleRm([Summary("channel-id", "The voice channel id")] ulong vcId)
        {
            if (await Service.RemoveVcRole(ctx.Guild.Id, vcId))
            {
                await ReplyConfirmAsync(Strings.VcroleRemoved(ctx.Guild.Id, Format.Bold(vcId.ToString())))
                    .ConfigureAwait(false);
            }
            else
            {
                await ReplyErrorAsync(Strings.VcroleNotFound(ctx.Guild.Id)).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Lists all voice channel roles for this guild.
        /// </summary>
        [SlashCommand("list", "Lists the voice channel roles")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task VcRoleList()
        {
            var guild = (SocketGuild)ctx.Guild;
            string? text;
            if (Service.VcRoles.TryGetValue(ctx.Guild.Id, out var roles))
            {
                if (roles.Count == 0)
                {
                    text = Strings.NoVcroles(ctx.Guild.Id);
                }
                else
                {
                    text = string.Join("\n", roles.Select(x =>
                        $"{Format.Bold(guild.GetVoiceChannel(x.Key)?.Name ?? x.Key.ToString())} => {x.Value}"));
                }
            }
            else
            {
                text = Strings.NoVcroles(ctx.Guild.Id);
            }

            await ctx.Interaction.RespondAsync(embed: new EmbedBuilder().WithOkColor()
                    .WithTitle(Strings.VcRoleList(ctx.Guild.Id))
                    .WithDescription(text)
                    .Build())
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Auto-assign role commands.
    /// </summary>
    [Group("auto", "Roles given automatically when someone joins")]
    public class RolesAuto : MewdekoSlashSubmodule<AutoAssignRoleService>
    {
        /// <summary>
        ///     Toggles auto-assigning the specified role to users when they join the guild, or lists the auto-assigned roles
        ///     when no role is given.
        /// </summary>
        /// <param name="role">The role to toggle. Omit to list the current roles.</param>
        [SlashCommand("role", "Toggles a role given to new members, or lists them when omitted")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageRoles)]
        public async Task AutoAssignRole([Summary("role", "The role to toggle, omit to list")] IRole? role = null)
        {
            if (role is null)
            {
                await ListRoles().ConfigureAwait(false);
                return;
            }

            var guser = (IGuildUser)ctx.User;
            if (role.Id == ctx.Guild.EveryoneRole.Id)
                return;

            if (ctx.User.Id != guser.Guild.OwnerId && guser.GetRoles().Max(x => x.Position) <= role.Position)
            {
                await ReplyErrorAsync(Strings.Hierarchy(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var roles = await Service.ToggleAarAsync(ctx.Guild.Id, role.Id).ConfigureAwait(false);
            if (roles.Count == 0)
                await ReplyConfirmAsync(Strings.AarDisabled(ctx.Guild.Id)).ConfigureAwait(false);
            else if (roles.Contains(role.Id))
                await ListRoles().ConfigureAwait(false);
            else
                await ReplyConfirmAsync(Strings.AarRoleRemoved(ctx.Guild.Id, Format.Bold(role.Mention)))
                    .ConfigureAwait(false);
        }

        /// <summary>
        ///     Toggles auto-assigning the specified role to bots when they join the guild, or lists the auto-assigned bot
        ///     roles when no role is given.
        /// </summary>
        /// <param name="role">The role to toggle. Omit to list the current roles.</param>
        [SlashCommand("bot-role", "Toggles a role given to new bots, or lists them when omitted")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageRoles)]
        public async Task AutoAssignBotRole([Summary("role", "The role to toggle, omit to list")] IRole? role = null)
        {
            if (role is null)
            {
                await ListBotRoles().ConfigureAwait(false);
                return;
            }

            var guser = (IGuildUser)ctx.User;
            if (role.Id == ctx.Guild.EveryoneRole.Id)
                return;

            if (ctx.User.Id != guser.Guild.OwnerId && guser.GetRoles().Max(x => x.Position) <= role.Position)
            {
                await ReplyErrorAsync(Strings.Hierarchy(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var roles = await Service.ToggleAabrAsync(ctx.Guild.Id, role.Id).ConfigureAwait(false);
            if (roles.Count == 0)
                await ReplyConfirmAsync(Strings.AabrDisabled(ctx.Guild.Id)).ConfigureAwait(false);
            else if (roles.Contains(role.Id))
                await ListBotRoles().ConfigureAwait(false);
            else
                await ReplyConfirmAsync(Strings.AabrRoleRemoved(ctx.Guild.Id, Format.Bold(role.Mention)))
                    .ConfigureAwait(false);
        }

        private async Task ListRoles()
        {
            var roles = await Service.TryGetNormalRoles(ctx.Guild.Id);
            if (!roles.Any())
            {
                await ReplyConfirmAsync(Strings.AarNone(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var existing = roles.Select(rid => ctx.Guild.GetRole(rid)).Where(r => r is not null)
                .ToList();

            if (existing.Count != roles.Count())
                await Service.SetAarRolesAsync(ctx.Guild.Id, existing.Select(x => x.Id)).ConfigureAwait(false);

            await ReplyConfirmAsync(Strings.AarRoles(ctx.Guild.Id,
                $"\n{existing.Select(x => Format.Bold(x.Mention)).JoinWith("\n")}")).ConfigureAwait(false);
        }

        private async Task ListBotRoles()
        {
            var roles = await Service.TryGetBotRoles(ctx.Guild.Id);
            if (!roles.Any())
            {
                await ReplyConfirmAsync(Strings.AabrNone(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var existing = roles.Select(rid => ctx.Guild.GetRole(rid)).Where(r => r is not null)
                .ToList();

            if (existing.Count != roles.Count())
                await Service.SetAabrRolesAsync(ctx.Guild.Id, existing.Select(x => x.Id)).ConfigureAwait(false);

            await ReplyConfirmAsync(Strings.AabrRoles(ctx.Guild.Id,
                $"\n{existing.Select(x => Format.Bold(x.Mention)).JoinWith("\n")}")).ConfigureAwait(false);
        }
    }
}