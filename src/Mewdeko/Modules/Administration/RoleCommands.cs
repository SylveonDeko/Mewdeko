using System.Net;
using DataModel;
using Discord.Commands;
using Discord.Net;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Humanizer;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Common.TypeReaders.Models;
using Mewdeko.Modules.Administration.Services;
using Mewdeko.Modules.Moderation.Services;
using SkiaSharp;

namespace Mewdeko.Modules.Administration;

public partial class Administration
{
    /// <summary>
    ///     Commands for managing roles.
    /// </summary>
    /// <param name="services">Main services provider for the bot.</param>
    /// <param name="intserv">Interactive service used for paginated embeds.</param>
    /// <param name="logger">The logger instance for structured logging.</param>
    /// <param name="muteService">Service for managing timed roles and mutes.</param>
    public class RoleCommands(
        IServiceProvider services,
        InteractiveService intserv,
        ILogger<RoleCommands> logger,
        MuteService muteService)
        : MewdekoSubmodule<RoleCommandsService>
    {
        /// <summary>
        ///     Enumerates different variations of the term "exclude". Sets whether roles are exclusive.
        /// </summary>
        public enum Exclude
        {
            /// <summary>
            ///     Represents the term "excl".
            /// </summary>
            Excl
        }

        private async Task InternalReactionRoles(bool exclusive, ulong? messageId, params string[] input)
        {
            try
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
                            logger.LogWarning("Role {0} not found", inputRoleStr);
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
                        await ReplyErrorAsync(Strings.ReactionCantAccess(ctx.Guild.Id, Format.Code(x.emote.ToString())))
                            .ConfigureAwait(false);
                        return;
                    }

                    await Task.Delay(500).ConfigureAwait(false);
                }

                if (target != null && await Service.Add(ctx.Guild.Id, new ReactionRoleMessage
                    {
                        Exclusive = exclusive,
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
                    await ReplyErrorAsync(Strings.ReactionRolesError(ctx.Guild.Id)).ConfigureAwait(false);
                }
            }
            catch (Exception e)
            {
                logger.LogError("There was an error adding a reaction role, see below exception: {Exception}", e);
            }
        }

        /// <summary>
        ///     Assigns reaction roles based on the provided input.
        /// </summary>
        /// <remarks>
        ///     This command allows administrators to manage reaction roles in the server.
        ///     It requires the Manage Roles permission for the user and the Manage Roles permission for the bot.
        /// </remarks>
        /// <param name="messageId">The ID of the message to which reactions will be added.</param>
        /// <param name="input">The roles and emojis to be associated with reactions.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [BotPerm(GuildPermission.ManageRoles)]
        [Priority(0)]
        public Task ReactionRoles(ulong messageId, params string[] input)
        {
            return InternalReactionRoles(false, messageId, input);
        }

        /// <summary>
        ///     Assigns reaction roles based on the provided input, excluding certain roles.
        /// </summary>
        /// <remarks>
        ///     This command allows administrators to manage reaction roles in the server while making all roles exclusive.
        ///     It requires the Manage Roles permission for the user and the Manage Roles permission for the bot.
        /// </remarks>
        /// <param name="messageId">The ID of the message to which reactions will be added.</param>
        /// <param name="_">Used to mark </param>
        /// <param name="input">The roles and emojis to be associated with reactions.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageRoles)]
        [BotPerm(GuildPermission.ManageRoles)]
        [Priority(1)]
        public async Task ReactionRoles(ulong messageId, Exclude _, params string[] input)
        {
            await InternalReactionRoles(true, messageId, input);
        }

        /// <summary>
        ///     Assigns reaction roles based on the provided input, excluding certain roles.
        /// </summary>
        /// <remarks>
        ///     This command allows administrators to manage reaction roles in the server while making all roles exclusive.
        ///     It requires the Manage Roles permission for the user and the Manage Roles permission for the bot.
        /// </remarks>
        /// <param name="messageId">The ID of the message to which reactions will be added.</param>
        /// <param name="_">Exclusion parameter (ignored).</param>
        /// <param name="input">The roles and emojis to be associated with reactions.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageRoles)]
        [BotPerm(GuildPermission.ManageRoles)]
        [Priority(1)]
        public async Task ReactionRoles(Exclude _, ulong messageId, params string[] input)
        {
            await InternalReactionRoles(true, messageId, input);
        }

        /// <summary>
        ///     Assigns reaction roles based on the provided input.
        /// </summary>
        /// <remarks>
        ///     This command allows administrators to manage reaction roles in the server.
        ///     It requires the Manage Roles permission for the user and the Manage Roles permission for the bot.
        /// </remarks>
        /// <param name="input">The roles and emojis to be associated with reactions.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageRoles)]
        [BotPerm(GuildPermission.ManageRoles)]
        [Priority(0)]
        public async Task ReactionRoles(params string[] input)
        {
            await InternalReactionRoles(false, null, input);
        }

        /// <summary>
        ///     Assigns reaction roles based on the provided input, excluding certain roles.
        /// </summary>
        /// <remarks>
        ///     This command allows administrators to manage reaction roles in the server while making all roles exclusive.
        ///     It requires the Manage Roles permission for the user and the Manage Roles permission for the bot.
        /// </remarks>
        /// <param name="_">The exclusion parameter to make roles exclusive.</param>
        /// <param name="input">The roles and emojis to be associated with reactions.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageRoles)]
        [BotPerm(GuildPermission.ManageRoles)]
        [Priority(1)]
        public async Task ReactionRoles(Exclude _, params string[] input)
        {
            await InternalReactionRoles(true, null, input);
        }


        /// <summary>
        ///     Displays a list of reaction roles configured in the server.
        /// </summary>
        /// <remarks>
        ///     This command allows administrators to view a list of reaction roles configured in the server.
        ///     It requires the Manage Roles permission for the user.
        /// </remarks>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageRoles)]
        public async Task ReactionRolesList()
        {
            var (success, rrs) = await Service.Get(ctx.Guild.Id).ConfigureAwait(false);
            if (!success || rrs.Count == 0)
            {
                await ctx.Channel.SendErrorAsync(Strings.NoReactionRoles(ctx.Guild.Id), Config).ConfigureAwait(false);
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
        ///     Removes a reaction role based on its index.
        /// </summary>
        /// <remarks>
        ///     This command allows administrators to remove a reaction role based on its index in the list.
        ///     It requires the Manage Roles permission for the user.
        /// </remarks>
        /// <param name="index">The index of the reaction role to remove.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageRoles)]
        public async Task ReactionRolesRemove(int index)
        {
            var (success, rrs) = await Service.Get(ctx.Guild.Id).ConfigureAwait(false);
            if (index < 1 ||
                !success ||
                rrs.Count == 0 || rrs.Count < index)
            {
                return;
            }

            index--;
            await Service.Remove(ctx.Guild.Id, index);
            await ReplyConfirmAsync(Strings.ReactionRoleRemoved(ctx.Guild.Id, index + 1)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets a role to a user.
        /// </summary>
        /// <remarks>
        ///     This command allows administrators to set a role to a specified user.
        ///     It requires the Manage Roles permission for the user.
        /// </remarks>
        /// <param name="roleToAdd">The role to add to the user.</param>
        /// <param name="targetUser">The user to add the role to.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageRoles)]
        [BotPerm(GuildPermission.ManageRoles)]
        [Priority(0)]
        public async Task SetRole(IRole roleToAdd, IGuildUser targetUser)
        {
            var runnerUser = (IGuildUser)ctx.User;
            var runnerMaxRolePosition = runnerUser.GetRoles().Max(x => x.Position);
            if (ctx.User.Id != ctx.Guild.OwnerId && runnerMaxRolePosition <= roleToAdd.Position)
                return;
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
        }

        /// <summary>
        ///     Sets a role to a user for a specified time duration.
        /// </summary>
        /// <remarks>
        ///     This command allows administrators to set a role to a specified user with a time limit.
        ///     It requires the Manage Roles permission for the user.
        /// </remarks>
        /// <param name="roleToAdd">The role to add to the user.</param>
        /// <param name="targetUser">The user to add the role to.</param>
        /// <param name="time">The duration for which the role will be active.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageRoles)]
        [BotPerm(GuildPermission.ManageRoles)]
        [Priority(1)]
        public async Task SetRole(IRole roleToAdd, IGuildUser targetUser, StoopidTime time)
        {
            var runnerUser = (IGuildUser)ctx.User;
            var runnerMaxRolePosition = runnerUser.GetRoles().Max(x => x.Position);
            if (ctx.User.Id != ctx.Guild.OwnerId && runnerMaxRolePosition <= roleToAdd.Position)
                return;
            try
            {
                await muteService.TimedRole(targetUser, time.Time, $"Timed role assignment by {ctx.User}", roleToAdd)
                    .ConfigureAwait(false);

                await ReplyConfirmAsync(Strings.SetroleTime(ctx.Guild.Id, Format.Bold(roleToAdd.Name),
                        Format.Bold(targetUser.ToString()), time.Time.Humanize()))
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error in timed setrole command");
                await ReplyErrorAsync(Strings.SetroleErr(ctx.Guild.Id)).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Adds a role to a user.
        /// </summary>
        /// <remarks>
        ///     This command allows administrators to set a role to a specified user.
        ///     It requires the Manage Roles permission for the user.
        /// </remarks>
        /// <param name="targetUser">The user to add the role to.</param>
        /// <param name="roleToAdd">The role to add to the user.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageRoles)]
        [BotPerm(GuildPermission.ManageRoles)]
        [Priority(0)]
        public async Task SetRole(IGuildUser targetUser, IRole roleToAdd)
        {
            var runnerUser = (IGuildUser)ctx.User;
            var runnerMaxRolePosition = runnerUser.GetRoles().Max(x => x.Position);
            if (ctx.User.Id != ctx.Guild.OwnerId && runnerMaxRolePosition <= roleToAdd.Position)
                return;
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
        }

        /// <summary>
        ///     Adds a role to a user for a specified time duration.
        /// </summary>
        /// <remarks>
        ///     This command allows administrators to set a role to a specified user with a time limit.
        ///     It requires the Manage Roles permission for the user.
        /// </remarks>
        /// <param name="targetUser">The user to add the role to.</param>
        /// <param name="roleToAdd">The role to add to the user.</param>
        /// <param name="time">The duration for which the role will be active.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageRoles)]
        [BotPerm(GuildPermission.ManageRoles)]
        [Priority(1)]
        public async Task SetRole(IGuildUser targetUser, IRole roleToAdd, StoopidTime time)
        {
            var runnerUser = (IGuildUser)ctx.User;
            var runnerMaxRolePosition = runnerUser.GetRoles().Max(x => x.Position);
            if (ctx.User.Id != ctx.Guild.OwnerId && runnerMaxRolePosition <= roleToAdd.Position)
                return;
            try
            {
                await muteService.TimedRole(targetUser, time.Time, $"Timed role assignment by {ctx.User}", roleToAdd)
                    .ConfigureAwait(false);

                await ReplyConfirmAsync(Strings.SetroleTime(ctx.Guild.Id, Format.Bold(roleToAdd.Name),
                        Format.Bold(targetUser.ToString()), time.Time.Humanize()))
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
        /// <remarks>
        ///     This command allows administrators to remove a role from a specified user.
        ///     It requires the Manage Roles permission for the user.
        /// </remarks>
        /// <param name="roleToRemove">The role to remove from the user.</param>
        /// <param name="targetUser">The user to remove the role from.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageRoles)]
        [BotPerm(GuildPermission.ManageRoles)]
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
                await ReplyConfirmAsync(Strings.Remrole(ctx.Guild.Id, Format.Bold(roleToRemove.Name),
                    Format.Bold(targetUser.ToString()))).ConfigureAwait(false);
            }
            catch
            {
                await ReplyErrorAsync(Strings.RemroleErr(ctx.Guild.Id)).ConfigureAwait(false);
            }
        }


        /// <summary>
        ///     Removes a role from a user.
        /// </summary>
        /// <remarks>
        ///     This command allows administrators to remove a role from a specified user.
        ///     It requires the Manage Roles permission for the user.
        /// </remarks>
        /// <param name="targetUser">The user to remove the role from.</param>
        /// <param name="roleToRemove">The role to remove from the user.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageRoles)]
        [BotPerm(GuildPermission.ManageRoles)]
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
                await ReplyConfirmAsync(Strings.Remrole(ctx.Guild.Id, Format.Bold(roleToRemove.Name),
                    Format.Bold(targetUser.ToString()))).ConfigureAwait(false);
            }
            catch
            {
                await ReplyErrorAsync(Strings.RemroleErr(ctx.Guild.Id)).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Renames a role.
        /// </summary>
        /// <remarks>
        ///     This command allows administrators to rename a specified role.
        ///     It requires the Manage Roles permission for the user.
        /// </remarks>
        /// <param name="roleToEdit">The role to rename.</param>
        /// <param name="newname">The new name for the role.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageRoles)]
        [BotPerm(GuildPermission.ManageRoles)]
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
        ///     Removes all roles from a user except managed roles and the everyone role.
        /// </summary>
        /// <remarks>
        ///     This command allows administrators to remove all roles from a specified user except managed roles and the everyone
        ///     role.
        ///     It requires the Manage Roles permission for the user.
        /// </remarks>
        /// <param name="user">The user from whom to remove all roles.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageRoles)]
        [BotPerm(GuildPermission.ManageRoles)]
        public async Task RemoveAllRoles([Remainder] IGuildUser user)
        {
            var guser = (IGuildUser)ctx.User;

            var userRoles = user.GetRoles()
                .Where(x => !x.IsManaged && x != x.Guild.EveryoneRole)
                .ToList();

            if (user.Id == ctx.Guild.OwnerId || ctx.User.Id != ctx.Guild.OwnerId &&
                guser.GetRoles().Max(x => x.Position) <=
                userRoles.Max(x => x.Position))
            {
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
        ///     Creates a new role with the specified name.
        /// </summary>
        /// <remarks>
        ///     This command allows administrators to create a new role with the specified name.
        ///     It requires the Manage Roles permission for the user.
        /// </remarks>
        /// <param name="roleName">The name of the role to create.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageRoles)]
        [BotPerm(GuildPermission.ManageRoles)]
        public async Task CreateRole([Remainder] string? roleName = null)
        {
            if (string.IsNullOrWhiteSpace(roleName))
                return;

            var r = await ctx.Guild.CreateRoleAsync(roleName, isMentionable: false).ConfigureAwait(false);
            await ReplyConfirmAsync(Strings.Cr(ctx.Guild.Id, Format.Bold(r.Name))).ConfigureAwait(false);
        }

        /// <summary>
        ///     Deletes the specified role.
        /// </summary>
        /// <remarks>
        ///     This command allows administrators to delete the specified role.
        ///     It requires the Manage Roles permission for the user.
        /// </remarks>
        /// <param name="role">The role to delete.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageRoles)]
        [BotPerm(GuildPermission.ManageRoles)]
        public async Task DeleteRole([Remainder] IRole role)
        {
            var guser = (IGuildUser)ctx.User;
            if (ctx.User.Id != guser.Guild.OwnerId
                && guser.GetRoles().Max(x => x.Position) <= role.Position)
            {
                return;
            }

            await role.DeleteAsync().ConfigureAwait(false);
            await ReplyConfirmAsync(Strings.Dr(ctx.Guild.Id, Format.Bold(role.Name))).ConfigureAwait(false);
        }


        /// <summary>
        ///     Toggles the hoist status of the specified role.
        /// </summary>
        /// <remarks>
        ///     This command allows administrators to toggle the hoist status of the specified role.
        ///     It requires the Manage Roles permission for the user.
        /// </remarks>
        /// <param name="role">The role to toggle the hoist status for.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageRoles)]
        [BotPerm(GuildPermission.ManageRoles)]
        public async Task RoleHoist(IRole role)
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
        ///     Displays the hexadecimal color value of the specified role.
        /// </summary>
        /// <remarks>
        ///     This command allows users to see the hexadecimal color value of the specified role.
        /// </remarks>
        /// <param name="role">The role to display the color for.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [Priority(1)]
        public async Task RoleColor([Remainder] IRole role)
        {
            await ctx.Channel.SendConfirmAsync(Strings.Rolecolor(ctx.Guild.Id), role.Colors.PrimaryColor.RawValue.ToString("x6"))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Changes the color of the specified role.
        /// </summary>
        /// <remarks>
        ///     This command allows administrators to change the color of the specified role.
        ///     It requires the Manage Roles permission for the user.
        /// </remarks>
        /// <param name="role">The role to change the color for.</param>
        /// <param name="color">The new color for the role.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageRoles)]
        [BotPerm(GuildPermission.ManageRoles)]
        [Priority(0)]
        public async Task RoleColor(IRole role, SKColor color)
        {
            try
            {
                await role.ModifyAsync(r => r.Color = new Color(color.Red, color.Green, color.Blue))
                    .ConfigureAwait(false);
                await ReplyConfirmAsync(Strings.Rc(ctx.Guild.Id, Format.Bold(role.Name))).ConfigureAwait(false);
            }
            catch (Exception)
            {
                await ReplyErrorAsync(Strings.RcPerms(ctx.Guild.Id)).ConfigureAwait(false);
            }
        }
    }
}