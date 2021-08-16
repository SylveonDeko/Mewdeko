using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Mewdeko.Common.Attributes;
using Mewdeko.Core.Modules.Administration.Services;
using Mewdeko.Extensions;

namespace Mewdeko.Modules.Administration
{
    public partial class Administration
    {
        [Group]
        public class SelfAssignedRolesCommands : MewdekoSubmodule<SelfAssignedRolesService>
        {
            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageMessages)]
            [BotPerm(GuildPerm.ManageMessages)]
            public async Task AdSarm()
            {
                var newVal = _service.ToggleAdSarm(ctx.Guild.Id);

                if (newVal)
                    await ReplyConfirmLocalizedAsync("adsarm_enable", Prefix).ConfigureAwait(false);
                else
                    await ReplyConfirmLocalizedAsync("adsarm_disable", Prefix).ConfigureAwait(false);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageRoles)]
            [BotPerm(GuildPerm.ManageRoles)]
            [Priority(1)]
            public Task Asar([Leftover] IRole role)
            {
                return Asar(0, role);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageRoles)]
            [BotPerm(GuildPerm.ManageRoles)]
            [Priority(0)]
            public async Task Asar(int group, [Leftover] IRole role)
            {
                var guser = (IGuildUser) ctx.User;
                if (ctx.User.Id != guser.Guild.OwnerId && guser.GetRoles().Max(x => x.Position) <= role.Position)
                    return;

                var succ = _service.AddNew(ctx.Guild.Id, role, group);

                if (succ)
                    await ReplyConfirmLocalizedAsync("role_added", Format.Bold(role.Name),
                        Format.Bold(group.ToString())).ConfigureAwait(false);
                else
                    await ReplyErrorLocalizedAsync("role_in_list", Format.Bold(role.Name)).ConfigureAwait(false);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageRoles)]
            [BotPerm(GuildPerm.ManageRoles)]
            [Priority(0)]
            public async Task Sargn(int group, [Leftover] string name = null)
            {
                var guser = (IGuildUser) ctx.User;

                var set = await _service.SetNameAsync(ctx.Guild.Id, group, name).ConfigureAwait(false);

                if (set)
                    await ReplyConfirmLocalizedAsync("group_name_added", Format.Bold(group.ToString()),
                        Format.Bold(name)).ConfigureAwait(false);
                else
                    await ReplyConfirmLocalizedAsync("group_name_removed", Format.Bold(group.ToString()))
                        .ConfigureAwait(false);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageRoles)]
            public async Task Rsar([Leftover] IRole role)
            {
                var guser = (IGuildUser) ctx.User;
                if (ctx.User.Id != guser.Guild.OwnerId && guser.GetRoles().Max(x => x.Position) <= role.Position)
                    return;

                var success = _service.RemoveSar(role.Guild.Id, role.Id);
                if (!success)
                    await ReplyErrorLocalizedAsync("self_assign_not").ConfigureAwait(false);
                else
                    await ReplyConfirmLocalizedAsync("self_assign_rem", Format.Bold(role.Name)).ConfigureAwait(false);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task Lsar(int page = 1)
            {
                if (--page < 0)
                    return;

                var (exclusive, roles, groups) = _service.GetRoles(ctx.Guild);

                await ctx.SendPaginatedConfirmAsync(page, cur =>
                {
                    var rolesStr = new StringBuilder();
                    var roleGroups = roles
                        .OrderBy(x => x.Model.Group)
                        .Skip(cur * 20)
                        .Take(20)
                        .GroupBy(x => x.Model.Group)
                        .OrderBy(x => x.Key);

                    foreach (var kvp in roleGroups)
                    {
                        var groupNameText = "";
                        if (!groups.TryGetValue(kvp.Key, out var name))
                            groupNameText = Format.Bold(GetText("self_assign_group", kvp.Key));
                        else
                            groupNameText = Format.Bold($"{kvp.Key} - {name.TrimTo(25, true)}");

                        rolesStr.AppendLine("\t\t\t\t ⟪" + groupNameText + "⟫");
                        foreach (var (Model, Role) in kvp.AsEnumerable())
                            if (Role == null)
                            {
                            }
                            else
                            {
                                // first character is invisible space
                                if (Model.LevelRequirement == 0)
                                    rolesStr.AppendLine("‌‌   " + Role.Name);
                                else
                                    rolesStr.AppendLine("‌‌   " + Role.Name + $" (lvl {Model.LevelRequirement}+)");
                            }

                        rolesStr.AppendLine();
                    }

                    return new EmbedBuilder().WithOkColor()
                        .WithTitle(Format.Bold(GetText("self_assign_list", roles.Count())))
                        .WithDescription(rolesStr.ToString())
                        .WithFooter(exclusive
                            ? GetText("self_assign_are_exclusive")
                            : GetText("self_assign_are_not_exclusive"));
                }, roles.Count(), 20).ConfigureAwait(false);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageRoles)]
            [BotPerm(GuildPerm.ManageRoles)]
            public async Task Togglexclsar()
            {
                var areExclusive = _service.ToggleEsar(ctx.Guild.Id);
                if (areExclusive)
                    await ReplyConfirmLocalizedAsync("self_assign_excl").ConfigureAwait(false);
                else
                    await ReplyConfirmLocalizedAsync("self_assign_no_excl").ConfigureAwait(false);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageRoles)]
            [BotPerm(GuildPerm.ManageRoles)]
            public async Task RoleLevelReq(int level, [Leftover] IRole role)
            {
                if (level < 0)
                    return;

                var succ = _service.SetLevelReq(ctx.Guild.Id, role, level);

                if (!succ)
                {
                    await ReplyErrorLocalizedAsync("self_assign_not").ConfigureAwait(false);
                    return;
                }

                await ReplyConfirmLocalizedAsync("self_assign_level_req",
                    Format.Bold(role.Name),
                    Format.Bold(level.ToString())).ConfigureAwait(false);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task Iam([Leftover] IRole role)
            {
                var guildUser = (IGuildUser) ctx.User;

                var (result, autoDelete, extra) = await _service.Assign(guildUser, role).ConfigureAwait(false);

                IUserMessage msg;
                if (result == SelfAssignedRolesService.AssignResult.Err_Not_Assignable)
                    msg = await ReplyErrorLocalizedAsync("self_assign_not").ConfigureAwait(false);
                else if (result == SelfAssignedRolesService.AssignResult.Err_Lvl_Req)
                    msg = await ReplyErrorLocalizedAsync("self_assign_not_level", Format.Bold(extra.ToString()))
                        .ConfigureAwait(false);
                else if (result == SelfAssignedRolesService.AssignResult.Err_Already_Have)
                    msg = await ReplyErrorLocalizedAsync("self_assign_already", Format.Bold(role.Name))
                        .ConfigureAwait(false);
                else if (result == SelfAssignedRolesService.AssignResult.Err_Not_Perms)
                    msg = await ReplyErrorLocalizedAsync("self_assign_perms").ConfigureAwait(false);
                else
                    msg = await ReplyConfirmLocalizedAsync("self_assign_success", Format.Bold(role.Name))
                        .ConfigureAwait(false);

                if (autoDelete)
                {
                    msg.DeleteAfter(3);
                    ctx.Message.DeleteAfter(3);
                }
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task Iamnot([Leftover] IRole role)
            {
                var guildUser = (IGuildUser) ctx.User;

                var (result, autoDelete) = await _service.Remove(guildUser, role).ConfigureAwait(false);

                IUserMessage msg;
                if (result == SelfAssignedRolesService.RemoveResult.Err_Not_Assignable)
                    msg = await ReplyErrorLocalizedAsync("self_assign_not").ConfigureAwait(false);
                else if (result == SelfAssignedRolesService.RemoveResult.Err_Not_Have)
                    msg = await ReplyErrorLocalizedAsync("self_assign_not_have", Format.Bold(role.Name))
                        .ConfigureAwait(false);
                else if (result == SelfAssignedRolesService.RemoveResult.Err_Not_Perms)
                    msg = await ReplyErrorLocalizedAsync("self_assign_perms").ConfigureAwait(false);
                else
                    msg = await ReplyConfirmLocalizedAsync("self_assign_remove", Format.Bold(role.Name))
                        .ConfigureAwait(false);

                if (autoDelete)
                {
                    msg.DeleteAfter(3);
                    ctx.Message.DeleteAfter(3);
                }
            }
        }
    }
}