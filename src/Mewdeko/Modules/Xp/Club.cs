using System.Threading.Tasks;
using Discord.Commands;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Xp.Common;
using Mewdeko.Modules.Xp.Services;

namespace Mewdeko.Modules.Xp;

public partial class Xp
{
    [Group]
    public class Club : MewdekoSubmodule<ClubService>
    {
        private readonly InteractiveService interactivity;

        public Club(InteractiveService serv) => interactivity = serv;

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
                    if (x.IsClubAdmin)
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

            await interactivity.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60)).ConfigureAwait(false);

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
                            if (x.IsClubAdmin)
                                return $"{x}⭐{lvlStr}";
                            return x + lvlStr;
                        })));

                return Uri.IsWellFormedUriString(club.ImageUrl, UriKind.Absolute) ? embed.WithThumbnailUrl(club.ImageUrl) : embed;
            }
        }

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

            await interactivity.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60)).ConfigureAwait(false);

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

            await interactivity.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60)).ConfigureAwait(false);

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

        [Cmd, Aliases, Priority(1)]
        public Task ClubAccept(IUser user) => ClubAccept(user.ToString());

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

        [Cmd, Aliases]
        public async Task Clubleave()
        {
            if (await Service.LeaveClub(ctx.User).ConfigureAwait(false))
                await ReplyConfirmLocalizedAsync("club_left").ConfigureAwait(false);
            else
                await ReplyErrorLocalizedAsync("club_not_in_club").ConfigureAwait(false);
        }

        [Cmd, Aliases, Priority(1)]
        public async Task ClubKick([Remainder] IUser user) => await ClubKick(user.ToString()).ConfigureAwait(false);

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

        [Cmd, Aliases, Priority(1)]
        public async Task ClubBan([Remainder] IUser user) => await ClubBan(user.ToString());

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

        [Cmd, Aliases, Priority(1)]
        public async Task ClubUnBan([Remainder] IUser user) => await ClubUnBan(user.ToString()).ConfigureAwait(false);

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