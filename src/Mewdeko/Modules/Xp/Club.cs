using Discord.Commands;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Xp.Common;
using Mewdeko.Modules.Xp.Services;

namespace Mewdeko.Modules.Xp;

public partial class Xp
{
    /// <summary>
    /// Contains commands related to clubs.
    /// </summary>
    /// <param name="serv">The interactive service.</param>
    [Group]
    public class Club(InteractiveService serv) : MewdekoSubmodule<ClubService>
    {
        /// <summary>
        /// Transfers the ownership of the club to a new owner.
        /// </summary>
        /// <param name="newOwner">The user to whom the ownership will be transferred.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <remarks>
        /// The command initiates a transfer of club ownership. If successful, it notifies the command invoker of the transfer.
        /// Otherwise, it reports a failure to transfer.
        /// </remarks>
        [Cmd, Aliases]
        public async Task ClubTransfer([Remainder] IUser newOwner)
        {
            var club = await Service.TransferClub(ctx.User, newOwner).ConfigureAwait(false);

            if (club != null)
            {
                await ReplyConfirmLocalizedAsync("club_transfered",
                    Format.Bold(club.Name),
                    Format.Bold(newOwner.ToString())).ConfigureAwait(false);
            }
            else
            {
                await ReplyErrorLocalizedAsync("club_transfer_failed").ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Toggles the administrative status of a user within the club.
        /// </summary>
        /// <param name="toAdmin">The user whose admin status will be toggled.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <remarks>
        /// The command attempts to toggle the specified user's administrative status in the club.
        /// It notifies the invoker of the result.
        /// </remarks>
        [Cmd, Aliases]
        public async Task ClubAdmin([Remainder] IUser toAdmin)
        {
            bool admin;
            try
            {
                admin = await Service.ToggleAdmin(ctx.User, toAdmin).ConfigureAwait(false);
            }
            catch (InvalidOperationException)
            {
                await ReplyErrorLocalizedAsync("club_admin_error").ConfigureAwait(false);
                return;
            }

            if (admin)
            {
                await ReplyConfirmLocalizedAsync("club_admin_add", Format.Bold(toAdmin.ToString()))
                    .ConfigureAwait(false);
            }
            else
            {
                await ReplyConfirmLocalizedAsync("club_admin_remove", Format.Bold(toAdmin.ToString()))
                    .ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Creates a new club with the specified name.
        /// </summary>
        /// <param name="clubName">The name of the club to be created.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <remarks>
        /// The command creates a new club with the given name. It checks for name validity and uniqueness.
        /// On success, it notifies the invoker; on failure, it provides an error message.
        /// </remarks>
        [Cmd, Aliases]
        public async Task ClubCreate([Remainder] string clubName)
        {
            if (string.IsNullOrWhiteSpace(clubName) || clubName.Length > 20)
            {
                await ReplyErrorLocalizedAsync("club_name_too_long").ConfigureAwait(false);
                return;
            }

            var club = await Service.CreateClub(ctx.User, clubName).ConfigureAwait(false);
            if (!club.Item1)
            {
                await ReplyErrorLocalizedAsync("club_create_error").ConfigureAwait(false);
                return;
            }

            await ReplyConfirmLocalizedAsync("club_created", Format.Bold(club.Item2.ToString())).ConfigureAwait(false);
        }

        /// <summary>
        /// Sets or updates the club's icon.
        /// </summary>
        /// <param name="url">The URL of the new club icon. If null, the current icon will be removed.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <remarks>
        /// The command updates the club's icon to a new image from the provided URL.
        /// If the URL is invalid or the operation fails, it notifies the invoker.
        /// </remarks>
        [Cmd, Aliases]
        public async Task ClubIcon([Remainder] string? url = null)
        {
            if ((!Uri.IsWellFormedUriString(url, UriKind.Absolute) && url != null)
                || !await Service.SetClubIcon(ctx.User.Id, url == null ? null : new Uri(url)).ConfigureAwait(false))
            {
                await ReplyErrorLocalizedAsync("club_icon_error").ConfigureAwait(false);
                return;
            }

            await ReplyConfirmLocalizedAsync("club_icon_set").ConfigureAwait(false);
        }

        /// <summary>
        /// Displays information about a club based on a member or a club name.
        /// </summary>
        /// <param name="user">The user whose club information will be displayed. If null, the command invoker's club will be used.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <remarks>
        /// Overloaded method that either takes a user to find their club or directly takes a club name.
        /// It displays detailed information about the club, including members and their roles.
        /// </remarks>
        [Cmd, Aliases, Priority(1)]
        public async Task ClubInformation(IUser? user = null)
        {
            user ??= ctx.User;
            var club = await Service.GetClubByMember(user);
            if (club == null)
            {
                await ErrorLocalizedAsync("club_user_not_in_club", Format.Bold(user.ToString())).ConfigureAwait(false);
                return;
            }

            await ClubInformation(club.ToString()).ConfigureAwait(false);
        }

        /// <summary>
        /// Displays information about a club based on a member or a club name.
        /// </summary>
        /// <param name="clubName">The club information will be displayed. If null, the command invoker's club will be used.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <remarks>
        /// Overloaded method that either takes a user to find their club or directly takes a club name.
        /// It displays detailed information about the club, including members and their roles.
        /// </remarks>
        [Cmd, Aliases, Priority(0)]
        public async Task ClubInformation([Remainder] string? clubName = null)
        {
            if (string.IsNullOrWhiteSpace(clubName))
            {
                await ClubInformation(ctx.User).ConfigureAwait(false);
                return;
            }

            if (!Service.GetClubByName(clubName, out var club))
            {
                await ReplyErrorLocalizedAsync("club_not_exists").ConfigureAwait(false);
                return;
            }

            var lvl = new LevelStats(club.Xp);
            var users = club.Users
                .OrderByDescending(x =>
                {
                    var l = new LevelStats(x.TotalXp).Level;
                    if (club.OwnerId == x.Id)
                        return int.MaxValue;
                    if (false.ParseBoth(x.IsClubAdmin))
                        return (int.MaxValue / 2) + l;
                    return l;
                });

            var paginator = new LazyPaginatorBuilder()
                .AddUser(ctx.User)
                .WithPageFactory(PageFactory)
                .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                .WithMaxPageIndex(club.Users.Count / 10)
                .WithDefaultEmotes()
                .WithActionOnCancellation(ActionOnStop.DeleteMessage)
                .Build();

            await serv.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60)).ConfigureAwait(false);

            async Task<PageBuilder> PageFactory(int page)
            {
                await Task.CompletedTask.ConfigureAwait(false);
                var embed = new PageBuilder()
                    .WithOkColor()
                    .WithTitle($"{club}")
                    .WithDescription($"{GetText("level_x", lvl.Level)} ({club.Xp} xp)")
                    .AddField(GetText("desc"), string.IsNullOrWhiteSpace(club.Description) ? "-" : club.Description)
                    .AddField(GetText("owner"), club.Owner.ToString(), true)
                    .AddField(GetText("level_req"), club.MinimumLevelReq.ToString(), true)
                    .AddField(GetText("members"), string.Join("\n", users
                        .Skip(page * 10)
                        .Take(10)
                        .Select(x =>
                        {
                            var l = new LevelStats(x.TotalXp);
                            var lvlStr = Format.Bold($" ⟪{l.Level}⟫");
                            if (club.OwnerId == x.Id)
                                return $"{x}🌟{lvlStr}";
                            if (false.ParseBoth(x.IsClubAdmin))
                                return $"{x}⭐{lvlStr}";
                            return x + lvlStr;
                        })));

                return Uri.IsWellFormedUriString(club.ImageUrl, UriKind.Absolute)
                    ? embed.WithThumbnailUrl(club.ImageUrl)
                    : embed;
            }
        }

        /// <summary>
        /// Displays the list of users banned from the club.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <remarks>
        /// The command fetches and displays a list of users who are banned from the club.
        /// If the club does not exist or there are no bans, it provides an appropriate message.
        /// </remarks>
        [Cmd, Aliases]
        public async Task ClubBans()
        {
            var club = await Service.GetClubWithBansAndApplications(ctx.User.Id).ConfigureAwait(false);
            if (club == null)
            {
                await ReplyErrorLocalizedAsync("club_not_exists_owner").ConfigureAwait(false);
                return;
            }

            var bans = club
                .Bans
                .Select(x => x.User)
                .ToArray();

            var paginator = new LazyPaginatorBuilder()
                .AddUser(ctx.User)
                .WithPageFactory(PageFactory)
                .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                .WithMaxPageIndex(bans.Length / 10)
                .WithDefaultEmotes()
                .WithActionOnCancellation(ActionOnStop.DeleteMessage)
                .Build();

            await serv.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60)).ConfigureAwait(false);

            async Task<PageBuilder> PageFactory(int page)
            {
                await Task.CompletedTask.ConfigureAwait(false);
                var toShow = string.Join("\n", bans
                    .Skip(page * 10)
                    .Take(10)
                    .Select(x => x.ToString()));

                return new PageBuilder()
                    .WithTitle(GetText("club_bans_for", club.ToString()))
                    .WithDescription(toShow)
                    .WithOkColor();
            }
        }

        /// <summary>
        /// Displays the list of pending applications to the club.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <remarks>
        /// The command shows all users who have applied to join the club.
        /// It allows the club owner to manage these applications.
        /// </remarks>
        [Cmd, Aliases]
        public async Task ClubApps()
        {
            var club = await Service.GetClubWithBansAndApplications(ctx.User.Id).ConfigureAwait(false);
            if (club == null)
            {
                await ReplyErrorLocalizedAsync("club_not_exists_owner").ConfigureAwait(false);
                return;
            }

            var apps = club
                .Applicants
                .Select(x => x.User)
                .ToArray();

            var paginator = new LazyPaginatorBuilder()
                .AddUser(ctx.User)
                .WithPageFactory(PageFactory)
                .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                .WithMaxPageIndex(apps.Length / 10)
                .WithDefaultEmotes()
                .WithActionOnCancellation(ActionOnStop.DeleteMessage)
                .Build();

            await serv.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60)).ConfigureAwait(false);

            async Task<PageBuilder> PageFactory(int page)
            {
                await Task.CompletedTask.ConfigureAwait(false);
                var toShow = string.Join("\n", apps
                    .Skip(page * 10)
                    .Take(10)
                    .Select(x => x.ToString()));

                return new PageBuilder()
                    .WithTitle(GetText("club_apps_for", club.ToString()))
                    .WithDescription(toShow)
                    .WithOkColor();
            }
        }

        /// <summary>
        /// Applies to join a specified club.
        /// </summary>
        /// <param name="clubName">The name of the club to apply to.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <remarks>
        /// The command sends an application to join the named club.
        /// It checks for eligibility and notifies the user of the outcome.
        /// </remarks>
        [Cmd, Aliases]
        public async Task ClubApply([Remainder] string clubName)
        {
            if (string.IsNullOrWhiteSpace(clubName))
                return;

            if (!Service.GetClubByName(clubName, out var club))
            {
                await ReplyErrorLocalizedAsync("club_not_exists").ConfigureAwait(false);
                return;
            }

            if (await Service.ApplyToClub(ctx.User, club).ConfigureAwait(false))
            {
                await ReplyConfirmLocalizedAsync("club_applied", Format.Bold(club.ToString()))
                    .ConfigureAwait(false);
            }
            else
            {
                await ReplyErrorLocalizedAsync("club_apply_error").ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Accepts a user's application to join the club.
        /// </summary>
        /// <param name="user">The user whose application will be accepted.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <remarks>
        /// This command accepts an application based on the user's name.
        /// It adds the user to the club and notifies the invoker.
        /// </remarks>
        [Cmd, Aliases, Priority(1)]
        public Task ClubAccept(IUser user) => ClubAccept(user.ToString());

        /// <summary>
        /// Accepts a user's application to join the club.
        /// </summary>
        /// <param name="userName">The name of the user whose application will be accepted.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <remarks>
        /// This command accepts an application based on the user's name.
        /// It adds the user to the club and notifies the invoker.
        /// </remarks>
        [Cmd, Aliases, Priority(0)]
        public async Task ClubAccept([Remainder] string userName)
        {
            var app = await Service.AcceptApplication(ctx.User.Id, userName).ConfigureAwait(false);
            if (app.Item1)
            {
                await ReplyConfirmLocalizedAsync("club_accepted", Format.Bold(app.Item2.ToString()))
                    .ConfigureAwait(false);
            }
            else
            {
                await ReplyErrorLocalizedAsync("club_accept_error").ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Leaves the current club.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <remarks>
        /// The command allows a user to leave their current club.
        /// It ensures the user is part of a club before proceeding with the operation.
        /// </remarks>
        [Cmd, Aliases]
        public async Task Clubleave()
        {
            if (await Service.LeaveClub(ctx.User).ConfigureAwait(false))
                await ReplyConfirmLocalizedAsync("club_left").ConfigureAwait(false);
            else
                await ReplyErrorLocalizedAsync("club_not_in_club").ConfigureAwait(false);
        }

        /// <summary>
        /// Kicks a user from the club.
        /// </summary>
        /// <param name="user">The user to be kicked.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <remarks>
        /// The command removes a specified user from the club.
        /// It requires administrative privileges within the club to execute.
        /// </remarks>
        [Cmd, Aliases, Priority(1)]
        public Task ClubKick([Remainder] IUser user) => ClubKick(user.ToString());

        /// <summary>
        /// Kicks a user from the club.
        /// </summary>
        /// <param name="userName">The name of the user to be kicked.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <remarks>
        /// The command removes a specified user from the club.
        /// It requires administrative privileges within the club to execute.
        /// </remarks>
        [Cmd, Aliases, Priority(0)]
        public async Task ClubKick([Remainder] string userName)
        {
            var app = await Service.Kick(ctx.User.Id, userName).ConfigureAwait(false);
            if (app.Item1)
            {
                await ReplyConfirmLocalizedAsync("club_user_kick", Format.Bold(userName),
                    Format.Bold(app.Item2.ToString())).ConfigureAwait(false);
                return;
            }

            await ReplyErrorLocalizedAsync("club_user_kick_fail").ConfigureAwait(false);
        }

        /// <summary>
        /// Bans a user from the club.
        /// </summary>
        /// <param name="user">The user to be banned.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <remarks>
        /// The command bans a specified user from the club, preventing them from rejoining.
        /// It requires club owner privileges to execute.
        /// </remarks>
        [Cmd, Aliases, Priority(1)]
        public Task ClubBan([Remainder] IUser user) => ClubBan(user.ToString());

        /// <summary>
        /// Bans a user from the club.
        /// </summary>
        /// <param name="userName">The name of the user to be banned.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <remarks>
        /// The command bans a specified user from the club, preventing them from rejoining.
        /// It requires club owner privileges to execute.
        /// </remarks>
        [Cmd, Aliases, Priority(0)]
        public async Task ClubBan([Remainder] string userName)
        {
            var ban = await Service.Ban(ctx.User.Id, userName).ConfigureAwait(false);
            if (ban.Item1)
            {
                await ReplyConfirmLocalizedAsync("club_user_banned", Format.Bold(userName),
                    Format.Bold(ban.Item2.ToString())).ConfigureAwait(false);
                return;
            }

            await ReplyErrorLocalizedAsync("club_user_ban_fail").ConfigureAwait(false);
        }

        /// <summary>
        /// Unbans a user from the club.
        /// </summary>
        /// <param name="user">The user to be unbanned.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <remarks>
        /// The command removes a ban from a specified user, allowing them to rejoin the club or apply again.
        /// It requires club owner privileges to execute.
        /// </remarks>
        [Cmd, Aliases, Priority(1)]
        public Task ClubUnBan([Remainder] IUser user) => ClubUnBan(user.ToString());

        /// <summary>
        /// Unbans a user from the club.
        /// </summary>
        /// <param name="userName">The name of the user to be unbanned.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <remarks>
        /// The command removes a ban from a specified user, allowing them to rejoin the club or apply again.
        /// It requires club owner privileges to execute.
        /// </remarks>
        [Cmd, Aliases, Priority(0)]
        public async Task ClubUnBan([Remainder] string userName)
        {
            var unban = await Service.UnBan(ctx.User.Id, userName).ConfigureAwait(false);
            if (unban.Item1)
            {
                await ReplyConfirmLocalizedAsync("club_user_unbanned", Format.Bold(userName),
                    Format.Bold(unban.Item2.ToString())).ConfigureAwait(false);
                return;
            }

            await ReplyErrorLocalizedAsync("club_user_unban_fail").ConfigureAwait(false);
        }

        /// <summary>
        /// Sets the minimum level requirement for joining the club.
        /// </summary>
        /// <param name="level">The level requirement for joining the club.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <remarks>
        /// The command updates the club's level requirement for new members.
        /// It is used to ensure that only users of a certain level can join.
        /// </remarks>
        [Cmd, Aliases]
        public async Task ClubLevelReq(int level)
        {
            var req = await Service.ChangeClubLevelReq(ctx.User.Id, level).ConfigureAwait(false);
            if (req)
            {
                await ReplyConfirmLocalizedAsync("club_level_req_changed", Format.Bold(level.ToString()))
                    .ConfigureAwait(false);
            }
            else
            {
                await ReplyErrorLocalizedAsync("club_level_req_change_error").ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Updates the description of the club.
        /// </summary>
        /// <param name="desc">The new description for the club. If null, the description is removed.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <remarks>
        /// The command allows for the modification of the club's public description.
        /// It requires club owner privileges to execute.
        /// </remarks>
        [Cmd, Aliases]
        public async Task ClubDescription([Remainder] string? desc = null)
        {
            if (await Service.ChangeClubDescription(ctx.User.Id, desc).ConfigureAwait(false))
            {
                await ReplyConfirmLocalizedAsync("club_desc_updated", Format.Bold(desc ?? "-"))
                    .ConfigureAwait(false);
            }
            else
            {
                await ReplyErrorLocalizedAsync("club_desc_update_failed").ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Disbands the club, removing all members and deleting it.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <remarks>
        /// The command permanently deletes the club and notifies the owner of the outcome.
        /// It requires club owner privileges to execute.
        /// </remarks>
        [Cmd, Aliases]
        public async Task ClubDisband()
        {
            var disband = await Service.Disband(ctx.User.Id).ConfigureAwait(false);
            if (disband.Item1)
            {
                await ReplyConfirmLocalizedAsync("club_disbanded", Format.Bold(disband.Item2.ToString()))
                    .ConfigureAwait(false);
            }
            else
            {
                await ReplyErrorLocalizedAsync("club_disband_error").ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Displays the leaderboard of clubs based on their total XP.
        /// </summary>
        /// <param name="page">The page number of the leaderboard to display.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <remarks>
        /// The command fetches and displays a page of the club leaderboard, sorted by club XP.
        /// It provides a view into the most active and engaged clubs within the community.
        /// </remarks>
        [Cmd, Aliases]
        public async Task ClubLeaderboard(int page = 1)
        {
            if (--page < 0)
                return;

            var clubs = await Service.GetClubLeaderboardPage(page);

            var embed = new EmbedBuilder()
                .WithTitle(GetText("club_leaderboard", page + 1))
                .WithOkColor();

            var i = page * 9;
            foreach (var club in clubs) embed.AddField($"#{++i} {club}", $"{club.Xp} xp");

            await ctx.Channel.EmbedAsync(embed);
        }
    }
}