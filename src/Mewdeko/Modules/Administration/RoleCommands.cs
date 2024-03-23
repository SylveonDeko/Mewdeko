using System.Net;
using Discord.Commands;
using Discord.Net;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Administration.Services;
using Serilog;
using SkiaSharp;

namespace Mewdeko.Modules.Administration;

public partial class Administration
{
    /// <summary>
    /// Commands for managing roles.
    /// </summary>
    /// <param name="services">Main services provider for the bot.</param>
    /// <param name="intserv">Interactive service used for paginated embeds.</param>
    public class RoleCommands(IServiceProvider services, InteractiveService intserv)
        : MewdekoSubmodule<RoleCommandsService>
    {
        /// <summary>
        /// Enumerates different variations of the term "exclude". Sets whether roles are exclusive.
        /// </summary>
        public enum Exclude
        {
            /// <summary>
            /// Represents the term "excl".
            /// </summary>
            Excl
        }

        private async Task? InternalReactionRoles(bool exclusive, ulong? messageId, params string[] input)
        {
            var target = messageId is { } msgId
                ? await ctx.Channel.GetMessageAsync(msgId).ConfigureAwait(false)
                : (await ctx.Channel.GetMessagesAsync(2).FlattenAsync().ConfigureAwait(false))
                .Skip(1)
                .FirstOrDefault();

            if (input.Length % 2 != 0)
                return;

            var grp = 0;
            var results = input
                .GroupBy(_ => grp++ / 2)
                .Select(async x =>
                {
                    var inputRoleStr = x.First();
                    var roleReader = new RoleTypeReader<SocketRole>();
                    var roleResult = await roleReader.ReadAsync(ctx, inputRoleStr, services).ConfigureAwait(false);
                    if (!roleResult.IsSuccess)
                    {
                        Log.Warning("Role {0} not found", inputRoleStr);
                        return null;
                    }

                    var role = (IRole)roleResult.BestMatch;
                    if (role.Position > ((IGuildUser)ctx.User).GetRoles().Select(r => r.Position).Max()
                        && ctx.User.Id != ctx.Guild.OwnerId)
                    {
                        return null;
                    }

                    var emote = x.Last().ToIEmote();
                    return new
                    {
                        role, emote
                    };
                })
                .Where(x => x != null);

            var all = await Task.WhenAll(results).ConfigureAwait(false);

            if (all.Length == 0)
                return;

            foreach (var x in all)
            {
                try
                {
                    if (target != null)
                    {
                        await target.AddReactionAsync(x.emote, new RequestOptions
                        {
                            RetryMode = RetryMode.Retry502 | RetryMode.RetryRatelimit
                        }).ConfigureAwait(false);
                    }
                }
                catch (HttpException ex) when (ex.HttpCode == HttpStatusCode.BadRequest)
                {
                    await ReplyErrorLocalizedAsync("reaction_cant_access", Format.Code(x.emote.ToString()))
                        .ConfigureAwait(false);
                    return;
                }

                await Task.Delay(500).ConfigureAwait(false);
            }

            if (target != null && await Service.Add(ctx.Guild.Id, new ReactionRoleMessage
                {
                    Exclusive = exclusive ? 1 : 0,
                    MessageId = target.Id,
                    ChannelId = target.Channel.Id,
                    ReactionRoles = all.Select(x => new ReactionRole
                    {
                        EmoteName = x.emote.ToString(), RoleId = x.role.Id
                    }).ToList()
                }))
            {
                await ctx.OkAsync().ConfigureAwait(false);
            }
            else
            {
                await ReplyErrorLocalizedAsync("reaction_roles_full").ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Assigns reaction roles based on the provided input.
        /// </summary>
        /// <remarks>
        /// This command allows administrators to manage reaction roles in the server.
        /// It requires the Manage Roles permission for the user and the Manage Roles permission for the bot.
        /// </remarks>
        /// <param name="messageId">The ID of the message to which reactions will be added.</param>
        /// <param name="input">The roles and emojis to be associated with reactions.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd, Aliases, RequireContext(ContextType.Guild), BotPerm(GuildPermission.ManageRoles), Priority(0)]
        public Task ReactionRoles(ulong messageId, params string[] input) =>
            InternalReactionRoles(false, messageId, input);

        /// <summary>
        /// Assigns reaction roles based on the provided input, excluding certain roles.
        /// </summary>
        /// <remarks>
        /// This command allows administrators to manage reaction roles in the server while making all roles exclusive.
        /// It requires the Manage Roles permission for the user and the Manage Roles permission for the bot.
        /// </remarks>
        /// <param name="messageId">The ID of the message to which reactions will be added.</param>
        /// <param name="input">The roles and emojis to be associated with reactions.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.ManageRoles),
         BotPerm(GuildPermission.ManageRoles), Priority(1)]
        public Task ReactionRoles(ulong messageId, Exclude _, params string[] input) =>
            InternalReactionRoles(true, messageId, input);

        /// <summary>
        /// Assigns reaction roles based on the provided input, excluding certain roles.
        /// </summary>
        /// <remarks>
        /// This command allows administrators to manage reaction roles in the server while making all roles exclusive.
        /// It requires the Manage Roles permission for the user and the Manage Roles permission for the bot.
        /// </remarks>
        /// <param name="messageId">The ID of the message to which reactions will be added.</param>
        /// <param name="_">Exclusion parameter (ignored).</param>
        /// <param name="input">The roles and emojis to be associated with reactions.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.ManageRoles),
         BotPerm(GuildPermission.ManageRoles), Priority(1)]
        public Task ReactionRoles(Exclude _, ulong messageId, params string[] input) =>
            InternalReactionRoles(true, messageId, input);

        /// <summary>
        /// Assigns reaction roles based on the provided input.
        /// </summary>
        /// <remarks>
        /// This command allows administrators to manage reaction roles in the server.
        /// It requires the Manage Roles permission for the user and the Manage Roles permission for the bot.
        /// </remarks>
        /// <param name="input">The roles and emojis to be associated with reactions.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageRoles), BotPerm(GuildPermission.ManageRoles), Priority(0)]
        public Task ReactionRoles(params string[] input) => InternalReactionRoles(false, null, input);

        /// <summary>
        /// Assigns reaction roles based on the provided input, excluding certain roles.
        /// </summary>
        /// <remarks>
        /// This command allows administrators to manage reaction roles in the server while making all roles exclusive.
        /// It requires the Manage Roles permission for the user and the Manage Roles permission for the bot.
        /// </remarks>
        /// <param name="input">The roles and emojis to be associated with reactions.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageRoles), BotPerm(GuildPermission.ManageRoles), Priority(1)]
        public Task ReactionRoles(Exclude _, params string[] input) => InternalReactionRoles(true, null, input);


        /// <summary>
        /// Displays a list of reaction roles configured in the server.
        /// </summary>
        /// <remarks>
        /// This command allows administrators to view a list of reaction roles configured in the server.
        /// It requires the Manage Roles permission for the user.
        /// </remarks>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageRoles)]
        public async Task ReactionRolesList()
        {
            if (!Service.Get(ctx.Guild.Id, out var rrs) || rrs.Count == 0)
            {
                await ctx.Channel.SendErrorAsync(GetText("no_reaction_roles")).ConfigureAwait(false);
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

                await intserv.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60))
                    .ConfigureAwait(false);

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
                            .AddField(GetText("rero_roles_count", rr.ReactionRoles.Count),
                                string.Join(",",
                                    rr.ReactionRoles.Select(x => $"{x.EmoteName} {g.GetRole(x.RoleId).Mention}")))
                            .AddField(GetText("users_can_select_morethan_one"), rr.Exclusive == 1)
                            .AddField(GetText("wasdeleted"), msg == null ? GetText("yes") : GetText("no"))
                            .AddField(GetText("messagelink"),
                                msg == null
                                    ? GetText("messagewasdeleted")
                                    : $"[{GetText("HYATT")}]({msg.GetJumpUrl()})");
                }
            }
        }

        /// <summary>
        /// Removes a reaction role based on its index.
        /// </summary>
        /// <remarks>
        /// This command allows administrators to remove a reaction role based on its index in the list.
        /// It requires the Manage Roles permission for the user.
        /// </remarks>
        /// <param name="index">The index of the reaction role to remove.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageRoles)]
        public async Task ReactionRolesRemove(int index)
        {
            if (index < 1 ||
                !Service.Get(ctx.Guild.Id, out var rrs) ||
                rrs.Count == 0 || rrs.Count < index)
            {
                return;
            }

            index--;
            await Service.Remove(ctx.Guild.Id, index);
            await ReplyConfirmLocalizedAsync("reaction_role_removed", index + 1).ConfigureAwait(false);
        }

        /// <summary>
        /// Sets a role to a user.
        /// </summary>
        /// <remarks>
        /// This command allows administrators to set a role to a specified user.
        /// It requires the Manage Roles permission for the user.
        /// </remarks>
        /// <param name="roleToAdd">The role to add to the user.</param>
        /// <param name="targetUser">The user to add the role to.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageRoles), BotPerm(GuildPermission.ManageRoles)]
        public async Task SetRole(IRole roleToAdd, [Remainder] IGuildUser targetUser)
        {
            var runnerUser = (IGuildUser)ctx.User;
            var runnerMaxRolePosition = runnerUser.GetRoles().Max(x => x.Position);
            if (ctx.User.Id != ctx.Guild.OwnerId && runnerMaxRolePosition <= roleToAdd.Position)
                return;
            try
            {
                await targetUser.AddRoleAsync(roleToAdd).ConfigureAwait(false);

                await ReplyConfirmLocalizedAsync("setrole", Format.Bold(roleToAdd.Name),
                        Format.Bold(targetUser.ToString()))
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error in setrole command");
                await ReplyErrorLocalizedAsync("setrole_err").ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Adds a role to a user.
        /// </summary>
        /// <remarks>
        /// This command allows administrators to set a role to a specified user.
        /// It requires the Manage Roles permission for the user.
        /// </remarks>
        /// <param name="targetUser">The user to add the role to.</param>
        /// <param name="roleToAdd">The role to add to the user.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageRoles), BotPerm(GuildPermission.ManageRoles)]
        public async Task SetRole(IGuildUser targetUser, [Remainder] IRole roleToAdd)
        {
            var runnerUser = (IGuildUser)ctx.User;
            var runnerMaxRolePosition = runnerUser.GetRoles().Max(x => x.Position);
            if (ctx.User.Id != ctx.Guild.OwnerId && runnerMaxRolePosition <= roleToAdd.Position)
                return;
            try
            {
                await targetUser.AddRoleAsync(roleToAdd).ConfigureAwait(false);

                await ReplyConfirmLocalizedAsync("setrole", Format.Bold(roleToAdd.Name),
                        Format.Bold(targetUser.ToString()))
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error in setrole command");
                await ReplyErrorLocalizedAsync("setrole_err").ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Removes a role from a user.
        /// </summary>
        /// <remarks>
        /// This command allows administrators to remove a role from a specified user.
        /// It requires the Manage Roles permission for the user.
        /// </remarks>
        /// <param name="roleToRemove">The role to remove from the user.</param>
        /// <param name="targetUser">The user to remove the role from.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageRoles), BotPerm(GuildPermission.ManageRoles)]
        public async Task RemoveRole(IRole roleToRemove, [Remainder] IGuildUser targetUser)
        {
            var runnerUser = (IGuildUser)ctx.User;
            if (ctx.User.Id != runnerUser.Guild.OwnerId &&
                runnerUser.GetRoles().Max(x => x.Position) <= roleToRemove.Position)
            {
                return;
            }

            try
            {
                await targetUser.RemoveRoleAsync(roleToRemove).ConfigureAwait(false);
                await ReplyConfirmLocalizedAsync("remrole", Format.Bold(roleToRemove.Name),
                    Format.Bold(targetUser.ToString())).ConfigureAwait(false);
            }
            catch
            {
                await ReplyErrorLocalizedAsync("remrole_err").ConfigureAwait(false);
            }
        }


        /// <summary>
        /// Removes a role from a user.
        /// </summary>
        /// <remarks>
        /// This command allows administrators to remove a role from a specified user.
        /// It requires the Manage Roles permission for the user.
        /// </remarks>
        /// <param name="targetUser">The user to remove the role from.</param>
        /// <param name="roleToRemove">The role to remove from the user.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageRoles), BotPerm(GuildPermission.ManageRoles)]
        public async Task RemoveRole(IGuildUser targetUser, [Remainder] IRole roleToRemove)
        {
            var runnerUser = (IGuildUser)ctx.User;
            if (ctx.User.Id != runnerUser.Guild.OwnerId &&
                runnerUser.GetRoles().Max(x => x.Position) <= roleToRemove.Position)
            {
                return;
            }

            try
            {
                await targetUser.RemoveRoleAsync(roleToRemove).ConfigureAwait(false);
                await ReplyConfirmLocalizedAsync("remrole", Format.Bold(roleToRemove.Name),
                    Format.Bold(targetUser.ToString())).ConfigureAwait(false);
            }
            catch
            {
                await ReplyErrorLocalizedAsync("remrole_err").ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Renames a role.
        /// </summary>
        /// <remarks>
        /// This command allows administrators to rename a specified role.
        /// It requires the Manage Roles permission for the user.
        /// </remarks>
        /// <param name="roleToEdit">The role to rename.</param>
        /// <param name="newname">The new name for the role.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageRoles), BotPerm(GuildPermission.ManageRoles)]
        public async Task RenameRole(IRole roleToEdit, [Remainder] string newname)
        {
            var guser = (IGuildUser)ctx.User;
            if (ctx.User.Id != guser.Guild.OwnerId && guser.GetRoles().Max(x => x.Position) <= roleToEdit.Position)
                return;
            try
            {
                if (roleToEdit.Position > (await ctx.Guild.GetCurrentUserAsync().ConfigureAwait(false)).GetRoles()
                    .Max(r => r.Position))
                {
                    await ReplyErrorLocalizedAsync("renrole_perms").ConfigureAwait(false);
                    return;
                }

                await roleToEdit.ModifyAsync(g => g.Name = newname).ConfigureAwait(false);
                await ReplyConfirmLocalizedAsync("renrole").ConfigureAwait(false);
            }
            catch (Exception)
            {
                await ReplyErrorLocalizedAsync("renrole_err").ConfigureAwait(false);
            }
        }


        /// <summary>
        /// Removes all roles from a user except managed roles and the everyone role.
        /// </summary>
        /// <remarks>
        /// This command allows administrators to remove all roles from a specified user except managed roles and the everyone role.
        /// It requires the Manage Roles permission for the user.
        /// </remarks>
        /// <param name="user">The user from whom to remove all roles.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageRoles), BotPerm(GuildPermission.ManageRoles)]
        public async Task RemoveAllRoles([Remainder] IGuildUser user)
        {
            var guser = (IGuildUser)ctx.User;

            var userRoles = user.GetRoles()
                .Where(x => !x.IsManaged && x != x.Guild.EveryoneRole)
                .ToList();

            if (user.Id == ctx.Guild.OwnerId || (ctx.User.Id != ctx.Guild.OwnerId &&
                                                 guser.GetRoles().Max(x => x.Position) <=
                                                 userRoles.Max(x => x.Position)))
            {
                return;
            }

            try
            {
                await user.RemoveRolesAsync(userRoles).ConfigureAwait(false);
                await ReplyConfirmLocalizedAsync("rar", Format.Bold(user.ToString())).ConfigureAwait(false);
            }
            catch (Exception)
            {
                await ReplyErrorLocalizedAsync("rar_err").ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Creates a new role with the specified name.
        /// </summary>
        /// <remarks>
        /// This command allows administrators to create a new role with the specified name.
        /// It requires the Manage Roles permission for the user.
        /// </remarks>
        /// <param name="roleName">The name of the role to create.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageRoles), BotPerm(GuildPermission.ManageRoles)]
        public async Task CreateRole([Remainder] string? roleName = null)
        {
            if (string.IsNullOrWhiteSpace(roleName))
                return;

            var r = await ctx.Guild.CreateRoleAsync(roleName, isMentionable: false).ConfigureAwait(false);
            await ReplyConfirmLocalizedAsync("cr", Format.Bold(r.Name)).ConfigureAwait(false);
        }

        /// <summary>
        /// Deletes the specified role.
        /// </summary>
        /// <remarks>
        /// This command allows administrators to delete the specified role.
        /// It requires the Manage Roles permission for the user.
        /// </remarks>
        /// <param name="role">The role to delete.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageRoles), BotPerm(GuildPermission.ManageRoles)]
        public async Task DeleteRole([Remainder] IRole role)
        {
            var guser = (IGuildUser)ctx.User;
            if (ctx.User.Id != guser.Guild.OwnerId
                && guser.GetRoles().Max(x => x.Position) <= role.Position)
            {
                return;
            }

            await role.DeleteAsync().ConfigureAwait(false);
            await ReplyConfirmLocalizedAsync("dr", Format.Bold(role.Name)).ConfigureAwait(false);
        }


        /// <summary>
        /// Toggles the hoist status of the specified role.
        /// </summary>
        /// <remarks>
        /// This command allows administrators to toggle the hoist status of the specified role.
        /// It requires the Manage Roles permission for the user.
        /// </remarks>
        /// <param name="role">The role to toggle the hoist status for.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageRoles), BotPerm(GuildPermission.ManageRoles)]
        public async Task RoleHoist(IRole role)
        {
            var newHoisted = !role.IsHoisted;
            await role.ModifyAsync(r => r.Hoist = newHoisted).ConfigureAwait(false);
            if (newHoisted)
            {
                await ReplyConfirmLocalizedAsync("rolehoist_enabled", Format.Bold(role.Name)).ConfigureAwait(false);
            }
            else
            {
                await ReplyConfirmLocalizedAsync("rolehoist_disabled", Format.Bold(role.Name))
                    .ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Displays the hexadecimal color value of the specified role.
        /// </summary>
        /// <remarks>
        /// This command allows users to see the hexadecimal color value of the specified role.
        /// </remarks>
        /// <param name="role">The role to display the color for.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd, Aliases, RequireContext(ContextType.Guild), Priority(1)]
        public async Task RoleColor([Remainder] IRole role) =>
            await ctx.Channel.SendConfirmAsync(GetText("rolecolor"), role.Color.RawValue.ToString("x6"))
                .ConfigureAwait(false);

        /// <summary>
        /// Changes the color of the specified role.
        /// </summary>
        /// <remarks>
        /// This command allows administrators to change the color of the specified role.
        /// It requires the Manage Roles permission for the user.
        /// </remarks>
        /// <param name="role">The role to change the color for.</param>
        /// <param name="color">The new color for the role.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageRoles), BotPerm(GuildPermission.ManageRoles), Priority(0)]
        public async Task RoleColor(IRole role, SKColor color)
        {
            try
            {
                await role.ModifyAsync(r => r.Color = new Color(color.Red, color.Green, color.Blue))
                    .ConfigureAwait(false);
                await ReplyConfirmLocalizedAsync("rc", Format.Bold(role.Name)).ConfigureAwait(false);
            }
            catch (Exception)
            {
                await ReplyErrorLocalizedAsync("rc_perms").ConfigureAwait(false);
            }
        }
    }
}