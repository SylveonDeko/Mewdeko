using DataModel;
using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Utility.Services;

namespace Mewdeko.Modules.Utility;

public partial class Utility
{
    /// <summary>
    ///     Provides commands for roles members earn at a number of invites.
    /// </summary>
    [Group]
    public class InviteRoleCommands : MewdekoSubmodule<InviteRoleService>
    {
        /// <summary>
        ///     Gives a role to members who reach a number of net invites.
        /// </summary>
        /// <param name="role">The role.</param>
        /// <param name="invites">The net invites needed.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        [BotPerm(GuildPermission.ManageRoles)]
        public async Task InviteRoleAdd(IRole role, int invites)
        {
            if (!InviteRoleService.CanAssign((SocketGuild)ctx.Guild, role.Id))
            {
                await ReplyErrorAsync(Strings.InviteRoleUnassignable(ctx.Guild.Id, role.Mention));
                return;
            }

            var saved = await Service.SetRoleAsync(ctx.Guild.Id, role.Id, Math.Max(1, invites));
            await ReplyConfirmAsync(Strings.InviteRoleSet(ctx.Guild.Id, role.Mention, saved.RequiredInvites));
        }

        /// <summary>
        ///     Stops giving a role at a number of invites. Members keep the role.
        /// </summary>
        /// <param name="role">The role.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task InviteRoleRemove(IRole role)
        {
            await (await Service.RemoveRoleAsync(ctx.Guild.Id, role.Id)
                ? ReplyConfirmAsync(Strings.InviteRoleRemoved(ctx.Guild.Id, role.Mention))
                : ReplyErrorAsync(Strings.InviteRoleMissing(ctx.Guild.Id, role.Mention)));
        }

        /// <summary>
        ///     Lists the invite milestone roles and how they are handed out.
        /// </summary>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task InviteRoles()
        {
            var roles = await Service.GetRolesAsync(ctx.Guild.Id);
            if (roles.Count == 0)
            {
                await ReplyErrorAsync(Strings.InviteRolesNone(ctx.Guild.Id));
                return;
            }

            var settings = await Service.GetSettingsAsync(ctx.Guild.Id);
            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithTitle(Strings.InviteRolesTitle(ctx.Guild.Id))
                .WithDescription(string.Join("\n", roles.Take(40).Select(x =>
                    Strings.InviteRolesLine(ctx.Guild.Id, x.RequiredInvites, MentionUtils.MentionRole(x.RoleId)))))
                .AddField(Strings.InviteRolesMode(ctx.Guild.Id), settings.Mode == DataModel.InviteRoleMode.HighestOnly
                    ? Strings.InviteRolesModeHighest(ctx.Guild.Id)
                    : Strings.InviteRolesModeStack(ctx.Guild.Id), true)
                .AddField(Strings.InviteRolesRemoveOnDrop(ctx.Guild.Id), GetEnDis(settings.RemoveOnDrop), true);

            await ctx.Channel.SendMessageAsync(embed: eb.Build());
        }

        /// <summary>
        ///     Sets whether members keep every milestone role they reach or only the highest.
        /// </summary>
        /// <param name="mode">StackAll or HighestOnly.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task InviteRoleMode(InviteRoleMode mode)
        {
            await Service.UpdateSettingsAsync(ctx.Guild.Id, mode);
            await ReplyConfirmAsync(mode == DataModel.InviteRoleMode.HighestOnly
                ? Strings.InviteRoleModeHighestSet(ctx.Guild.Id)
                : Strings.InviteRoleModeStackSet(ctx.Guild.Id));
        }

        /// <summary>
        ///     Toggles whether members lose a milestone role when their invites fall below it.
        /// </summary>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task InviteRoleDrop()
        {
            var current = await Service.GetSettingsAsync(ctx.Guild.Id);
            var saved = await Service.UpdateSettingsAsync(ctx.Guild.Id, removeOnDrop: !current.RemoveOnDrop);
            await ReplyConfirmAsync(saved.RemoveOnDrop
                ? Strings.InviteRoleDropOn(ctx.Guild.Id)
                : Strings.InviteRoleDropOff(ctx.Guild.Id));
        }

        /// <summary>
        ///     Gives or takes milestone roles for every member to match their invites.
        /// </summary>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        [BotPerm(GuildPermission.ManageRoles)]
        [Ratelimit(60)]
        public async Task InviteRoleSync()
        {
            var changed = await Service.SyncAsync((SocketGuild)ctx.Guild);
            await ReplyConfirmAsync(Strings.InviteRolesSynced(ctx.Guild.Id, changed));
        }

        private static string GetEnDis(bool endis)
        {
            return endis ? "Enabled" : "Disabled";
        }
    }
}
