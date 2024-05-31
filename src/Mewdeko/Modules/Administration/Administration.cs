using System.Text.RegularExpressions;
using Discord.Commands;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Humanizer;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Common.TypeReaders.Models;
using Mewdeko.Modules.Administration.Services;
using Mewdeko.Services.Settings;
using Serilog;

namespace Mewdeko.Modules.Administration;

/// <summary>
/// Class for the Administration Module.
/// </summary>
/// <param name="serv">The interactivity service by Fergun.Interactive</param>
/// <param name="configService">The bot config service that uses yml from data/</param>
public partial class Administration(InteractiveService serv, BotConfigService configService)
    : MewdekoModuleBase<AdministrationService>
{
    /// <summary>
    /// Enumerates different variations of the term "channel".
    /// </summary>
    public enum Channel
    {
        /// <summary>
        /// Represents the term "channel".
        /// </summary>
        Channel,

        /// <summary>
        /// Represents the abbreviation "ch" for "channel".
        /// </summary>
        Ch,

        /// <summary>
        /// Represents the abbreviation "chnl" for "channel".
        /// </summary>
        Chnl,

        /// <summary>
        /// Represents the abbreviation "chan" for "channel".
        /// </summary>
        Chan
    }

    /// <summary>
    /// Enumerates different variations of the term "list".
    /// </summary>
    public enum List
    {
        /// <summary>
        /// Represents the term "list".
        /// </summary>
        List = 0,

        /// <summary>
        /// Represents the abbreviation "ls" for "list".
        /// </summary>
        Ls = 0
    }

    /// <summary>
    /// Enumerates different variations of the term "server".
    /// </summary>
    public enum Server
    {
        /// <summary>
        /// Represents the term "server".
        /// </summary>
        Server
    }

    /// <summary>
    /// Enumerates different states such as enable, disable, or inherit.
    /// </summary>
    public enum State
    {
        /// <summary>
        /// Represents the state of being enabled.
        /// </summary>
        Enable,

        /// <summary>
        /// Represents the state of being disabled.
        /// </summary>
        Disable,

        /// <summary>
        /// Represents the state of being inherited.
        /// </summary>
        Inherit
    }


    /// <summary>
    /// Bans multiple users by their avatar id, aka their avatar hash. Useful for userbots that are stupid.
    /// </summary>
    /// <param name="avatarHash">The avatar hash to search for</param>
    [Cmd, Aliases, UserPerm(GuildPermission.Administrator)]
    public async Task BanByHash(string avatarHash)
    {
        var users = await ctx.Guild.GetUsersAsync();
        var usersToBan = users?.Where(x => x.AvatarId == avatarHash);

        if (usersToBan is null || !usersToBan.Any())
        {
            await ctx.Channel.SendErrorAsync(GetText("ban_by_hash_none", avatarHash), Config);
            return;
        }

        if (await PromptUserConfirmAsync(
                GetText("ban_by_hash_confirm", usersToBan.Count(), avatarHash), ctx.User.Id))
        {
            await ctx.Channel.SendConfirmAsync(GetText("ban_by_hash_start", usersToBan.Count(), avatarHash));
            var failedUsers = 0;
            var bannedUsers = 0;
            foreach (var i in usersToBan)
            {
                try
                {
                    await ctx.Guild.AddBanAsync(i, 0, $"{ctx.User.Id} banning by hash {avatarHash}");
                    bannedUsers++;
                }
                catch
                {
                    failedUsers++;
                }
            }

            if (failedUsers == 0)
                await ctx.Channel.SendConfirmAsync(GetText("ban_by_hash_success", bannedUsers, avatarHash));
            else if (failedUsers == usersToBan.Count())
                await ctx.Channel.SendErrorAsync(GetText("ban_by_hash_fail_all", usersToBan.Count(), avatarHash),
                    Config);
            else
                await ctx.Channel.SendConfirmAsync(GetText("ban_by_hash_fail_some", bannedUsers, failedUsers,
                    avatarHash));
        }
    }

    /// <summary>
    /// Allows you to opt the entire guild out of stats tracking.
    /// </summary>
    /// <example>.guildstatsoptout</example>
    [Cmd, Aliases, UserPerm(GuildPermission.Administrator)]
    public async Task GuildStatsOptOut()
    {
        var optout = await Service.ToggleOptOut(ctx.Guild);
        if (!optout)
            await ctx.Channel.SendConfirmAsync(GetText("command_stats_enabled"));
        else
            await ctx.Channel.SendConfirmAsync(GetText("command_stats_disabled"));
    }

    /// <summary>
    /// Allows you to delete all stats data for the guild.
    /// </summary>
    /// <example>.deletestatsdata</example>
    [Cmd, Aliases, Ratelimit(3600), UserPerm(GuildPermission.Administrator)]
    public async Task DeleteGuildStatsData()
    {
        if (await PromptUserConfirmAsync(GetText("command_stats_delete_confirm"), ctx.User.Id))
        {
            if (await Service.DeleteStatsData(ctx.Guild))
                await ctx.Channel.SendErrorAsync(GetText("command_stats_delete_success"), Config);
            else
                await ctx.Channel.SendErrorAsync(GetText("command_stats_delete_fail"), Config);
        }
    }

    /// <summary>
    /// Lets you set the nickname for a mentioned user. If no user is mentioned it defaults to setting a nickname for the bot.
    /// </summary>
    /// <param name="gu">The target user.</param>
    /// <param name="newNick">The new nickname. Provide none to reset.</param>
    /// <example>.setnick @user newNick</example>
    [Cmd, BotPerm(GuildPermission.ManageNicknames), UserPerm(GuildPermission.ManageNicknames), Priority(1)]
    public async Task SetNick(IGuildUser gu, [Remainder] string? newNick = null)
    {
        var sg = (SocketGuild)Context.Guild;
        if (sg.OwnerId == gu.Id || gu.GetRoles().Max(r => r.Position) >= sg.CurrentUser.GetRoles().Max(r => r.Position))
        {
            await ReplyErrorLocalizedAsync("insuf_perms_i").ConfigureAwait(false);
            return;
        }

        await gu.ModifyAsync(u => u.Nickname = newNick).ConfigureAwait(false);

        await ReplyConfirmLocalizedAsync("user_nick", Format.Bold(gu.ToString()), Format.Bold(newNick) ?? "-")
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Allows you to ban users with a specific role.
    /// </summary>
    /// <param name="role">The role to ban users in</param>
    /// <param name="reason">The reason for the ban, optional</param>
    [Cmd, Aliases, UserPerm(GuildPermission.Administrator)]
    public async Task BanInRole(IRole role, [Remainder] string reason = null)
    {
        var users = await ctx.Guild.GetUsersAsync();
        var usersToBan = users.Where(x => x.RoleIds.Contains(role.Id)).ToList();
        if (usersToBan.Count == 0)
        {
            await ctx.Channel.SendErrorAsync(GetText("no_users_found"), Config).ConfigureAwait(false);
            return;
        }

        if (!await PromptUserConfirmAsync(GetText("ban_in_role_confirm", usersToBan.Count, role.Mention), ctx.User.Id))
        {
            await ctx.Channel.SendErrorAsync(GetText("ban_in_role_cancelled"), Config).ConfigureAwait(false);
            return;
        }

        var failedUsers = 0;
        foreach (var i in usersToBan)
        {
            try
            {
                await ctx.Guild.AddBanAsync(i, 0, reason ?? $"{ctx.User} | {ctx.User.Id} used baninrole")
                    .ConfigureAwait(false);
            }
            catch
            {
                failedUsers++;
            }
        }

        if (failedUsers == 0)
            await ctx.Channel.SendConfirmAsync(GetText("ban_in_role_done", usersToBan.Count, role.Mention))
                .ConfigureAwait(false);
        else if (failedUsers == usersToBan.Count)
            await ctx.Channel.SendErrorAsync(GetText("ban_in_role_fail", users.Count, role.Mention), Config)
                .ConfigureAwait(false);
        else
            await ctx.Channel
                .SendConfirmAsync(GetText("ban_in_role_failed_some", usersToBan.Count - failedUsers, role.Mention,
                    failedUsers)).ConfigureAwait(false);
    }

    /// <summary>
    /// Overload for setting the bot's nickname.
    /// </summary>
    /// <param name="newNick">The new nickname you want to set.</param>
    /// <example>.setnick newNick</example>
    [Cmd, Aliases, UserPerm(GuildPermission.ManageNicknames), BotPerm(GuildPermission.ChangeNickname), Priority(0)]
    public async Task SetNick([Remainder] string? newNick = null)
    {
        if (string.IsNullOrWhiteSpace(newNick))
            return;
        var curUser = await ctx.Guild.GetCurrentUserAsync().ConfigureAwait(false);
        await curUser.ModifyAsync(u => u.Nickname = newNick).ConfigureAwait(false);

        await ReplyConfirmLocalizedAsync("bot_nick", Format.Bold(newNick) ?? "-").ConfigureAwait(false);
    }

    /// <summary>
    /// Allows you to ban users with a specific name. This command will show a preview of the users that will be banned. Takes a regex pattern as well.
    /// </summary>
    /// <param name="name">The name or regex pattern you want to use.</param>
    /// <example>.nameban name</example>
    /// <example>.nameban ^[a-z]{3,16}$</example>
    [Cmd, Aliases, UserPerm(GuildPermission.Administrator), BotPerm(GuildPermission.BanMembers)]
    public async Task NameBan([Remainder] string name)
    {
        var regex = new Regex(name, RegexOptions.Compiled, matchTimeout: TimeSpan.FromMilliseconds(200));
        var users = await ctx.Guild.GetUsersAsync();
        users = users.Where(x => regex.IsMatch(x.Username.ToLower())).ToList();
        if (!users.Any())
        {
            await ctx.Channel.SendErrorAsync($"{configService.Data.ErrorEmote} {GetText("no_users_found_nameban")}",
                Config);
            return;
        }

        await ctx.Channel.SendConfirmAsync(GetText("nameban_message_delete"));
        var deleteString = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id);
        if (deleteString == null)
        {
            await ctx.Channel.SendErrorAsync($"{configService.Data.ErrorEmote} {GetText("nameban_cancelled")}", Config);
            return;
        }

        if (!int.TryParse(deleteString, out var _))
        {
            await ctx.Channel.SendErrorAsync($"{configService.Data.ErrorEmote} {GetText("invalid_input_number")}",
                Config);
            return;
        }

        var deleteCount = int.Parse(deleteString);
        var components = new ComponentBuilder()
            .WithButton(GetText("preview"), "previewbans")
            .WithButton(GetText("execute"), "executeorder66", ButtonStyle.Success)
            .WithButton(GetText("cancel"), "cancel", ButtonStyle.Danger);
        var eb = new EmbedBuilder()
            .WithDescription(GetText("preview_or_execute"))
            .WithOkColor();
        var msg = await ctx.Channel.SendMessageAsync(embed: eb.Build(), components: components.Build());
        var input = await GetButtonInputAsync(ctx.Channel.Id, msg.Id, ctx.User.Id);
        switch (input)
        {
            case "cancel":
                await ctx.Channel.SendErrorAsync($"{configService.Data.ErrorEmote} {GetText("nameban_cancelled")}",
                    Config);
                break;
            case "previewbans":
                var paginator = new LazyPaginatorBuilder()
                    .AddUser(ctx.User)
                    .WithPageFactory(PageFactory)
                    .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                    .WithMaxPageIndex(users.Count / 20)
                    .WithDefaultCanceledPage()
                    .WithDefaultEmotes()
                    .WithActionOnCancellation(ActionOnStop.DeleteMessage).Build();
                await serv.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60))
                    .ConfigureAwait(false);

                break;

                async Task<PageBuilder> PageFactory(int page)
                {
                    await Task.CompletedTask.ConfigureAwait(false);
                    return new PageBuilder().WithTitle(GetText("nameban_preview_count", users.Count, name.ToLower()))
                        .WithDescription(string.Join("\n", users.Skip(page * 20).Take(20)));
                }
            case "executeorder66":
                if (await PromptUserConfirmAsync(GetText("nameban_confirm", users.Count), ctx.User.Id))
                {
                    var failedUsers = 0;
                    await SuccessLocalizedAsync("nameban_processing", users.Count);
                    foreach (var i in users)
                    {
                        try
                        {
                            await ctx.Guild.AddBanAsync(i, deleteCount, options: new RequestOptions
                            {
                                AuditLogReason = GetText("mass_ban_requested_by", ctx.User)
                            });
                        }
                        catch
                        {
                            failedUsers++;
                        }
                    }

                    await ctx.Channel.SendConfirmAsync(
                        $"{configService.Data.SuccessEmote} executed order 66 on {users.Count - failedUsers} users. Failed to ban {failedUsers} users (Probably due to bad role heirarchy).");
                }

                break;
        }
    }

    /// <summary>
    /// Allows you to ban users that have been in the server for a certain amount of time.
    /// </summary>
    /// <param name="time">The amount of time. Formatted as {0}mo{1}d{2}h{3}m{4}s</param>
    /// <param name="option">Allows you to specify -accage to check account age rather than server age, or -p to preview the users to ban.</param>
    /// <param name="time1">Allows you to specify a time range.</param>
    /// <example>.banunder 1mo</example>
    /// <example>.banunder 1mo -accage 1d</example>
    /// <example>.banunder 1mo -p</example>
    [Cmd, Aliases, UserPerm(GuildPermission.Administrator), BotPerm(GuildPermission.BanMembers)]
    public async Task BanUnder(StoopidTime time, string? option = null, StoopidTime? time1 = null)
    {
        try
        {
            await ctx.Guild.DownloadUsersAsync().ConfigureAwait(false);
            IEnumerable<IUser> users;
            if (option is not null && option.ToLower() == "-accage" && time1 is not null)
            {
                users = ((SocketGuild)ctx.Guild).Users.Where(c =>
                    c.JoinedAt != null
                    && DateTimeOffset.Now.Subtract(c.JoinedAt.Value).TotalSeconds <= time.Time.TotalSeconds
                    && DateTimeOffset.Now.Subtract(c.CreatedAt).TotalSeconds <= time1.Time.TotalSeconds);
            }
            else
            {
                users = ((SocketGuild)ctx.Guild).Users.Where(c =>
                    c.JoinedAt != null && DateTimeOffset.Now.Subtract(c.JoinedAt.Value).TotalSeconds <=
                    time.Time.TotalSeconds);
            }

            if (!users.Any())
            {
                await ctx.Channel.SendErrorAsync(GetText("banunder_no_users"), Config).ConfigureAwait(false);
                return;
            }

            if (option is not null && option.ToLower() == "-p")
            {
                var paginator = new LazyPaginatorBuilder().AddUser(ctx.User).WithPageFactory(PageFactory)
                    .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                    .WithMaxPageIndex(users.Count() / 20).WithDefaultCanceledPage().WithDefaultEmotes()
                    .WithActionOnCancellation(ActionOnStop.DeleteMessage).Build();
                await serv.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60))
                    .ConfigureAwait(false);

                async Task<PageBuilder> PageFactory(int page)
                {
                    await Task.CompletedTask.ConfigureAwait(false);
                    return new PageBuilder()
                        .WithTitle(GetText("banunder_preview", users.Count(),
                            time.Time.Humanize(maxUnit: TimeUnit.Year)))
                        .WithDescription(string.Join("\n", users.Skip(page * 20).Take(20)));
                }
            }

            var banned = 0;
            var errored = 0;
            var msg = await ctx.Channel.SendMessageAsync(embed: new EmbedBuilder().WithErrorColor()
                .WithDescription(GetText("banunder_confirm", users.Count()))
                .Build());
            var text = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id);
            await msg.DeleteAsync();
            if (!text.ToLower().Contains("yes"))
                return;
            var message = await ConfirmLocalizedAsync("banunder_banning").ConfigureAwait(false);
            foreach (var i in users)
            {
                try
                {
                    await ctx.Guild.AddBanAsync(i, options: new RequestOptions
                    {
                        AuditLogReason = GetText("banunder_starting", ctx.User)
                    }).ConfigureAwait(false);
                    banned++;
                }
                catch
                {
                    errored++;
                }
            }

            var eb = new EmbedBuilder()
                .WithDescription(GetText("banunder_kicked", banned, errored))
                .WithOkColor();
            await message.ModifyAsync(x => x.Embed = eb.Build()).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Error(ex.ToString());
        }
    }

    /// <summary>
    /// Kicks users who have been in the server for less than a specified time.
    /// </summary>
    /// <param name="time">Time duration in a custom format</param>
    /// <param name="option">Optional parameter to preview users to be kicked</param>
    /// <remarks>
    /// This command requires the caller to have GuildPermission.Administrator and the bot to have GuildPermission.KickMembers.
    /// </remarks>
    /// <example>.kickunder 1mo</example>
    /// <example>.kickunder 1mo -p</example>
    [Cmd, Aliases, UserPerm(GuildPermission.Administrator), BotPerm(GuildPermission.KickMembers)]
    public async Task KickUnder(StoopidTime time, string? option = null)
    {
        await ctx.Guild.DownloadUsersAsync().ConfigureAwait(false);
        var users = ((SocketGuild)ctx.Guild).Users.Where(c =>
            c.JoinedAt != null && DateTimeOffset.Now.Subtract(c.JoinedAt.Value).TotalSeconds <= time.Time.TotalSeconds);
        var guildUsers = users as SocketGuildUser[] ?? users.ToArray();
        if (guildUsers.Length == 0)
        {
            await ErrorLocalizedAsync("kickunder_no_users").ConfigureAwait(false);
            return;
        }

        if (option is not null && option.ToLower() == "-p")
        {
            var paginator = new LazyPaginatorBuilder()
                .AddUser(ctx.User)
                .WithPageFactory(PageFactory)
                .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                .WithMaxPageIndex(guildUsers.Length / 20)
                .WithDefaultCanceledPage()
                .WithDefaultEmotes()
                .WithActionOnCancellation(ActionOnStop.DeleteMessage)
                .Build();
            await serv.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60)).ConfigureAwait(false);

            async Task<PageBuilder> PageFactory(int page)
            {
                await Task.CompletedTask.ConfigureAwait(false);
                return new PageBuilder()
                    .WithTitle(GetText("kickunder_preview", guildUsers.Length,
                        time.Time.Humanize(maxUnit: TimeUnit.Year)))
                    .WithDescription(string.Join("\n", guildUsers.Skip(page * 20).Take(20)));
            }
        }

        var banned = 0;
        var errored = 0;
        var msg = await ctx.Channel.SendMessageAsync(embed: new EmbedBuilder().WithErrorColor()
            .WithDescription(GetText("kickunder_confirm", users.Count()))
            .Build());
        var text = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id);
        await msg.DeleteAsync();
        if (!text.ToLower().Contains("yes"))
            return;
        var message = await ConfirmLocalizedAsync("kickunder_kicking").ConfigureAwait(false);
        foreach (var i in guildUsers)
        {
            try
            {
                await i.KickAsync(GetText("kickunder_starting", ctx.User)).ConfigureAwait(false);
                banned++;
            }
            catch
            {
                errored++;
            }
        }

        var eb = new EmbedBuilder()
            .WithDescription(GetText("kickunder_kicked", banned, errored))
            .WithOkColor();
        await message.ModifyAsync(x => x.Embed = eb.Build()).ConfigureAwait(false);
    }


    /// <summary>
    /// Prunes members from the server based on their activity or inactivity.
    /// </summary>
    /// <param name="time">Time duration in a custom format</param>
    /// <param name="e">Optional parameter indicating whether to include users with specific roles</param>
    /// <remarks>
    /// This command requires the caller to have GuildPermission.Administrator and the bot to have GuildPermission.ManageGuild.
    /// </remarks>
    /// <example>.prunemembers 30d</example>
    /// <example>.prunemembers 30d yes</example>
    [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.Administrator),
     BotPerm(GuildPermission.ManageGuild)]
    public async Task PruneMembers(StoopidTime time, string e = "no")
    {
        try
        {
            await ConfirmLocalizedAsync("command_expected_latency_server_size");
            if (e == "no")
            {
                var toprune = await ctx.Guild.PruneUsersAsync(time.Time.Days, true);
                if (toprune == 0)
                {
                    await ErrorLocalizedAsync("prune_no_members_upsell").ConfigureAwait(false);
                    return;
                }

                var eb = new EmbedBuilder
                {
                    Description = $"Are you sure you want to prune {toprune} Members?", Color = Mewdeko.OkColor
                };
                if (!await PromptUserConfirmAsync(eb, ctx.User.Id).ConfigureAwait(false))
                {
                    await ConfirmLocalizedAsync("prune_canceled_member_upsell").ConfigureAwait(false);
                }
                else
                {
                    var msg = await ConfirmLocalizedAsync("pruning_members", toprune).ConfigureAwait(false);
                    await ctx.Guild.PruneUsersAsync(time.Time.Days).ConfigureAwait(false);
                    var ebi = new EmbedBuilder
                    {
                        Description = GetText("pruned_members", toprune), Color = Mewdeko.OkColor
                    };
                    await msg.ModifyAsync(x => x.Embed = ebi.Build()).ConfigureAwait(false);
                }
            }
            else
            {
                ctx.Guild.GetRole(await Service.GetMemberRole(ctx.Guild.Id));
                var toprune = await ctx.Guild.PruneUsersAsync(time.Time.Days, true,
                    includeRoleIds: new[]
                    {
                        await Service.GetMemberRole(ctx.Guild.Id)
                    }).ConfigureAwait(false);
                if (toprune == 0)
                {
                    await ErrorLocalizedAsync("prune_no_members").ConfigureAwait(false);
                    return;
                }

                var eb = new EmbedBuilder
                {
                    Description = GetText("prune_confirm", toprune), Color = Mewdeko.OkColor
                };
                if (!await PromptUserConfirmAsync(eb, ctx.User.Id).ConfigureAwait(false))
                {
                    await ConfirmLocalizedAsync("prune_canceled").ConfigureAwait(false);
                }
                else
                {
                    var msg = await ConfirmLocalizedAsync("pruning_members", toprune).ConfigureAwait(false);
                    await ctx.Guild.PruneUsersAsync(time.Time.Days,
                        includeRoleIds: new[]
                        {
                            await Service.GetMemberRole(ctx.Guild.Id)
                        });
                    var ebi = new EmbedBuilder
                    {
                        Description = GetText("pruned_members", toprune), Color = Mewdeko.OkColor
                    };
                    await msg.ModifyAsync(x => x.Embed = ebi.Build()).ConfigureAwait(false);
                }
            }
        }
        catch (Exception exception)
        {
            Log.Error("Error in prunemembers: \n{0}", exception);
        }
    }


    /// <summary>
    /// Sets the member role for the server. Currently unused.
    /// </summary>
    /// <param name="role">The role that members will have.</param>
    /// <remarks>
    /// This command requires the caller to have GuildPermission.Administrator.
    /// </remarks>
    /// <example>.memberrole @Member</example>
    [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.Administrator)]
    public async Task MemberRole(IRole? role)
    {
        var rol = await Service.GetMemberRole(ctx.Guild.Id);
        if (rol is 0 && role != null)
        {
            await Service.MemberRoleSet(ctx.Guild, role.Id).ConfigureAwait(false);
            await ConfirmLocalizedAsync("member_role_set", role.Id).ConfigureAwait(false);
        }

        if (rol != 0 && role != null && rol == role.Id)
        {
            await ErrorLocalizedAsync("member_role_already_set").ConfigureAwait(false);
            return;
        }

        if (rol is 0 && role == null)
        {
            await ErrorLocalizedAsync("member_role_missing").ConfigureAwait(false);
            return;
        }

        if (rol != 0 && role is null)
        {
            var r = ctx.Guild.GetRole(rol);
            await ConfirmLocalizedAsync("member_role_current", r.Id).ConfigureAwait(false);
            return;
        }

        if (role != null && rol is not 0)
        {
            var oldrole = ctx.Guild.GetRole(rol);
            await Service.MemberRoleSet(ctx.Guild, role.Id).ConfigureAwait(false);
            await ConfirmLocalizedAsync("member_role_updated", oldrole.Id, role.Id).ConfigureAwait(false);
        }
    }


    /// <summary>
    /// Sets or updates the role assigned to staff members.
    /// </summary>
    /// <param name="role">The role to be assigned to staff members</param>
    /// <remarks>
    /// This command requires the caller to have GuildPermission.Administrator.
    /// </remarks>
    /// <example>.staffrole @Staff</example>
    [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.Administrator)]
    public async Task StaffRole([Remainder] IRole? role = null)
    {
        var rol = await Service.GetStaffRole(ctx.Guild.Id);
        if (rol is 0 && role != null)
        {
            await Service.StaffRoleSet(ctx.Guild, role.Id).ConfigureAwait(false);
            await ConfirmLocalizedAsync("staff_role_set", role.Id).ConfigureAwait(false);
        }

        if (rol != 0 && role != null && rol == role.Id)
        {
            await ErrorLocalizedAsync("staff_role_already_set").ConfigureAwait(false);
            return;
        }

        if (rol is 0 && role == null)
        {
            await ErrorLocalizedAsync("staff_role_missing").ConfigureAwait(false);
            return;
        }

        if (rol != 0 && role is null)
        {
            var r = ctx.Guild.GetRole(rol);
            await ConfirmLocalizedAsync("staff_role_current", r.Id).ConfigureAwait(false);
            return;
        }

        if (role != null && rol is not 0)
        {
            var oldrole = ctx.Guild.GetRole(rol);
            await Service.StaffRoleSet(ctx.Guild, role.Id).ConfigureAwait(false);
            await ConfirmLocalizedAsync("staff_role_updated", oldrole.Id, role.Id).ConfigureAwait(false);
        }
    }


    /// <summary>
    /// Disables the role assigned to staff members.
    /// </summary>
    /// <remarks>
    /// This command requires the caller to have GuildPermission.Administrator.
    /// </remarks>
    /// <example>.staffroledisable</example>
    [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.Administrator)]
    public async Task StaffRoleDisable()
    {
        var r = await Service.GetStaffRole(ctx.Guild.Id);
        if (r == 0)
        {
            await ctx.Channel.SendErrorAsync(GetText("staff_role_missing"), Config).ConfigureAwait(false);
        }
        else
        {
            await Service.StaffRoleSet(ctx.Guild, 0).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync(GetText("staff_role_disabled")).ConfigureAwait(false);
        }
    }


    /// <summary>
    /// Displays the status of deleting messages on command execution.
    /// </summary>
    /// <remarks>
    /// This command requires the caller to have GuildPermission.Administrator and the bot to have GuildPermission.ManageMessages.
    /// </remarks>
    /// <example>.delmsgoncmd</example>
    [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.Administrator),
     BotPerm(GuildPermission.ManageMessages), Priority(2)]
    public async Task Delmsgoncmd(List _)
    {
        var guild = (SocketGuild)ctx.Guild;
        var (enabled, channels) = await Service.GetDelMsgOnCmdData(ctx.Guild.Id);

        var embed = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(GetText("server_delmsgoncmd"))
            .WithDescription(enabled ? "✅" : "❌");

        var str = string.Join("\n", channels
            .Select(x =>
            {
                var ch = guild.GetChannel(x.ChannelId)?.ToString()
                         ?? x.ChannelId.ToString();
                var prefix = x.State ? "✅ " : "❌ ";
                return prefix + ch;
            }));

        if (string.IsNullOrWhiteSpace(str))
            str = "-";

        embed.AddField(GetText("channel_delmsgoncmd"), str);

        await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
    }


    /// <summary>
    /// Toggles the deletion of messages on command execution for the server.
    /// </summary>
    /// <param name="_">Unused parameter</param>
    /// <remarks>
    /// This command requires the caller to have GuildPermission.Administrator and the bot to have GuildPermission.ManageMessages.
    /// </remarks>
    /// <example>.delmsgoncmd</example>
    [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.Administrator),
     BotPerm(GuildPermission.ManageMessages), Priority(1)]
    public async Task Delmsgoncmd(Server _ = Server.Server)
    {
        if (await Service.ToggleDeleteMessageOnCommand(ctx.Guild.Id))
        {
            await ReplyConfirmLocalizedAsync("delmsg_on").ConfigureAwait(false);
        }
        else
        {
            await ReplyConfirmLocalizedAsync("delmsg_off").ConfigureAwait(false);
        }
    }


    /// <summary>
    /// Sets the state of deleting messages on command execution for a specific channel.
    /// </summary>
    /// <param name="_">Unused parameter</param>
    /// <param name="s">The state to set for deleting messages on command execution</param>
    /// <param name="ch">The channel where the state should be applied</param>
    /// <remarks>
    /// This command requires the caller to have GuildPermission.Administrator and the bot to have GuildPermission.ManageMessages.
    /// </remarks>
    /// <example>.delmsgoncmd enable #channel</example>
    /// <example>.delmsgoncmd disable #channel</example>
    [Cmd, RequireContext(ContextType.Guild), UserPerm(GuildPermission.Administrator),
     BotPerm(GuildPermission.ManageMessages), Priority(0)]
    public Task Delmsgoncmd(Channel _, State s, ITextChannel ch) => Delmsgoncmd(_, s, ch.Id);


    /// <summary>
    /// Sets the state of deleting messages on command execution for a specific channel.
    /// </summary>
    /// <param name="_">Unused parameter</param>
    /// <param name="s">The state to set for deleting messages on command execution</param>
    /// <param name="chId">Optional channel ID where the state should be applied</param>
    /// <remarks>
    /// This command requires the caller to have GuildPermission.Administrator and the bot to have GuildPermission.ManageMessages.
    /// </remarks>
    /// <example>.delmsgoncmd enable #channel</example>
    /// <example>.delmsgoncmd disable #channel</example>
    [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.Administrator),
     BotPerm(GuildPermission.ManageMessages), Priority(1)]
    public async Task Delmsgoncmd(Channel _, State s, ulong? chId = null)
    {
        var actualChId = chId ?? ctx.Channel.Id;
        await Service.SetDelMsgOnCmdState(ctx.Guild.Id, actualChId, s).ConfigureAwait(false);

        switch (s)
        {
            case State.Disable:
                await ReplyConfirmLocalizedAsync("delmsg_channel_off").ConfigureAwait(false);
                break;
            case State.Enable:
                await ReplyConfirmLocalizedAsync("delmsg_channel_on").ConfigureAwait(false);
                break;
            default:
                await ReplyConfirmLocalizedAsync("delmsg_channel_inherit").ConfigureAwait(false);
                break;
        }
    }


    /// <summary>
    /// Deafens specified users in the guild.
    /// </summary>
    /// <param name="users">The users to deafen</param>
    /// <remarks>
    /// This command requires the caller to have GuildPermission.DeafenMembers and the bot to have GuildPermission.DeafenMembers.
    /// </remarks>
    /// <example>.deafen @User1 @User2</example>
    [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.DeafenMembers),
     BotPerm(GuildPermission.DeafenMembers)]
    public async Task Deafen(params IGuildUser[] users)
    {
        await AdministrationService.DeafenUsers(true, users).ConfigureAwait(false);
        await ReplyConfirmLocalizedAsync("deafen").ConfigureAwait(false);
    }

    /// <summary>
    /// Undeafens specified users in the guild.
    /// </summary>
    /// <param name="users">The users to undeafen</param>
    /// <remarks>
    /// This command requires the caller to have GuildPermission.DeafenMembers and the bot to have GuildPermission.DeafenMembers.
    /// </remarks>
    /// <example>.undeafen @User1 @User2</example>
    [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.DeafenMembers),
     BotPerm(GuildPermission.DeafenMembers)]
    public async Task UnDeafen(params IGuildUser[] users)
    {
        await AdministrationService.DeafenUsers(false, users).ConfigureAwait(false);
        await ReplyConfirmLocalizedAsync("undeafen").ConfigureAwait(false);
    }


    /// <summary>
    /// Deletes the specified voice channel.
    /// </summary>
    /// <param name="voiceChannel">The voice channel to delete</param>
    /// <remarks>
    /// This command requires the caller to have GuildPermission.ManageChannels and the bot to have GuildPermission.ManageChannels.
    /// </remarks>
    /// <example>.delvoich VoiceChannelName</example>
    [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.ManageChannels),
     BotPerm(GuildPermission.ManageChannels)]
    public async Task DelVoiChanl([Remainder] IVoiceChannel voiceChannel)
    {
        await voiceChannel.DeleteAsync().ConfigureAwait(false);
        await ReplyConfirmLocalizedAsync("delvoich", Format.Bold(voiceChannel.Name)).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a new voice channel with the specified name.
    /// </summary>
    /// <param name="channelName">The name of the voice channel to create</param>
    /// <remarks>
    /// This command requires the caller to have GuildPermission.ManageChannels and the bot to have GuildPermission.ManageChannels.
    /// </remarks>
    /// <example>.creatvoich VoiceChannelName</example>
    [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.ManageChannels),
     BotPerm(GuildPermission.ManageChannels)]
    public async Task CreatVoiChanl([Remainder] string channelName)
    {
        var ch = await ctx.Guild.CreateVoiceChannelAsync(channelName).ConfigureAwait(false);
        await ReplyConfirmLocalizedAsync("createvoich", Format.Bold(ch.Name)).ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes the specified text channel.
    /// </summary>
    /// <param name="toDelete">The text channel to delete</param>
    /// <remarks>
    /// This command requires the caller to have GuildPermission.ManageChannels and the bot to have GuildPermission.ManageChannels.
    /// </remarks>
    /// <example>.deltxtchan TextChannelName</example>
    [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.ManageChannels),
     BotPerm(GuildPermission.ManageChannels)]
    public async Task DelTxtChanl([Remainder] ITextChannel toDelete)
    {
        await toDelete.DeleteAsync().ConfigureAwait(false);
        await ReplyConfirmLocalizedAsync("deltextchan", Format.Bold(toDelete.Name)).ConfigureAwait(false);
    }


    /// <summary>
    /// Creates a new text channel with the specified name.
    /// </summary>
    /// <param name="channelName">The name of the text channel to create</param>
    /// <remarks>
    /// This command requires the caller to have GuildPermission.ManageChannels and the bot to have GuildPermission.ManageChannels.
    /// </remarks>
    /// <example>.createtxtchan TextChannelName</example>
    [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.ManageChannels),
     BotPerm(GuildPermission.ManageChannels)]
    public async Task CreaTxtChanl([Remainder] string channelName)
    {
        var txtCh = await ctx.Guild.CreateTextChannelAsync(channelName).ConfigureAwait(false);
        await ReplyConfirmLocalizedAsync("createtextchan", Format.Bold(txtCh.Name)).ConfigureAwait(false);
    }

    /// <summary>
    /// Sets the topic of the current text channel.
    /// </summary>
    /// <param name="topic">The topic to set for the text channel</param>
    /// <remarks>
    /// This command requires the caller to have GuildPermission.ManageChannels and the bot to have GuildPermission.ManageChannels.
    /// </remarks>
    /// <example>.settopic NewTopic</example>
    [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.ManageChannels),
     BotPerm(GuildPermission.ManageChannels)]
    public async Task SetTopic([Remainder] string? topic = null)
    {
        var channel = (ITextChannel)ctx.Channel;
        topic ??= "";
        await channel.ModifyAsync(c => c.Topic = topic).ConfigureAwait(false);
        await ReplyConfirmLocalizedAsync("set_topic").ConfigureAwait(false);
    }

    /// <summary>
    /// Sets the name of the current text channel.
    /// </summary>
    /// <param name="name">The name to set for the text channel</param>
    /// <remarks>
    /// This command requires the caller to have GuildPermission.ManageChannels and the bot to have GuildPermission.ManageChannels.
    /// </remarks>
    /// <example>.setchannelname NewChannelName</example>
    [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.ManageChannels),
     BotPerm(GuildPermission.ManageChannels)]
    public async Task SetChanlName([Remainder] string name)
    {
        var channel = (ITextChannel)ctx.Channel;
        await channel.ModifyAsync(c => c.Name = name).ConfigureAwait(false);
        await ReplyConfirmLocalizedAsync("set_channel_name").ConfigureAwait(false);
    }

    /// <summary>
    /// Toggles the NSFW setting of the current text channel.
    /// </summary>
    /// <remarks>
    /// This command requires the caller to have GuildPermission.ManageChannels and the bot to have GuildPermission.ManageChannels.
    /// </remarks>
    /// <example>.nsfwtoggle</example>
    [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.ManageChannels),
     BotPerm(GuildPermission.ManageChannels)]
    public async Task NsfwToggle()
    {
        var channel = (ITextChannel)ctx.Channel;
        var isEnabled = channel.IsNsfw;

        await channel.ModifyAsync(c => c.IsNsfw = !isEnabled).ConfigureAwait(false);

        if (isEnabled)
            await ReplyConfirmLocalizedAsync("nsfw_set_false").ConfigureAwait(false);
        else
            await ReplyConfirmLocalizedAsync("nsfw_set_true").ConfigureAwait(false);
    }

    /// <summary>
    /// Edits a message in the specified text channel.
    /// </summary>
    /// <param name="channel">The text channel where the message is located</param>
    /// <param name="messageId">The ID of the message to edit</param>
    /// <param name="text">The new text for the message</param>
    /// <remarks>
    /// This command requires the caller to have ChannelPermission.ManageMessages.
    /// </remarks>
    /// <example>.edit 123456789012345678 NewMessageText</example>
    [Cmd, Aliases, RequireContext(ContextType.Guild), Priority(1)]
    public async Task Edit(ITextChannel channel, ulong messageId, [Remainder] string? text)
    {
        var userPerms = ((SocketGuildUser)ctx.User).GetPermissions(channel);
        var botPerms = ((SocketGuild)ctx.Guild).CurrentUser.GetPermissions(channel);
        if (!userPerms.Has(ChannelPermission.ManageMessages))
        {
            await ReplyErrorLocalizedAsync("insuf_perms_u").ConfigureAwait(false);
            return;
        }

        if (!botPerms.Has(ChannelPermission.ViewChannel))
        {
            await ReplyErrorLocalizedAsync("insuf_perms_i").ConfigureAwait(false);
            return;
        }

        await AdministrationService.EditMessage(ctx, channel, messageId, text).ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes a message by its ID in the current text channel.
    /// </summary>
    /// <param name="messageId">The ID of the message to delete</param>
    /// <param name="time">Optional time duration after which the message should be deleted</param>
    /// <remarks>
    /// This command requires the caller to have ChannelPermission.ManageMessages and the bot to have ChannelPermission.ManageMessages.
    /// </remarks>
    /// <example>.delete 123456789012345678</example>
    [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(ChannelPermission.ManageMessages),
     BotPerm(ChannelPermission.ManageMessages)]
    public Task Delete(ulong messageId, StoopidTime? time = null) => Delete((ITextChannel)ctx.Channel, messageId, time);

    /// <summary>
    /// Deletes a message by its ID in the specified text channel.
    /// </summary>
    /// <param name="channel">The text channel where the message is located</param>
    /// <param name="messageId">The ID of the message to delete</param>
    /// <param name="time">Optional time duration after which the message should be deleted</param>
    /// <remarks>
    /// This command requires the caller to have ChannelPermission.ManageMessages.
    /// </remarks>
    /// <example>.delete #channel 123456789012345678</example>
    [Cmd, Aliases, RequireContext(ContextType.Guild)]
    public Task Delete(ITextChannel channel, ulong messageId, StoopidTime? time = null) =>
        InternalMessageAction(channel, messageId, time);

    /// <summary>
    /// Internal handler for message deletion.
    /// </summary>
    /// <param name="channel">The target channel</param>
    /// <param name="messageId">The target message ID</param>
    /// <param name="time">Time to delete, if any.</param>
    private async Task InternalMessageAction(ITextChannel channel, ulong messageId, StoopidTime? time)
    {
        var userPerms = ((SocketGuildUser)ctx.User).GetPermissions(channel);
        var botPerms = ((SocketGuild)ctx.Guild).CurrentUser.GetPermissions(channel);
        if (!userPerms.Has(ChannelPermission.ManageMessages))
        {
            await ReplyErrorLocalizedAsync("insuf_perms_u").ConfigureAwait(false);
            return;
        }

        if (!botPerms.Has(ChannelPermission.ManageMessages))
        {
            await ReplyErrorLocalizedAsync("insuf_perms_i").ConfigureAwait(false);
            return;
        }

        var msg = await channel.GetMessageAsync(messageId).ConfigureAwait(false);
        if (msg == null)
        {
            await ReplyErrorLocalizedAsync("msg_not_found").ConfigureAwait(false);
            return;
        }

        if (time == null)
        {
            await msg.DeleteAsync().ConfigureAwait(false);
        }
        else if (time.Time <= TimeSpan.FromDays(7))
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(time.Time).ConfigureAwait(false);
                await msg.DeleteAsync().ConfigureAwait(false);
            });
        }
        else
        {
            await ReplyErrorLocalizedAsync("time_too_long").ConfigureAwait(false);
            return;
        }

        await ctx.OkAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Renames the specified channel.
    /// </summary>
    /// <param name="channel">The channel to rename</param>
    /// <param name="name">The new name for the channel</param>
    /// <remarks>
    /// This command requires the caller to have GuildPermission.ManageChannels.
    /// </remarks>
    /// <example>.renamechannel #channel NewChannelName</example>
    [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.ManageChannels)]
    public async Task RenameChannel(IGuildChannel channel, [Remainder] string name)
    {
        await channel.ModifyAsync(x => x.Name = name).ConfigureAwait(false);
        await ConfirmLocalizedAsync("channel_renamed").ConfigureAwait(false);
    }
}