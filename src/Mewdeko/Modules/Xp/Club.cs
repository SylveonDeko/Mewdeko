using Discord;
using Discord.Commands;
using Mewdeko._Extensions;
using Mewdeko.Common;
using Mewdeko.Common.Attributes;
using Mewdeko.Common.Extensions.Interactive;
using Mewdeko.Common.Extensions.Interactive.Entities.Page;
using Mewdeko.Common.Extensions.Interactive.Pagination;
using Mewdeko.Common.Extensions.Interactive.Pagination.Lazy;
using Mewdeko.Modules.Xp.Common;
using Mewdeko.Modules.Xp.Services;

namespace Mewdeko.Modules.Xp;

public partial class Xp
{
    [Group]
    public class Club : MewdekoSubmodule<ClubService>
    {
        private readonly XpService _xps;
        private readonly InteractiveService Interactivity;

        public Club(XpService xps, InteractiveService serv)
        {
            _xps = xps;
            Interactivity = serv;
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        public async Task ClubTransfer([Remainder] IUser newOwner)
        {
            var club = Service.TransferClub(ctx.User, newOwner);

            if (club != null)
                await ReplyConfirmLocalizedAsync("club_transfered",
                    Format.Bold(club.Name),
                    Format.Bold(newOwner.ToString())).ConfigureAwait(false);
            else
                await ReplyErrorLocalizedAsync("club_transfer_failed").ConfigureAwait(false);
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        public async Task ClubAdmin([Remainder] IUser toAdmin)
        {
            bool admin;
            try
            {
                admin = Service.ToggleAdmin(ctx.User, toAdmin);
            }
            catch (InvalidOperationException)
            {
                await ReplyErrorLocalizedAsync("club_admin_error").ConfigureAwait(false);
                return;
            }

            if (admin)
                await ReplyConfirmLocalizedAsync("club_admin_add", Format.Bold(toAdmin.ToString()))
                    .ConfigureAwait(false);
            else
                await ReplyConfirmLocalizedAsync("club_admin_remove", Format.Bold(toAdmin.ToString()))
                    .ConfigureAwait(false);
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        public async Task ClubCreate([Remainder] string clubName)
        {
            if (string.IsNullOrWhiteSpace(clubName) || clubName.Length > 20)
            {
                await ReplyErrorLocalizedAsync("club_name_too_long").ConfigureAwait(false);
                return;
            }

            if (!Service.CreateClub(ctx.User, clubName, out var club))
            {
                await ReplyErrorLocalizedAsync("club_create_error").ConfigureAwait(false);
                return;
            }

            await ReplyConfirmLocalizedAsync("club_created", Format.Bold(club.ToString())).ConfigureAwait(false);
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        public async Task ClubIcon([Remainder] string url = null)
        {
            if (!Uri.IsWellFormedUriString(url, UriKind.Absolute) && url != null
                || !await Service.SetClubIcon(ctx.User.Id, url == null ? null : new Uri(url)))
            {
                await ReplyErrorLocalizedAsync("club_icon_error").ConfigureAwait(false);
                return;
            }

            await ReplyConfirmLocalizedAsync("club_icon_set").ConfigureAwait(false);
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [Priority(1)]
        public async Task ClubInformation(IUser user = null)
        {
            user ??= ctx.User;
            var club = Service.GetClubByMember(user);
            if (club == null)
            {
                await ErrorLocalizedAsync("club_user_not_in_club", Format.Bold(user.ToString()));
                return;
            }

            await ClubInformation(club.ToString()).ConfigureAwait(false);
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [Priority(0)]
        public async Task ClubInformation([Remainder] string clubName = null)
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
                        return int.MaxValue / 2 + l;
                    return l;
                });

            var paginator = new LazyPaginatorBuilder()
                .AddUser(ctx.User)
                .WithPageFactory(PageFactory)
                .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                .WithMaxPageIndex(club.Users.Count / 10)
                .WithDefaultEmotes()
                .Build();

            await Interactivity.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60));

            Task<PageBuilder> PageFactory(int page)
            {
                var embed = new PageBuilder()
                    .WithOkColor()
                    .WithTitle($"{club}")
                    .WithDescription(GetText("level_x", lvl.Level) + $" ({club.Xp} xp)")
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
                                return x + "🌟" + lvlStr;
                            if (x.IsClubAdmin)
                                return x + "⭐" + lvlStr;
                            return x + lvlStr;
                        })));

                if (Uri.IsWellFormedUriString(club.ImageUrl, UriKind.Absolute))
                    return Task.FromResult(embed.WithThumbnailUrl(club.ImageUrl));

                return Task.FromResult(embed);
            }
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        public async Task ClubBans(int page = 1)
        {
            if (--page < 0)
                return;

            var club = Service.GetClubWithBansAndApplications(ctx.User.Id);
            if (club == null)
            {
                await ReplyErrorLocalizedAsync("club_not_exists_owner");
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
                .Build();

            await Interactivity.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60));

            Task<PageBuilder> PageFactory(int page)
            {
                var toShow = string.Join("\n", bans
                    .Skip(page * 10)
                    .Take(10)
                    .Select(x => x.ToString()));

                return Task.FromResult(new PageBuilder()
                    .WithTitle(GetText("club_bans_for", club.ToString()))
                    .WithDescription(toShow)
                    .WithOkColor());
            }
        }


        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        public async Task ClubApps(int page = 1)
        {
            if (--page < 0)
                return;

            var club = Service.GetClubWithBansAndApplications(ctx.User.Id);
            if (club == null)
            {
                await ReplyErrorLocalizedAsync("club_not_exists_owner");
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
                .Build();

            await Interactivity.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60));

            Task<PageBuilder> PageFactory(int page)
            {
                var toShow = string.Join("\n", apps
                    .Skip(page * 10)
                    .Take(10)
                    .Select(x => x.ToString()));

                return Task.FromResult(new PageBuilder()
                    .WithTitle(GetText("club_apps_for", club.ToString()))
                    .WithDescription(toShow)
                    .WithOkColor());
            }
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        public async Task ClubApply([Remainder] string clubName)
        {
            if (string.IsNullOrWhiteSpace(clubName))
                return;

            if (!Service.GetClubByName(clubName, out var club))
            {
                await ReplyErrorLocalizedAsync("club_not_exists").ConfigureAwait(false);
                return;
            }

            if (Service.ApplyToClub(ctx.User, club))
                await ReplyConfirmLocalizedAsync("club_applied", Format.Bold(club.ToString()))
                    .ConfigureAwait(false);
            else
                await ReplyErrorLocalizedAsync("club_apply_error").ConfigureAwait(false);
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [Priority(1)]
        public Task ClubAccept(IUser user) => ClubAccept(user.ToString());

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [Priority(0)]
        public async Task ClubAccept([Remainder] string userName)
        {
            if (Service.AcceptApplication(ctx.User.Id, userName, out var discordUser))
                await ReplyConfirmLocalizedAsync("club_accepted", Format.Bold(discordUser.ToString()))
                    .ConfigureAwait(false);
            else
                await ReplyErrorLocalizedAsync("club_accept_error").ConfigureAwait(false);
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        public async Task Clubleave()
        {
            if (Service.LeaveClub(ctx.User))
                await ReplyConfirmLocalizedAsync("club_left").ConfigureAwait(false);
            else
                await ReplyErrorLocalizedAsync("club_not_in_club").ConfigureAwait(false);
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [Priority(1)]
        public Task ClubKick([Remainder] IUser user) => ClubKick(user.ToString());

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [Priority(0)]
        public Task ClubKick([Remainder] string userName)
        {
            if (Service.Kick(ctx.User.Id, userName, out var club))
                return ReplyConfirmLocalizedAsync("club_user_kick", Format.Bold(userName),
                    Format.Bold(club.ToString()));
            return ReplyErrorLocalizedAsync("club_user_kick_fail");
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [Priority(1)]
        public Task ClubBan([Remainder] IUser user) => ClubBan(user.ToString());

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [Priority(0)]
        public Task ClubBan([Remainder] string userName)
        {
            if (Service.Ban(ctx.User.Id, userName, out var club))
                return ReplyConfirmLocalizedAsync("club_user_banned", Format.Bold(userName),
                    Format.Bold(club.ToString()));
            return ReplyErrorLocalizedAsync("club_user_ban_fail");
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [Priority(1)]
        public Task ClubUnBan([Remainder] IUser user) => ClubUnBan(user.ToString());

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [Priority(0)]
        public Task ClubUnBan([Remainder] string userName)
        {
            if (Service.UnBan(ctx.User.Id, userName, out var club))
                return ReplyConfirmLocalizedAsync("club_user_unbanned", Format.Bold(userName),
                    Format.Bold(club.ToString()));
            return ReplyErrorLocalizedAsync("club_user_unban_fail");
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        public async Task ClubLevelReq(int level)
        {
            if (Service.ChangeClubLevelReq(ctx.User.Id, level))
                await ReplyConfirmLocalizedAsync("club_level_req_changed", Format.Bold(level.ToString()))
                    .ConfigureAwait(false);
            else
                await ReplyErrorLocalizedAsync("club_level_req_change_error").ConfigureAwait(false);
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        public async Task ClubDescription([Remainder] string desc = null)
        {
            if (Service.ChangeClubDescription(ctx.User.Id, desc))
                await ReplyConfirmLocalizedAsync("club_desc_updated", Format.Bold(desc ?? "-"))
                    .ConfigureAwait(false);
            else
                await ReplyErrorLocalizedAsync("club_desc_update_failed").ConfigureAwait(false);
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        public async Task ClubDisband()
        {
            if (Service.Disband(ctx.User.Id, out var club))
                await ReplyConfirmLocalizedAsync("club_disbanded", Format.Bold(club.ToString()))
                    .ConfigureAwait(false);
            else
                await ReplyErrorLocalizedAsync("club_disband_error").ConfigureAwait(false);
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        public Task ClubLeaderboard(int page = 1)
        {
            if (--page < 0)
                return Task.CompletedTask;

            var clubs = Service.GetClubLeaderboardPage(page);

            var embed = new EmbedBuilder()
                .WithTitle(GetText("club_leaderboard", page + 1))
                .WithOkColor();

            var i = page * 9;
            foreach (var club in clubs) embed.AddField($"#{++i} " + club, club.Xp + " xp");

            return ctx.Channel.EmbedAsync(embed);
        }
    }
}