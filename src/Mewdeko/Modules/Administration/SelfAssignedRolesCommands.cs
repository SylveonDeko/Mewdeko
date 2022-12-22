using System.Text;
using System.Threading.Tasks;
using Discord.Commands;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Administration.Services;

namespace Mewdeko.Modules.Administration;

public partial class Administration
{
    [Group]
    public class SelfAssignedRolesCommands : MewdekoSubmodule<SelfAssignedRolesService>
    {
        private readonly InteractiveService interactivity;
        private readonly GuildSettingsService guildSettings;

        public SelfAssignedRolesCommands(InteractiveService serv, GuildSettingsService guildSettings)
        {
            interactivity = serv;
            this.guildSettings = guildSettings;
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageMessages), BotPerm(GuildPermission.ManageMessages)]
        public async Task AdSarm()
        {
            var newVal = await Service.ToggleAdSarm(ctx.Guild.Id);

            if (newVal)
                await ReplyConfirmLocalizedAsync("adsarm_enable", await guildSettings.GetPrefix(ctx.Guild)).ConfigureAwait(false);
            else
                await ReplyConfirmLocalizedAsync("adsarm_disable", await guildSettings.GetPrefix(ctx.Guild)).ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageRoles), BotPerm(GuildPermission.ManageRoles), Priority(1)]
        public async Task Asar([Remainder] IRole role) => await Asar(0, role);

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

            await interactivity.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60)).ConfigureAwait(false);

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
                    var groupNameText = Format.Bold(!groups.TryGetValue(kvp.Key, out var name) ? GetText("self_assign_group", kvp.Key) : $"{kvp.Key} - {name.TrimTo(25, true)}");

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

        [Cmd, Aliases, RequireContext(ContextType.Guild)]
        public async Task Iam([Remainder] IRole role)
        {
            var guildUser = (IGuildUser)ctx.User;

            var (result, autoDelete, extra) = await Service.Assign(guildUser, role).ConfigureAwait(false);

            IUserMessage msg;
            if (result == SelfAssignedRolesService.AssignResult.ErrNotAssignable)
            {
                msg = await ReplyErrorLocalizedAsync("self_assign_not").ConfigureAwait(false);
            }
            else if (result == SelfAssignedRolesService.AssignResult.ErrLvlReq)
            {
                msg = await ReplyErrorLocalizedAsync("self_assign_not_level", Format.Bold(extra.ToString()))
                    .ConfigureAwait(false);
            }
            else if (result == SelfAssignedRolesService.AssignResult.ErrAlreadyHave)
            {
                msg = await ReplyErrorLocalizedAsync("self_assign_already", Format.Bold(role.Name))
                    .ConfigureAwait(false);
            }
            else if (result == SelfAssignedRolesService.AssignResult.ErrNotPerms)
            {
                msg = await ReplyErrorLocalizedAsync("self_assign_perms").ConfigureAwait(false);
            }
            else
            {
                msg = await ReplyConfirmLocalizedAsync("self_assign_success", Format.Bold(role.Name))
                    .ConfigureAwait(false);
            }

            if (autoDelete)
            {
                msg.DeleteAfter(3);
                ctx.Message.DeleteAfter(3);
            }
        }

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