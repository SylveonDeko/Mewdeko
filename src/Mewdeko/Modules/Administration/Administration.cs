using System.Net.Http;
using System.Text.RegularExpressions;
using Discord.Commands;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Humanizer;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Common.TypeReaders.Models;
using Mewdeko.Modules.Administration.Services;

namespace Mewdeko.Modules.Administration;

/// <summary>
///     Class for the Administration Module.
/// </summary>
/// <param name="serv">The interactivity service by Fergun.Interactive</param>
/// <param name="logger">The logger instance for structured logging.</param>
public partial class Administration(InteractiveService serv, ILogger<Administration> logger)
    : MewdekoModuleBase<AdministrationService>
{
    /// <summary>
    ///     Enumerates different variations of the term "channel".
    /// </summary>
    public enum Channel
    {
        /// <summary>
        ///     Represents the term "channel".
        /// </summary>
        Channel,

        /// <summary>
        ///     Represents the abbreviation "ch" for "channel".
        /// </summary>
        Ch,

        /// <summary>
        ///     Represents the abbreviation "chnl" for "channel".
        /// </summary>
        Chnl,

        /// <summary>
        ///     Represents the abbreviation "chan" for "channel".
        /// </summary>
        Chan
    }

    /// <summary>
    ///     Enumerates different variations of the term "list".
    /// </summary>
    public enum List
    {
        /// <summary>
        ///     Represents the term "list".
        /// </summary>
        List = 0,

        /// <summary>
        ///     Represents the abbreviation "ls" for "list".
        /// </summary>
        Ls = 0
    }

    /// <summary>
    ///     Enumerates different variations of the term "server".
    /// </summary>
    public enum Server
    {
        /// <summary>
        ///     Represents the term "server".
        /// </summary>
        Server
    }

    /// <summary>
    ///     Enumerates different states such as enable, disable, or inherit.
    /// </summary>
    public enum State
    {
        /// <summary>
        ///     Represents the state of being enabled.
        /// </summary>
        Enable,

        /// <summary>
        ///     Represents the state of being disabled.
        /// </summary>
        Disable,

        /// <summary>
        ///     Represents the state of being inherited.
        /// </summary>
        Inherit
    }

    // Cache for compiled regex patterns to avoid repeated compilation
    private static readonly ConcurrentDictionary<string, Regex> RegexCache = new();

    /// <summary>
    ///     Gets a cached compiled regex pattern or creates and caches a new one.
    /// </summary>
    /// <param name="pattern">The regex pattern to compile</param>
    /// <returns>A compiled regex with optimized options</returns>
    private static Regex GetCachedRegex(string pattern)
    {
        return RegexCache.GetOrAdd(pattern, static p =>
        {
            try
            {
                return new Regex(p, RegexOptions.Compiled, TimeSpan.FromMilliseconds(200));
            }
            catch (ArgumentException)
            {
                // If pattern is invalid, return a regex that never matches
                return new Regex("(?!.*)", RegexOptions.Compiled);
            }
        });
    }


    /// <summary>
    ///     Bans multiple users by their avatar id, aka their avatar hash. Useful for userbots that are stupid.
    /// </summary>
    /// <param name="avatarHash">The avatar hash to search for</param>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    public async Task BanByHash(string avatarHash)
    {
        var users = await ctx.Guild.GetUsersAsync();
        var usersToBan = users?.Where(x => x.AvatarId == avatarHash);

        if (usersToBan is null || !usersToBan.Any())
        {
            await ctx.Channel.SendErrorAsync(Strings.BanByHashNone(ctx.Guild.Id, avatarHash), Config);
            return;
        }

        if (await PromptUserConfirmAsync(
                Strings.BanByHashConfirm(ctx.Guild.Id, usersToBan.Count(), avatarHash), ctx.User.Id))
        {
            await ctx.Channel.SendConfirmAsync(Strings.BanByHashStart(ctx.Guild.Id, usersToBan.Count(), avatarHash));
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
                await ctx.Channel.SendConfirmAsync(Strings.BanByHashSuccess(ctx.Guild.Id, bannedUsers, avatarHash));
            else if (failedUsers == usersToBan.Count())
                await ctx.Channel.SendErrorAsync(Strings.BanByHashFailAll(ctx.Guild.Id, usersToBan.Count(), avatarHash),
                    Config);
            else
                await ctx.Channel.SendConfirmAsync(Strings.BanByHashFailSome(ctx.Guild.Id, bannedUsers, failedUsers,
                    avatarHash));
        }
    }

    /// <summary>
    ///     Allows you to opt the entire guild out of stats tracking.
    /// </summary>
    /// <example>.guildstatsoptout</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    public async Task GuildStatsOptOut()
    {
        var optout = await Service.ToggleOptOut(ctx.Guild);
        if (!optout)
            await ctx.Channel.SendConfirmAsync(Strings.CommandStatsEnabled(ctx.Guild.Id));
        else
            await ctx.Channel.SendConfirmAsync(Strings.CommandStatsDisabled(ctx.Guild.Id));
    }

    /// <summary>
    ///     Allows you to delete all stats data for the guild.
    /// </summary>
    /// <example>.deletestatsdata</example>
    [Cmd]
    [Aliases]
    [Ratelimit(3600)]
    [UserPerm(GuildPermission.Administrator)]
    public async Task DeleteGuildStatsData()
    {
        if (await PromptUserConfirmAsync(Strings.CommandStatsDeleteConfirm(ctx.Guild.Id), ctx.User.Id))
        {
            if (await Service.DeleteStatsData(ctx.Guild))
                await ctx.Channel.SendErrorAsync(Strings.CommandStatsDeleteSuccess(ctx.Guild.Id), Config);
            else
                await ctx.Channel.SendErrorAsync(Strings.CommandStatsDeleteFail(ctx.Guild.Id), Config);
        }
    }

    /// <summary>
    ///     Lets you set the nickname for a mentioned user. If no user is mentioned it defaults to setting a nickname for the
    ///     bot.
    /// </summary>
    /// <param name="gu">The target user.</param>
    /// <param name="newNick">The new nickname. Provide none to reset.</param>
    /// <example>.setnick @user newNick</example>
    [Cmd]
    [Aliases]
    [BotPerm(GuildPermission.ManageNicknames)]
    [UserPerm(GuildPermission.ManageNicknames)]
    [Priority(1)]
    public async Task SetNick(IGuildUser gu, [Remainder] string? newNick = null)
    {
        var sg = (SocketGuild)Context.Guild;
        if (sg.OwnerId == gu.Id || gu.GetRoles().Max(r => r.Position) >= sg.CurrentUser.GetRoles().Max(r => r.Position))
        {
            await ReplyErrorAsync(Strings.InsufPermsI(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await gu.ModifyAsync(u => u.Nickname = newNick).ConfigureAwait(false);

        await ReplyConfirmAsync(Strings.UserNick(ctx.Guild.Id, Format.Bold(gu.ToString()), Format.Bold(newNick) ?? "-"))
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Allows you to ban users with a specific role.
    /// </summary>
    /// <param name="role">The role to ban users in</param>
    /// <param name="reason">The reason for the ban, optional</param>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    public async Task BanInRole(IRole role, [Remainder] string reason = null)
    {
        var users = await ctx.Guild.GetUsersAsync();
        var usersToBan = users.Where(x => x.RoleIds.Contains(role.Id)).ToList();
        if (usersToBan.Count == 0)
        {
            await ctx.Channel.SendErrorAsync(Strings.BanInRoleNoUsers(ctx.Guild.Id), Config).ConfigureAwait(false);
            return;
        }

        if (!await PromptUserConfirmAsync(Strings.BanInRoleConfirm(ctx.Guild.Id, usersToBan.Count, role.Mention),
                ctx.User.Id))
        {
            await ctx.Channel.SendErrorAsync(Strings.BanInRoleCancelled(ctx.Guild.Id), Config).ConfigureAwait(false);
            return;
        }

        var failedUsers = 0;
        foreach (var i in usersToBan)
        {
            try
            {
                await ctx.Guild
                    .AddBanAsync(i, 0, reason ?? Strings.BanInRoleDefaultReason(ctx.Guild.Id, ctx.User, ctx.User.Id))
                    .ConfigureAwait(false);
            }
            catch
            {
                failedUsers++;
            }
        }

        if (failedUsers == 0)
            await ctx.Channel.SendConfirmAsync(Strings.BanInRoleSuccess(ctx.Guild.Id, usersToBan.Count, role.Mention))
                .ConfigureAwait(false);
        else if (failedUsers == usersToBan.Count)
            await ctx.Channel
                .SendErrorAsync(Strings.BanInRoleAllFailed(ctx.Guild.Id, users.Count, role.Mention), Config)
                .ConfigureAwait(false);
        else
            await ctx.Channel
                .SendConfirmAsync(Strings.BanInRolePartialSuccess(ctx.Guild.Id, usersToBan.Count - failedUsers,
                    role.Mention,
                    failedUsers)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Overload for setting the bot's nickname.
    /// </summary>
    /// <param name="newNick">The new nickname you want to set.</param>
    /// <example>.setnick newNick</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageNicknames)]
    [BotPerm(GuildPermission.ChangeNickname)]
    [Priority(0)]
    public async Task SetNick([Remainder] string? newNick = null)
    {
        if (string.IsNullOrWhiteSpace(newNick))
            return;
        var curUser = await ctx.Guild.GetCurrentUserAsync().ConfigureAwait(false);
        await curUser.ModifyAsync(u => u.Nickname = newNick).ConfigureAwait(false);

        await ReplyConfirmAsync(Strings.BotNick(ctx.Guild.Id, Format.Bold(newNick)) ?? "-").ConfigureAwait(false);
    }

    /// <summary>
    ///     Allows you to ban users with a specific name. This command will show a preview of the users that will be banned.
    ///     Takes a regex pattern as well.
    /// </summary>
    /// <param name="name">The name or regex pattern you want to use.</param>
    /// <example>.nameban name</example>
    /// <example>.nameban ^[a-z]{3,16}$</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    [BotPerm(GuildPermission.BanMembers)]
    public async Task NameBan([Remainder] string name)
    {
        var regex = GetCachedRegex(name);
        var users = await ctx.Guild.GetUsersAsync();
        users = users.Where(x => regex.IsMatch(x.Username.ToLower())).ToList();
        if (!users.Any())
        {
            await ctx.Channel.SendErrorAsync(Strings.NamebanNoUsersFound(ctx.Guild.Id), Config);
            return;
        }

        await ctx.Channel.SendConfirmAsync(Strings.NamebanMessageDelete(ctx.Guild.Id));
        var deleteString = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id);
        if (deleteString == null)
        {
            await ctx.Channel.SendErrorAsync(Strings.NamebanCancelled(ctx.Guild.Id), Config);
            return;
        }

        if (!int.TryParse(deleteString, out _))
        {
            await ctx.Channel.SendErrorAsync(Strings.InvalidInputNumber(ctx.Guild.Id), Config);
            return;
        }

        var deleteCount = int.Parse(deleteString);
        var components = new ComponentBuilder()
            .WithButton(Strings.Preview(ctx.Guild.Id), "previewbans")
            .WithButton(Strings.Execute(ctx.Guild.Id), "executeorder66", ButtonStyle.Success)
            .WithButton(Strings.Cancel(ctx.Guild.Id), "cancel", ButtonStyle.Danger);
        var eb = new EmbedBuilder()
            .WithDescription(Strings.PreviewOrExecute(ctx.Guild.Id))
            .WithOkColor();
        var msg = await ctx.Channel.SendMessageAsync(embed: eb.Build(), components: components.Build());
        var input = await GetButtonInputAsync(ctx.Channel.Id, msg.Id, ctx.User.Id);
        switch (input)
        {
            case "cancel":
                await ctx.Channel.SendErrorAsync(Strings.NamebanCancelled(ctx.Guild.Id), Config);
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
                    return new PageBuilder()
                        .WithTitle(Strings.NamebanPreviewCount(ctx.Guild.Id, users.Count, name.ToLower()))
                        .WithDescription(string.Join("\n", users.Skip(page * 20).Take(20)));
                }
            case "executeorder66":
                if (await PromptUserConfirmAsync(Strings.NamebanConfirm(ctx.Guild.Id, users.Count), ctx.User.Id))
                {
                    var failedUsers = 0;
                    await SuccessAsync(Strings.NamebanProcessing(ctx.Guild.Id, users.Count));
                    foreach (var i in users)
                    {
                        try
                        {
                            await ctx.Guild.AddBanAsync(i, deleteCount, options: new RequestOptions
                            {
                                AuditLogReason = Strings.MassBanRequestedBy(ctx.Guild.Id, ctx.User)
                            });
                        }
                        catch
                        {
                            failedUsers++;
                        }
                    }

                    await ctx.Channel.SendConfirmAsync(Strings.NamebanSuccess(ctx.Guild.Id, users.Count - failedUsers,
                        failedUsers));
                }

                break;
        }
    }


    /// <summary>
    ///     Allows you to ban users that have been in the server for a certain amount of time.
    /// </summary>
    /// <param name="time">The amount of time. Formatted as {0}mo{1}d{2}h{3}m{4}s</param>
    /// <param name="option">
    ///     Allows you to specify -accage to check account age rather than server age, or -p to preview the
    ///     users to ban.
    /// </param>
    /// <param name="time1">Allows you to specify a time range.</param>
    /// <example>.banunder 1mo</example>
    /// <example>.banunder 1mo -accage 1d</example>
    /// <example>.banunder 1mo -p</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    [BotPerm(GuildPermission.BanMembers)]
    public async Task BanUnder(StoopidTime time, string? option = null, StoopidTime? time1 = null)
    {
        try
        {
            await ctx.Guild.DownloadUsersAsync().ConfigureAwait(false);
            IEnumerable<IUser> users;
            if (option is not null && option.Equals("-accage", StringComparison.OrdinalIgnoreCase) && time1 is not null)
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
                await ctx.Channel.SendErrorAsync(Strings.BanunderNoUsers(ctx.Guild.Id), Config).ConfigureAwait(false);
                return;
            }

            if (option is not null && option.Equals("-p", StringComparison.OrdinalIgnoreCase))
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
                        .WithTitle(Strings.BanunderPreview(ctx.Guild.Id, users.Count(),
                            time.Time.Humanize(maxUnit: TimeUnit.Year)))
                        .WithDescription(string.Join("\n", users.Skip(page * 20).Take(20)));
                }
            }

            var banned = 0;
            var errored = 0;
            var msg = await ctx.Channel.SendMessageAsync(embed: new EmbedBuilder().WithErrorColor()
                .WithDescription(Strings.BanunderConfirm(ctx.Guild.Id, users.Count()))
                .Build());
            var text = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id);
            await msg.DeleteAsync();
            if (!text.ToLower().Contains("yes"))
                return;
            var message = await ConfirmAsync(Strings.BanunderBanning(ctx.Guild.Id, users.Count()))
                .ConfigureAwait(false);
            foreach (var i in users)
            {
                try
                {
                    await ctx.Guild.AddBanAsync(i, options: new RequestOptions
                    {
                        AuditLogReason = Strings.BanunderStarting(ctx.Guild.Id, ctx.User)
                    }).ConfigureAwait(false);
                    banned++;
                }
                catch
                {
                    errored++;
                }
            }

            var eb = new EmbedBuilder()
                .WithDescription(Strings.BanunderBanned(ctx.Guild.Id, banned, errored))
                .WithOkColor();
            await message.ModifyAsync(x => x.Embed = eb.Build()).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex.ToString());
        }
    }

    /// <summary>
    ///     Kicks users who have been in the server for less than a specified time.
    /// </summary>
    /// <param name="time">Time duration in a custom format</param>
    /// <param name="option">Optional parameter to preview users to be kicked</param>
    /// <remarks>
    ///     This command requires the caller to have GuildPermission.Administrator and the bot to have
    ///     GuildPermission.KickMembers.
    /// </remarks>
    /// <example>.kickunder 1mo</example>
    /// <example>.kickunder 1mo -p</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    [BotPerm(GuildPermission.KickMembers)]
    public async Task KickUnder(StoopidTime time, string? option = null)
    {
        await ctx.Guild.DownloadUsersAsync().ConfigureAwait(false);
        var users = ((SocketGuild)ctx.Guild).Users.Where(c =>
            c.JoinedAt != null && DateTimeOffset.Now.Subtract(c.JoinedAt.Value).TotalSeconds <= time.Time.TotalSeconds);
        var guildUsers = users as SocketGuildUser[] ?? users.ToArray();
        if (guildUsers.Length == 0)
        {
            await ErrorAsync(Strings.KickunderNoUsers(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        if (option is not null && option.Equals("-p", StringComparison.OrdinalIgnoreCase))
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
                    .WithTitle(Strings.KickunderPreview(ctx.Guild.Id, guildUsers.Length,
                        time.Time.Humanize(maxUnit: TimeUnit.Year)))
                    .WithDescription(string.Join("\n", guildUsers.Skip(page * 20).Take(20)));
            }
        }

        var banned = 0;
        var errored = 0;
        var msg = await ctx.Channel.SendMessageAsync(embed: new EmbedBuilder().WithErrorColor()
            .WithDescription(Strings.KickunderConfirm(ctx.Guild.Id, users.Count()))
            .Build());
        var text = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id);
        await msg.DeleteAsync();
        if (!text.ToLower().Contains("yes"))
            return;
        var message = await ConfirmAsync(Strings.KickunderKicking(ctx.Guild.Id, users.Count())).ConfigureAwait(false);
        foreach (var i in guildUsers)
        {
            try
            {
                await i.KickAsync(Strings.KickunderStarting(ctx.Guild.Id, ctx.User)).ConfigureAwait(false);
                banned++;
            }
            catch
            {
                errored++;
            }
        }

        var eb = new EmbedBuilder()
            .WithDescription(Strings.KickunderKicked(ctx.Guild.Id, banned, errored))
            .WithOkColor();
        await message.ModifyAsync(x => x.Embed = eb.Build()).ConfigureAwait(false);
    }


    /// <summary>
    ///     Prunes members from the server based on their activity or inactivity.
    /// </summary>
    /// <param name="time">Time duration in a custom format</param>
    /// <param name="e">Optional parameter indicating whether to include users with specific roles</param>
    /// <remarks>
    ///     This command requires the caller to have GuildPermission.Administrator and the bot to have
    ///     GuildPermission.ManageGuild.
    /// </remarks>
    /// <example>.prunemembers 30d</example>
    /// <example>.prunemembers 30d yes</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.Administrator)]
    [BotPerm(GuildPermission.ManageGuild)]
    public async Task PruneMembers(StoopidTime time, string e = "no")
    {
        try
        {
            await ConfirmAsync(Strings.CommandExpectedLatencyServerSize(ctx.Guild.Id));
            if (e == "no")
            {
                var toprune = await ctx.Guild.PruneUsersAsync(time.Time.Days, true);
                if (toprune == 0)
                {
                    await ErrorAsync(Strings.PruneNoMembersUpsell(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
                }

                var eb = new EmbedBuilder
                {
                    Description = $"Are you sure you want to prune {toprune} Members?", Color = Mewdeko.OkColor
                };
                if (!await PromptUserConfirmAsync(eb, ctx.User.Id).ConfigureAwait(false))
                {
                    await ConfirmAsync(Strings.PruneCanceledMemberUpsell(ctx.Guild.Id)).ConfigureAwait(false);
                }
                else
                {
                    var msg = await ConfirmAsync(Strings.PruningMembers(ctx.Guild.Id, toprune)).ConfigureAwait(false);
                    await ctx.Guild.PruneUsersAsync(time.Time.Days).ConfigureAwait(false);
                    var ebi = new EmbedBuilder
                    {
                        Description = Strings.PrunedMembers(ctx.Guild.Id, toprune), Color = Mewdeko.OkColor
                    };
                    await msg.ModifyAsync(x => x.Embed = ebi.Build()).ConfigureAwait(false);
                }
            }
            else
            {
                ctx.Guild.GetRole(await Service.GetMemberRole(ctx.Guild.Id));
                var toprune = await ctx.Guild.PruneUsersAsync(time.Time.Days, true,
                    includeRoleIds:
                    [
                        await Service.GetMemberRole(ctx.Guild.Id)
                    ]).ConfigureAwait(false);
                if (toprune == 0)
                {
                    await ErrorAsync(Strings.PruneNoMembers(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
                }

                var eb = new EmbedBuilder
                {
                    Description = Strings.PruneConfirm(ctx.Guild.Id, toprune), Color = Mewdeko.OkColor
                };
                if (!await PromptUserConfirmAsync(eb, ctx.User.Id).ConfigureAwait(false))
                {
                    await ConfirmAsync(Strings.PruneCanceled(ctx.Guild.Id)).ConfigureAwait(false);
                }
                else
                {
                    var msg = await ConfirmAsync(Strings.PruningMembers(ctx.Guild.Id, toprune)).ConfigureAwait(false);
                    await ctx.Guild.PruneUsersAsync(time.Time.Days,
                        includeRoleIds:
                        [
                            await Service.GetMemberRole(ctx.Guild.Id)
                        ]);
                    var ebi = new EmbedBuilder
                    {
                        Description = Strings.PrunedMembers(ctx.Guild.Id, toprune), Color = Mewdeko.OkColor
                    };
                    await msg.ModifyAsync(x => x.Embed = ebi.Build()).ConfigureAwait(false);
                }
            }
        }
        catch (Exception exception)
        {
            logger.LogError("Error in prunemembers: \n{0}", exception);
        }
    }


    /// <summary>
    ///     Sets the member role for the server. Currently unused.
    /// </summary>
    /// <param name="role">The role that members will have.</param>
    /// <remarks>
    ///     This command requires the caller to have GuildPermission.Administrator.
    /// </remarks>
    /// <example>.memberrole @Member</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.Administrator)]
    public async Task MemberRole(IRole? role)
    {
        var rol = await Service.GetMemberRole(ctx.Guild.Id);
        if (rol is 0 && role != null)
        {
            await Service.MemberRoleSet(ctx.Guild, role.Id).ConfigureAwait(false);
            await ConfirmAsync(Strings.MemberRoleSet(ctx.Guild.Id, role.Id)).ConfigureAwait(false);
        }

        if (rol != 0 && role != null && rol == role.Id)
        {
            await ErrorAsync(Strings.MemberRoleAlreadySet(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        if (rol is 0 && role == null)
        {
            await ErrorAsync(Strings.MemberRoleDisabled(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        if (rol != 0 && role is null)
        {
            var r = ctx.Guild.GetRole(rol);
            await ConfirmAsync(Strings.MemberRoleCurrent(ctx.Guild.Id, r.Id)).ConfigureAwait(false);
            return;
        }

        if (role != null && rol is not 0)
        {
            var oldrole = ctx.Guild.GetRole(rol);
            await Service.MemberRoleSet(ctx.Guild, role.Id).ConfigureAwait(false);
            await ConfirmAsync(Strings.MemberRoleUpdated(ctx.Guild.Id, oldrole.Id, role.Id)).ConfigureAwait(false);
        }
    }


    /// <summary>
    ///     Sets or updates the role assigned to staff members.
    /// </summary>
    /// <param name="role">The role to be assigned to staff members</param>
    /// <remarks>
    ///     This command requires the caller to have GuildPermission.Administrator.
    /// </remarks>
    /// <example>.staffrole @Staff</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.Administrator)]
    public async Task StaffRole([Remainder] IRole? role = null)
    {
        var rol = await Service.GetStaffRole(ctx.Guild.Id);
        if (rol is 0 && role != null)
        {
            await Service.StaffRoleSet(ctx.Guild, role.Id).ConfigureAwait(false);
            await ConfirmAsync(Strings.StaffRoleSet(ctx.Guild.Id, role.Id)).ConfigureAwait(false);
        }

        if (rol != 0 && role != null && rol == role.Id)
        {
            await ErrorAsync(Strings.StaffRoleAlreadySet(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        if (rol is 0 && role == null)
        {
            await ErrorAsync(Strings.StaffRoleMissing(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        if (rol != 0 && role is null)
        {
            var r = ctx.Guild.GetRole(rol);
            await ConfirmAsync(Strings.StaffRoleCurrent(ctx.Guild.Id, r.Id)).ConfigureAwait(false);
            return;
        }

        if (role != null && rol is not 0)
        {
            var oldrole = ctx.Guild.GetRole(rol);
            await Service.StaffRoleSet(ctx.Guild, role.Id).ConfigureAwait(false);
            await ConfirmAsync(Strings.StaffRoleUpdated(ctx.Guild.Id, oldrole.Id, role.Id)).ConfigureAwait(false);
        }
    }


    /// <summary>
    ///     Disables the role assigned to staff members.
    /// </summary>
    /// <remarks>
    ///     This command requires the caller to have GuildPermission.Administrator.
    /// </remarks>
    /// <example>.staffroledisable</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.Administrator)]
    public async Task StaffRoleDisable()
    {
        var r = await Service.GetStaffRole(ctx.Guild.Id);
        if (r == 0)
        {
            await ctx.Channel.SendErrorAsync(Strings.StaffRoleMissing(ctx.Guild.Id), Config).ConfigureAwait(false);
        }
        else
        {
            await Service.StaffRoleSet(ctx.Guild, 0).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync(Strings.StaffRoleDisabled(ctx.Guild.Id)).ConfigureAwait(false);
        }
    }


    /// <summary>
    ///     Displays the status of deleting messages on command execution.
    /// </summary>
    /// <remarks>
    ///     This command requires the caller to have GuildPermission.Administrator and the bot to have
    ///     GuildPermission.ManageMessages.
    /// </remarks>
    /// <example>.delmsgoncmd</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.Administrator)]
    [BotPerm(GuildPermission.ManageMessages)]
    [Priority(2)]
    public async Task Delmsgoncmd(List _)
    {
        var guild = (SocketGuild)ctx.Guild;
        var (enabled, channels) = await Service.GetDelMsgOnCmdData(ctx.Guild.Id);

        var embed = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(Strings.ServerDelmsgoncmd(ctx.Guild.Id))
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

        embed.AddField(Strings.ChannelDelmsgoncmd(ctx.Guild.Id), str);

        await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
    }


    /// <summary>
    ///     Toggles the deletion of messages on command execution for the server.
    /// </summary>
    /// <param name="_">Unused parameter</param>
    /// <remarks>
    ///     This command requires the caller to have GuildPermission.Administrator and the bot to have
    ///     GuildPermission.ManageMessages.
    /// </remarks>
    /// <example>.delmsgoncmd</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.Administrator)]
    [BotPerm(GuildPermission.ManageMessages)]
    [Priority(1)]
    public async Task Delmsgoncmd(Server _ = Server.Server)
    {
        if (await Service.ToggleDeleteMessageOnCommand(ctx.Guild.Id))
        {
            await ReplyConfirmAsync(Strings.DelmsgOn(ctx.Guild.Id)).ConfigureAwait(false);
        }
        else
        {
            await ReplyConfirmAsync(Strings.DelmsgOff(ctx.Guild.Id)).ConfigureAwait(false);
        }
    }


    /// <summary>
    ///     Sets the state of deleting messages on command execution for a specific channel.
    /// </summary>
    /// <param name="_">Unused parameter</param>
    /// <param name="s">The state to set for deleting messages on command execution</param>
    /// <param name="ch">The channel where the state should be applied</param>
    /// <remarks>
    ///     This command requires the caller to have GuildPermission.Administrator and the bot to have
    ///     GuildPermission.ManageMessages.
    /// </remarks>
    /// <example>.delmsgoncmd enable #channel</example>
    /// <example>.delmsgoncmd disable #channel</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.Administrator)]
    [BotPerm(GuildPermission.ManageMessages)]
    [Priority(0)]
    public Task Delmsgoncmd(Channel _, State s, ITextChannel ch)
    {
        return Delmsgoncmd(_, s, ch.Id);
    }


    /// <summary>
    ///     Sets the state of deleting messages on command execution for a specific channel.
    /// </summary>
    /// <param name="_">Unused parameter</param>
    /// <param name="s">The state to set for deleting messages on command execution</param>
    /// <param name="chId">Optional channel ID where the state should be applied</param>
    /// <remarks>
    ///     This command requires the caller to have GuildPermission.Administrator and the bot to have
    ///     GuildPermission.ManageMessages.
    /// </remarks>
    /// <example>.delmsgoncmd enable #channel</example>
    /// <example>.delmsgoncmd disable #channel</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.Administrator)]
    [BotPerm(GuildPermission.ManageMessages)]
    [Priority(1)]
    public async Task Delmsgoncmd(Channel _, State s, ulong? chId = null)
    {
        var actualChId = chId ?? ctx.Channel.Id;
        await Service.SetDelMsgOnCmdState(ctx.Guild.Id, actualChId, s).ConfigureAwait(false);

        switch (s)
        {
            case State.Disable:
                await ReplyConfirmAsync(Strings.DelmsgChannelOff(ctx.Guild.Id)).ConfigureAwait(false);
                break;
            case State.Enable:
                await ReplyConfirmAsync(Strings.DelmsgChannelOn(ctx.Guild.Id)).ConfigureAwait(false);
                break;
            default:
                await ReplyConfirmAsync(Strings.DelmsgChannelInherit(ctx.Guild.Id)).ConfigureAwait(false);
                break;
        }
    }


    /// <summary>
    ///     Deafens specified users in the guild.
    /// </summary>
    /// <param name="users">The users to deafen</param>
    /// <remarks>
    ///     This command requires the caller to have GuildPermission.DeafenMembers and the bot to have
    ///     GuildPermission.DeafenMembers.
    /// </remarks>
    /// <example>.deafen @User1 @User2</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.DeafenMembers)]
    [BotPerm(GuildPermission.DeafenMembers)]
    public async Task Deafen(params IGuildUser[] users)
    {
        await AdministrationService.DeafenUsers(true, users).ConfigureAwait(false);
        await ReplyConfirmAsync(Strings.Deafen(ctx.Guild.Id)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Undeafens specified users in the guild.
    /// </summary>
    /// <param name="users">The users to undeafen</param>
    /// <remarks>
    ///     This command requires the caller to have GuildPermission.DeafenMembers and the bot to have
    ///     GuildPermission.DeafenMembers.
    /// </remarks>
    /// <example>.undeafen @User1 @User2</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.DeafenMembers)]
    [BotPerm(GuildPermission.DeafenMembers)]
    public async Task UnDeafen(params IGuildUser[] users)
    {
        await AdministrationService.DeafenUsers(false, users).ConfigureAwait(false);
        await ReplyConfirmAsync(Strings.Undeafen(ctx.Guild.Id)).ConfigureAwait(false);
    }


    /// <summary>
    ///     Deletes the specified voice channel.
    /// </summary>
    /// <param name="voiceChannel">The voice channel to delete</param>
    /// <remarks>
    ///     This command requires the caller to have GuildPermission.ManageChannels and the bot to have
    ///     GuildPermission.ManageChannels.
    /// </remarks>
    /// <example>.delvoich VoiceChannelName</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageChannels)]
    [BotPerm(GuildPermission.ManageChannels)]
    public async Task DelVoiChanl([Remainder] IVoiceChannel voiceChannel)
    {
        await voiceChannel.DeleteAsync().ConfigureAwait(false);
        await ReplyConfirmAsync(Strings.Delvoich(ctx.Guild.Id, Format.Bold(voiceChannel.Name))).ConfigureAwait(false);
    }

    /// <summary>
    ///     Creates a new voice channel with the specified name.
    /// </summary>
    /// <param name="channelName">The name of the voice channel to create</param>
    /// <remarks>
    ///     This command requires the caller to have GuildPermission.ManageChannels and the bot to have
    ///     GuildPermission.ManageChannels.
    /// </remarks>
    /// <example>.creatvoich VoiceChannelName</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageChannels)]
    [BotPerm(GuildPermission.ManageChannels)]
    public async Task CreatVoiChanl([Remainder] string channelName)
    {
        var ch = await ctx.Guild.CreateVoiceChannelAsync(channelName).ConfigureAwait(false);
        await ReplyConfirmAsync(Strings.Createvoich(ctx.Guild.Id, Format.Bold(ch.Name))).ConfigureAwait(false);
    }

    /// <summary>
    ///     Deletes the specified text channel.
    /// </summary>
    /// <param name="toDelete">The text channel to delete</param>
    /// <remarks>
    ///     This command requires the caller to have GuildPermission.ManageChannels and the bot to have
    ///     GuildPermission.ManageChannels.
    /// </remarks>
    /// <example>.deltxtchan TextChannelName</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageChannels)]
    [BotPerm(GuildPermission.ManageChannels)]
    public async Task DelTxtChanl([Remainder] ITextChannel toDelete)
    {
        await toDelete.DeleteAsync().ConfigureAwait(false);
        await ReplyConfirmAsync(Strings.Deltextchan(ctx.Guild.Id, Format.Bold(toDelete.Name))).ConfigureAwait(false);
    }


    /// <summary>
    ///     Creates a new text channel with the specified name.
    /// </summary>
    /// <param name="channelName">The name of the text channel to create</param>
    /// <remarks>
    ///     This command requires the caller to have GuildPermission.ManageChannels and the bot to have
    ///     GuildPermission.ManageChannels.
    /// </remarks>
    /// <example>.createtxtchan TextChannelName</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageChannels)]
    [BotPerm(GuildPermission.ManageChannels)]
    public async Task CreaTxtChanl([Remainder] string channelName)
    {
        var txtCh = await ctx.Guild.CreateTextChannelAsync(channelName).ConfigureAwait(false);
        await ReplyConfirmAsync(Strings.Createtextchan(ctx.Guild.Id, Format.Bold(txtCh.Name))).ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets the topic of the current text channel.
    /// </summary>
    /// <param name="topic">The topic to set for the text channel</param>
    /// <remarks>
    ///     This command requires the caller to have GuildPermission.ManageChannels and the bot to have
    ///     GuildPermission.ManageChannels.
    /// </remarks>
    /// <example>.settopic NewTopic</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageChannels)]
    [BotPerm(GuildPermission.ManageChannels)]
    public async Task SetTopic([Remainder] string? topic = null)
    {
        var channel = (ITextChannel)ctx.Channel;
        topic ??= "";
        await channel.ModifyAsync(c => c.Topic = topic).ConfigureAwait(false);
        await ReplyConfirmAsync(Strings.SetTopic(ctx.Guild.Id)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets the name of the current text channel.
    /// </summary>
    /// <param name="name">The name to set for the text channel</param>
    /// <remarks>
    ///     This command requires the caller to have GuildPermission.ManageChannels and the bot to have
    ///     GuildPermission.ManageChannels.
    /// </remarks>
    /// <example>.setchannelname NewChannelName</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageChannels)]
    [BotPerm(GuildPermission.ManageChannels)]
    public async Task SetChanlName([Remainder] string name)
    {
        var channel = (ITextChannel)ctx.Channel;
        await channel.ModifyAsync(c => c.Name = name).ConfigureAwait(false);
        await ReplyConfirmAsync(Strings.SetChannelName(ctx.Guild.Id)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Toggles the NSFW setting of the current text channel.
    /// </summary>
    /// <remarks>
    ///     This command requires the caller to have GuildPermission.ManageChannels and the bot to have
    ///     GuildPermission.ManageChannels.
    /// </remarks>
    /// <example>.nsfwtoggle</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageChannels)]
    [BotPerm(GuildPermission.ManageChannels)]
    public async Task NsfwToggle()
    {
        var channel = (ITextChannel)ctx.Channel;
        var isEnabled = channel.IsNsfw;

        await channel.ModifyAsync(c => c.IsNsfw = !isEnabled).ConfigureAwait(false);

        if (isEnabled)
            await ReplyConfirmAsync(Strings.NsfwSetFalse(ctx.Guild.Id)).ConfigureAwait(false);
        else
            await ReplyConfirmAsync(Strings.NsfwSetTrue(ctx.Guild.Id)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Edits a message in the specified text channel.
    /// </summary>
    /// <param name="channel">The text channel where the message is located</param>
    /// <param name="messageId">The ID of the message to edit</param>
    /// <param name="text">The new text for the message</param>
    /// <remarks>
    ///     This command requires the caller to have ChannelPermission.ManageMessages.
    /// </remarks>
    /// <example>.edit 123456789012345678 NewMessageText</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [Priority(1)]
    public async Task Edit(ITextChannel channel, ulong messageId, [Remainder] string? text)
    {
        var userPerms = ((SocketGuildUser)ctx.User).GetPermissions(channel);
        var botPerms = ((SocketGuild)ctx.Guild).CurrentUser.GetPermissions(channel);
        if (!userPerms.Has(ChannelPermission.ManageMessages))
        {
            await ReplyErrorAsync(Strings.InsufPermsU(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        if (!botPerms.Has(ChannelPermission.ViewChannel))
        {
            await ReplyErrorAsync(Strings.InsufPermsI(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await AdministrationService.EditMessage(ctx, channel, messageId, text).ConfigureAwait(false);
    }

    /// <summary>
    ///     Deletes a message by its ID in the current text channel.
    /// </summary>
    /// <param name="messageId">The ID of the message to delete</param>
    /// <param name="time">Optional time duration after which the message should be deleted</param>
    /// <remarks>
    ///     This command requires the caller to have ChannelPermission.ManageMessages and the bot to have
    ///     ChannelPermission.ManageMessages.
    /// </remarks>
    /// <example>.delete 123456789012345678</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(ChannelPermission.ManageMessages)]
    [BotPerm(ChannelPermission.ManageMessages)]
    public Task Delete(ulong messageId, StoopidTime? time = null)
    {
        return Delete((ITextChannel)ctx.Channel, messageId, time);
    }

    /// <summary>
    ///     Deletes a message by its ID in the specified text channel.
    /// </summary>
    /// <param name="channel">The text channel where the message is located</param>
    /// <param name="messageId">The ID of the message to delete</param>
    /// <param name="time">Optional time duration after which the message should be deleted</param>
    /// <remarks>
    ///     This command requires the caller to have ChannelPermission.ManageMessages.
    /// </remarks>
    /// <example>.delete #channel 123456789012345678</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public Task Delete(ITextChannel channel, ulong messageId, StoopidTime? time = null)
    {
        return InternalMessageAction(channel, messageId, time);
    }

    /// <summary>
    ///     Internal handler for message deletion.
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
            await ReplyErrorAsync(Strings.InsufPermsU(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        if (!botPerms.Has(ChannelPermission.ManageMessages))
        {
            await ReplyAsync(Strings.InsufPermsI(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var msg = await channel.GetMessageAsync(messageId).ConfigureAwait(false);
        if (msg == null)
        {
            await ReplyErrorAsync(Strings.MsgNotFound(ctx.Guild.Id)).ConfigureAwait(false);
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
            await ReplyErrorAsync(Strings.TimeTooLong(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await ctx.OkAsync().ConfigureAwait(false);
    }

    /// <summary>
    ///     Renames the specified channel.
    /// </summary>
    /// <param name="channel">The channel to rename</param>
    /// <param name="name">The new name for the channel</param>
    /// <remarks>
    ///     This command requires the caller to have GuildPermission.ManageChannels.
    /// </remarks>
    /// <example>.renamechannel #channel NewChannelName</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageChannels)]
    public async Task RenameChannel(IGuildChannel channel, [Remainder] string name)
    {
        await channel.ModifyAsync(x => x.Name = name).ConfigureAwait(false);
        await ConfirmAsync(Strings.ChannelRenamed(ctx.Guild.Id)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets the bot's guild-specific avatar.
    /// </summary>
    /// <param name="avatarUrl">URL to the avatar image (optional if attachment is provided)</param>
    /// <remarks>
    ///     This command requires the caller to have GuildPermission.ManageGuild.
    ///     The image will be downloaded and converted to base64 for the Discord API.
    ///     You can either provide a URL or attach an image to the message.
    /// </remarks>
    /// <example>.setguildavatar https://example.com/avatar.png</example>
    /// <example>.setguildavatar (with attached image)</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task SetGuildAvatar([Remainder] string? avatarUrl = null)
    {
        try
        {
            // Check for attachment first, then fall back to URL parameter
            var imageUrl = avatarUrl;
            if (string.IsNullOrWhiteSpace(imageUrl) && ctx.Message.Attachments.Any())
            {
                imageUrl = ctx.Message.Attachments.First().Url;
            }

            if (string.IsNullOrWhiteSpace(imageUrl))
            {
                await ctx.Channel.SendErrorAsync(Strings.SetGuildProfileNoImage(ctx.Guild.Id), Config);
                return;
            }

            var loadingMsg = await ctx.Channel.SendConfirmAsync(Strings.SetGuildProfileProcessing(ctx.Guild.Id));

            await Service.SetGuildProfile(ctx.Guild.Id, imageUrl, null, null);

            await loadingMsg.DeleteAsync();
            await ctx.Channel.SendConfirmAsync(Strings.SetGuildAvatarSuccess(ctx.Guild.Id));
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "HTTP error setting guild avatar for guild {GuildId}", ctx.Guild.Id);
            await ctx.Channel.SendErrorAsync(Strings.SetGuildProfileHttpError(ctx.Guild.Id), Config);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error setting guild avatar for guild {GuildId}", ctx.Guild.Id);
            await ctx.Channel.SendErrorAsync(Strings.SetGuildProfileError(ctx.Guild.Id), Config);
        }
    }

    /// <summary>
    ///     Sets the bot's guild-specific banner.
    /// </summary>
    /// <param name="bannerUrl">URL to the banner image (optional if attachment is provided)</param>
    /// <remarks>
    ///     This command requires the caller to have GuildPermission.ManageGuild.
    ///     The image will be downloaded and converted to base64 for the Discord API.
    ///     You can either provide a URL or attach an image to the message.
    /// </remarks>
    /// <example>.setguildbanner https://example.com/banner.png</example>
    /// <example>.setguildbanner (with attached image)</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task SetGuildBanner([Remainder] string? bannerUrl = null)
    {
        try
        {
            // Check for attachment first, then fall back to URL parameter
            var imageUrl = bannerUrl;
            if (string.IsNullOrWhiteSpace(imageUrl) && ctx.Message.Attachments.Any())
            {
                imageUrl = ctx.Message.Attachments.First().Url;
            }

            if (string.IsNullOrWhiteSpace(imageUrl))
            {
                await ctx.Channel.SendErrorAsync(Strings.SetGuildProfileNoImage(ctx.Guild.Id), Config);
                return;
            }

            var loadingMsg = await ctx.Channel.SendConfirmAsync(Strings.SetGuildProfileProcessing(ctx.Guild.Id));

            await Service.SetGuildProfile(ctx.Guild.Id, null, imageUrl, null);

            await loadingMsg.DeleteAsync();
            await ctx.Channel.SendConfirmAsync(Strings.SetGuildBannerSuccess(ctx.Guild.Id));
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "HTTP error setting guild banner for guild {GuildId}", ctx.Guild.Id);
            await ctx.Channel.SendErrorAsync(Strings.SetGuildProfileHttpError(ctx.Guild.Id), Config);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error setting guild banner for guild {GuildId}", ctx.Guild.Id);
            await ctx.Channel.SendErrorAsync(Strings.SetGuildProfileError(ctx.Guild.Id), Config);
        }
    }

    /// <summary>
    ///     Sets the bot's guild-specific bio.
    /// </summary>
    /// <param name="bio">Bio text</param>
    /// <remarks>
    ///     This command requires the caller to have GuildPermission.ManageGuild.
    /// </remarks>
    /// <example>.setguildbio This is my cool bio!</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task SetGuildBio([Remainder] string bio)
    {
        try
        {
            var loadingMsg = await ctx.Channel.SendConfirmAsync(Strings.SetGuildProfileProcessing(ctx.Guild.Id));

            await Service.SetGuildProfile(ctx.Guild.Id, null, null, bio);

            await loadingMsg.DeleteAsync();
            await ctx.Channel.SendConfirmAsync(Strings.SetGuildBioSuccess(ctx.Guild.Id));
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "HTTP error setting guild bio for guild {GuildId}", ctx.Guild.Id);
            await ctx.Channel.SendErrorAsync(Strings.SetGuildProfileHttpError(ctx.Guild.Id), Config);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error setting guild bio for guild {GuildId}", ctx.Guild.Id);
            await ctx.Channel.SendErrorAsync(Strings.SetGuildProfileError(ctx.Guild.Id), Config);
        }
    }
}