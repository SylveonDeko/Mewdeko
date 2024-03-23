using System.Text;
using Discord.Commands;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Administration.Services;

namespace Mewdeko.Modules.Administration;

public partial class Administration
{
    /// <summary>
    /// Module for managing self-assigned roles.
    /// </summary>
    /// <param name="serv">Embed pagination service.</param>
    /// <param name="guildSettings">The guild settings service.</param>
    [Group]
    public class SelfAssignedRolesCommands(InteractiveService serv, GuildSettingsService guildSettings)
        : MewdekoSubmodule<SelfAssignedRolesService>
    {
        /// <summary>
        /// Toggles the auto delete of server advertisement messages.
        /// </summary>
        /// <remarks>
        /// This command allows users to toggle the automatic deletion of server advertisement messages.
        /// It requires the Manage Messages permission for the user.
        /// </remarks>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageMessages), BotPerm(GuildPermission.ManageMessages)]
        public async Task AdSarm()
        {
            var newVal = await Service.ToggleAdSarm(ctx.Guild.Id);

            if (newVal)
                await ReplyConfirmLocalizedAsync("adsarm_enable", await guildSettings.GetPrefix(ctx.Guild))
                    .ConfigureAwait(false);
            else
                await ReplyConfirmLocalizedAsync("adsarm_disable", await guildSettings.GetPrefix(ctx.Guild))
                    .ConfigureAwait(false);
        }

        /// <summary>
        /// Adds the specified role to the auto-self-assignable role list.
        /// </summary>
        /// <remarks>
        /// This command allows administrators to add a role to the auto-self-assignable role list,
        /// optionally specifying a group number for organization.
        /// It requires the Manage Roles permission for the user.
        /// </remarks>
        /// <param name="role">The role to add to the auto-self-assignable role list.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageRoles), BotPerm(GuildPermission.ManageRoles), Priority(1)]
        public Task Asar([Remainder] IRole role) => Asar(0, role);

        /// <summary>
        /// Adds the specified role to the auto-self-assignable role list within the specified group.
        /// </summary>
        /// <remarks>
        /// This command allows administrators to add a role to the auto-self-assignable role list within a specific group.
        /// It requires the Manage Roles permission for the user.
        /// </remarks>
        /// <param name="group">The group number for organization.</param>
        /// <param name="role">The role to add to the auto-self-assignable role list.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageRoles), BotPerm(GuildPermission.ManageRoles), Priority(0)]
        public async Task Asar(int group, [Remainder] IRole role)
        {
            var guser = (IGuildUser)ctx.User;
            if (ctx.User.Id != guser.Guild.OwnerId && guser.GetRoles().Max(x => x.Position) <= role.Position)
                return;

            var succ = await Service.AddNew(ctx.Guild.Id, role, group);

            if (succ)
            {
                await ReplyConfirmLocalizedAsync("role_added", Format.Bold(role.Name),
                    Format.Bold(group.ToString())).ConfigureAwait(false);
            }
            else
            {
                await ReplyErrorLocalizedAsync("role_in_list", Format.Bold(role.Name)).ConfigureAwait(false);
            }
        }


        /// <summary>
        /// Sets or removes the name for the specified group of auto-self-assignable roles.
        /// </summary>
        /// <remarks>
        /// This command allows administrators to set or remove the name for a group of auto-self-assignable roles.
        /// It requires the Manage Roles permission for the user.
        /// </remarks>
        /// <param name="group">The group number for which to set or remove the name.</param>
        /// <param name="name">The name to set for the group. If not provided, the name for the group will be removed.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageRoles), BotPerm(GuildPermission.ManageRoles), Priority(0)]
        public async Task Sargn(int group, [Remainder] string? name = null)
        {
            var set = await Service.SetNameAsync(ctx.Guild.Id, group, name).ConfigureAwait(false);

            if (set)
            {
                await ReplyConfirmLocalizedAsync("group_name_added", Format.Bold(group.ToString()),
                    Format.Bold(name)).ConfigureAwait(false);
            }
            else
            {
                await ReplyConfirmLocalizedAsync("group_name_removed", Format.Bold(group.ToString()))
                    .ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Removes the specified role from the auto-self-assignable roles list.
        /// </summary>
        /// <remarks>
        /// This command allows administrators to remove a role from the auto-self-assignable roles list.
        /// It requires the Manage Roles permission for the user.
        /// </remarks>
        /// <param name="role">The role to remove from the auto-self-assignable roles list.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageRoles)]
        public async Task Rsar([Remainder] IRole role)
        {
            var guser = (IGuildUser)ctx.User;
            if (ctx.User.Id != guser.Guild.OwnerId && guser.GetRoles().Max(x => x.Position) <= role.Position)
                return;

            var success = await Service.RemoveSar(role.Guild.Id, role.Id);
            if (!success)
                await ReplyErrorLocalizedAsync("self_assign_not").ConfigureAwait(false);
            else
                await ReplyConfirmLocalizedAsync("self_assign_rem", Format.Bold(role.Name)).ConfigureAwait(false);
        }

        /// <summary>
        /// Lists the auto-self-assignable roles configured for the guild.
        /// </summary>
        /// <remarks>
        /// This command allows users to list the auto-self-assignable roles configured for the guild.
        /// </remarks>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd, Aliases, RequireContext(ContextType.Guild)]
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

            await serv.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60)).ConfigureAwait(false);

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
                        ? GetText("self_assign_group", kvp.Key)
                        : $"{kvp.Key} - {name.TrimTo(25, true)}");

                    rolesStr.AppendLine($"\t\t\t\t ⟪{groupNameText}⟫");
                    foreach (var (model, role) in kvp.AsEnumerable())
                    {
                        if (role.Name is null)
                        {
                        }
                        else
                        {
                            // first character is invisible space
                            if (model.LevelRequirement == 0)
                                rolesStr.AppendLine($"‌‌   {role.Name}");
                            else
                                rolesStr.AppendLine($"‌‌   {role.Name} (lvl {model.LevelRequirement}+)");
                        }
                    }

                    rolesStr.AppendLine();
                }

                return new PageBuilder().WithColor(Mewdeko.OkColor)
                    .WithTitle(Format.Bold(GetText("self_assign_list", roles.Count())))
                    .WithDescription(rolesStr.ToString())
                    .WithFooter(exclusive
                        ? GetText("self_assign_are_exclusive")
                        : GetText("self_assign_are_not_exclusive"));
            }
        }

        /// <summary>
        /// Toggles the exclusivity of auto-self-assignable roles.
        /// </summary>
        /// <remarks>
        /// This command allows users to toggle the exclusivity of auto-self-assignable roles.
        /// </remarks>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageRoles), BotPerm(GuildPermission.ManageRoles)]
        public async Task Togglexclsar()
        {
            var areExclusive = await Service.ToggleEsar(ctx.Guild.Id);
            if (areExclusive)
                await ReplyConfirmLocalizedAsync("self_assign_excl").ConfigureAwait(false);
            else
                await ReplyConfirmLocalizedAsync("self_assign_no_excl").ConfigureAwait(false);
        }

        /// <summary>
        /// Sets the level requirement for an auto-self-assignable role.
        /// </summary>
        /// <param name="level">The level required to obtain the role.</param>
        /// <param name="role">The role to set the level requirement for.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageRoles), BotPerm(GuildPermission.ManageRoles)]
        public async Task RoleLevelReq(int level, [Remainder] IRole role)
        {
            if (level < 0)
                return;

            var succ = await Service.SetLevelReq(ctx.Guild.Id, role, level);

            if (!succ)
            {
                await ReplyErrorLocalizedAsync("self_assign_not").ConfigureAwait(false);
                return;
            }

            await ReplyConfirmLocalizedAsync("self_assign_level_req",
                Format.Bold(role.Name),
                Format.Bold(level.ToString())).ConfigureAwait(false);
        }

        /// <summary>
        /// Grants a user a self-assignable role.
        /// </summary>
        /// <remarks>
        /// This command allows users to assign themselves a self-assignable role.
        /// </remarks>
        /// <param name="role">The role to be assigned to the user.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd, Aliases, RequireContext(ContextType.Guild)]
        public async Task Iam([Remainder] IRole role)
        {
            var guildUser = (IGuildUser)ctx.User;

            var (result, autoDelete, extra) = await Service.Assign(guildUser, role).ConfigureAwait(false);

            var msg = result switch
            {
                SelfAssignedRolesService.AssignResult.ErrNotAssignable =>
                    await ReplyErrorLocalizedAsync("self_assign_not").ConfigureAwait(false),
                SelfAssignedRolesService.AssignResult.ErrLvlReq => await ReplyErrorLocalizedAsync(
                        "self_assign_not_level", Format.Bold(extra.ToString()))
                    .ConfigureAwait(false),
                SelfAssignedRolesService.AssignResult.ErrAlreadyHave => await ReplyErrorLocalizedAsync(
                        "self_assign_already", Format.Bold(role.Name))
                    .ConfigureAwait(false),
                SelfAssignedRolesService.AssignResult.ErrNotPerms => await ReplyErrorLocalizedAsync("self_assign_perms")
                    .ConfigureAwait(false),
                _ => await ReplyConfirmLocalizedAsync("self_assign_success", Format.Bold(role.Name))
                    .ConfigureAwait(false)
            };

            if (autoDelete)
            {
                msg.DeleteAfter(3);
                ctx.Message.DeleteAfter(3);
            }
        }


        /// <summary>
        /// Removes a self-assigned role from the user.
        /// </summary>
        /// <remarks>
        /// This command allows users to remove a self-assigned role from themselves.
        /// </remarks>
        /// <param name="role">The role to be removed from the user.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd, Aliases, RequireContext(ContextType.Guild)]
        public async Task Iamnot([Remainder] IRole role)
        {
            var guildUser = (IGuildUser)ctx.User;

            var (result, autoDelete) = await Service.Remove(guildUser, role).ConfigureAwait(false);

            IUserMessage msg;
            if (result == SelfAssignedRolesService.RemoveResult.ErrNotAssignable)
            {
                msg = await ReplyErrorLocalizedAsync("self_assign_not").ConfigureAwait(false);
            }
            else if (result == SelfAssignedRolesService.RemoveResult.ErrNotHave)
            {
                msg = await ReplyErrorLocalizedAsync("self_assign_not_have", Format.Bold(role.Name))
                    .ConfigureAwait(false);
            }
            else if (result == SelfAssignedRolesService.RemoveResult.ErrNotPerms)
            {
                msg = await ReplyErrorLocalizedAsync("self_assign_perms").ConfigureAwait(false);
            }
            else
            {
                msg = await ReplyConfirmLocalizedAsync("self_assign_remove", Format.Bold(role.Name))
                    .ConfigureAwait(false);
            }

            if (autoDelete)
            {
                msg.DeleteAfter(3);
                ctx.Message.DeleteAfter(3);
            }
        }
    }
}