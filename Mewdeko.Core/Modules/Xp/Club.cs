using Discord;
using Discord.Commands;
using Mewdeko.Common.Attributes;
using Mewdeko.Core.Services.Database.Models;
using Mewdeko.Extensions;
using Mewdeko.Modules.Xp.Services;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Mewdeko.Modules.Xp
{
    public partial class Xp
    {
        [Group]
        public class Club : MewdekoSubmodule<ClubService>
        {
            private readonly XpService _xps;

            public Club(XpService xps)
            {
                _xps = xps;
            }

            [MewdekoCommand, Usage, Description, Aliases]
            public async Task ClubTransfer([Leftover] IUser newOwner)
            {
                var club = _service.TransferClub(ctx.User, newOwner);

                if (club != null)
                    await ReplyConfirmLocalizedAsync("club_transfered",
                        Format.Bold(club.Name),
                        Format.Bold(newOwner.ToString())).ConfigureAwait(false);
                else
                    await ReplyErrorLocalizedAsync("club_transfer_failed").ConfigureAwait(false);
            }

            [MewdekoCommand, Usage, Description, Aliases]
            public async Task ClubAdmin([Leftover] IUser toAdmin)
            {
                bool admin;
                try
                {
                    admin = _service.ToggleAdmin(ctx.User, toAdmin);
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

            [MewdekoCommand, Usage, Description, Aliases]
            public async Task ClubCreate([Leftover] string clubName)
            {
                if (string.IsNullOrWhiteSpace(clubName) || clubName.Length > 20)
                {
                    await ReplyErrorLocalizedAsync("club_name_too_long").ConfigureAwait(false);
                    return;
                }

                if (!_service.CreateClub(ctx.User, clubName, out ClubInfo club))
                {
                    await ReplyErrorLocalizedAsync("club_create_error").ConfigureAwait(false);
                    return;
                }

                await ReplyConfirmLocalizedAsync("club_created", Format.Bold(club.ToString())).ConfigureAwait(false);
            }

            [MewdekoCommand, Usage, Description, Aliases]
            public async Task ClubIcon([Leftover] string url = null)
            {
                if ((!Uri.IsWellFormedUriString(url, UriKind.Absolute) && url != null)
                    || !await _service.SetClubIcon(ctx.User.Id, url == null ? null : new Uri(url)))
                {
                    await ReplyErrorLocalizedAsync("club_icon_error").ConfigureAwait(false);
                    return;
                }

                await ReplyConfirmLocalizedAsync("club_icon_set").ConfigureAwait(false);
            }

            [MewdekoCommand, Usage, Description, Aliases]
            [Priority(1)]
            public async Task ClubInformation(IUser user = null)
            {
                user = user ?? ctx.User;
                var club = _service.GetClubByMember(user);
                if (club == null)
                {
                    await ErrorLocalizedAsync("club_user_not_in_club", Format.Bold(user.ToString()));
                    return;
                }

                await ClubInformation(club.ToString()).ConfigureAwait(false);
            }

            [MewdekoCommand, Usage, Description, Aliases]
            [Priority(0)]
            public async Task ClubInformation([Leftover] string clubName = null)
            {
                if (string.IsNullOrWhiteSpace(clubName))
                {
                    await ClubInformation(ctx.User).ConfigureAwait(false);
                    return;
                }

                if (!_service.GetClubByName(clubName, out ClubInfo club))
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
                        else if (x.IsClubAdmin)
                            return int.MaxValue / 2 + l;
                        else
                            return l;
                    });

                await ctx.SendPaginatedConfirmAsync(0, (page) =>
                {
                    var embed = new EmbedBuilder()
                        .WithOkColor()
                        .WithTitle($"{club.ToString()}")
                        .WithDescription(GetText("level_x", lvl.Level) + $" ({club.Xp} xp)")
                        .AddField(GetText("desc"), string.IsNullOrWhiteSpace(club.Description) ? "-" : club.Description,
                            false)
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
                                    return x.ToString() + "🌟" + lvlStr;
                                else if (x.IsClubAdmin)
                                    return x.ToString() + "⭐" + lvlStr;
                                return x.ToString() + lvlStr;
                            })), false);

                    if (Uri.IsWellFormedUriString(club.ImageUrl, UriKind.Absolute))
                        return embed.WithThumbnailUrl(club.ImageUrl);

                    return embed;
                }, club.Users.Count, 10).ConfigureAwait(false);
            }

            [MewdekoCommand, Usage, Description, Aliases]
            public Task ClubBans(int page = 1)
            {
                if (--page < 0)
                    return Task.CompletedTask;

                var club = _service.GetClubWithBansAndApplications(ctx.User.Id);
                if (club == null)
                    return ReplyErrorLocalizedAsync("club_not_exists_owner");

                var bans = club
                    .Bans
                    .Select(x => x.User)
                    .ToArray();

                return ctx.SendPaginatedConfirmAsync(page,
                    curPage =>
                    {
                        var toShow = string.Join("\n", bans
                            .Skip(page * 10)
                            .Take(10)
                            .Select(x => x.ToString()));

                        return new EmbedBuilder()
                            .WithTitle(GetText("club_bans_for", club.ToString()))
                            .WithDescription(toShow)
                            .WithOkColor();
                    }, bans.Length, 10);
            }


            [MewdekoCommand, Usage, Description, Aliases]
            public Task ClubApps(int page = 1)
            {
                if (--page < 0)
                    return Task.CompletedTask;

                var club = _service.GetClubWithBansAndApplications(ctx.User.Id);
                if (club == null)
                    return ReplyErrorLocalizedAsync("club_not_exists_owner");

                var apps = club
                    .Applicants
                    .Select(x => x.User)
                    .ToArray();

                return ctx.SendPaginatedConfirmAsync(page,
                    curPage =>
                    {
                        var toShow = string.Join("\n", apps
                            .Skip(page * 10)
                            .Take(10)
                            .Select(x => x.ToString()));

                        return new EmbedBuilder()
                            .WithTitle(GetText("club_apps_for", club.ToString()))
                            .WithDescription(toShow)
                            .WithOkColor();
                    }, apps.Length, 10);
            }

            [MewdekoCommand, Usage, Description, Aliases]
            public async Task ClubApply([Leftover] string clubName)
            {
                if (string.IsNullOrWhiteSpace(clubName))
                    return;

                if (!_service.GetClubByName(clubName, out ClubInfo club))
                {
                    await ReplyErrorLocalizedAsync("club_not_exists").ConfigureAwait(false);
                    return;
                }

                if (_service.ApplyToClub(ctx.User, club))
                {
                    await ReplyConfirmLocalizedAsync("club_applied", Format.Bold(club.ToString()))
                        .ConfigureAwait(false);
                }
                else
                {
                    await ReplyErrorLocalizedAsync("club_apply_error").ConfigureAwait(false);
                }
            }

            [MewdekoCommand, Usage, Description, Aliases]
            [Priority(1)]
            public Task ClubAccept(IUser user)
                => ClubAccept(user.ToString());

            [MewdekoCommand, Usage, Description, Aliases]
            [Priority(0)]
            public async Task ClubAccept([Leftover] string userName)
            {
                if (_service.AcceptApplication(ctx.User.Id, userName, out var discordUser))
                {
                    await ReplyConfirmLocalizedAsync("club_accepted", Format.Bold(discordUser.ToString()))
                        .ConfigureAwait(false);
                }
                else
                    await ReplyErrorLocalizedAsync("club_accept_error").ConfigureAwait(false);
            }

            [MewdekoCommand, Usage, Description, Aliases]
            public async Task Clubleave()
            {
                if (_service.LeaveClub(ctx.User))
                    await ReplyConfirmLocalizedAsync("club_left").ConfigureAwait(false);
                else
                    await ReplyErrorLocalizedAsync("club_not_in_club").ConfigureAwait(false);
            }

            [MewdekoCommand, Usage, Description, Aliases]
            [Priority(1)]
            public Task ClubKick([Leftover] IUser user)
                => ClubKick(user.ToString());

            [MewdekoCommand, Usage, Description, Aliases]
            [Priority(0)]
            public Task ClubKick([Leftover] string userName)
            {
                if (_service.Kick(ctx.User.Id, userName, out var club))
                    return ReplyConfirmLocalizedAsync("club_user_kick", Format.Bold(userName),
                        Format.Bold(club.ToString()));
                else
                    return ReplyErrorLocalizedAsync("club_user_kick_fail");
            }

            [MewdekoCommand, Usage, Description, Aliases]
            [Priority(1)]
            public Task ClubBan([Leftover] IUser user)
                => ClubBan(user.ToString());

            [MewdekoCommand, Usage, Description, Aliases]
            [Priority(0)]
            public Task ClubBan([Leftover] string userName)
            {
                if (_service.Ban(ctx.User.Id, userName, out var club))
                    return ReplyConfirmLocalizedAsync("club_user_banned", Format.Bold(userName),
                        Format.Bold(club.ToString()));
                else
                    return ReplyErrorLocalizedAsync("club_user_ban_fail");
            }

            [MewdekoCommand, Usage, Description, Aliases]
            [Priority(1)]
            public Task ClubUnBan([Leftover] IUser user)
                => ClubUnBan(user.ToString());

            [MewdekoCommand, Usage, Description, Aliases]
            [Priority(0)]
            public Task ClubUnBan([Leftover] string userName)
            {
                if (_service.UnBan(ctx.User.Id, userName, out var club))
                    return ReplyConfirmLocalizedAsync("club_user_unbanned", Format.Bold(userName),
                        Format.Bold(club.ToString()));
                else
                    return ReplyErrorLocalizedAsync("club_user_unban_fail");
            }

            [MewdekoCommand, Usage, Description, Aliases]
            public async Task ClubLevelReq(int level)
            {
                if (_service.ChangeClubLevelReq(ctx.User.Id, level))
                {
                    await ReplyConfirmLocalizedAsync("club_level_req_changed", Format.Bold(level.ToString()))
                        .ConfigureAwait(false);
                }
                else
                {
                    await ReplyErrorLocalizedAsync("club_level_req_change_error").ConfigureAwait(false);
                }
            }

            [MewdekoCommand, Usage, Description, Aliases]
            public async Task ClubDescription([Leftover] string desc = null)
            {
                if (_service.ChangeClubDescription(ctx.User.Id, desc))
                {
                    await ReplyConfirmLocalizedAsync("club_desc_updated", Format.Bold(desc ?? "-"))
                        .ConfigureAwait(false);
                }
                else
                {
                    await ReplyErrorLocalizedAsync("club_desc_update_failed").ConfigureAwait(false);
                }
            }

            [MewdekoCommand, Usage, Description, Aliases]
            public async Task ClubDisband()
            {
                if (_service.Disband(ctx.User.Id, out ClubInfo club))
                {
                    await ReplyConfirmLocalizedAsync("club_disbanded", Format.Bold(club.ToString()))
                        .ConfigureAwait(false);
                }
                else
                {
                    await ReplyErrorLocalizedAsync("club_disband_error").ConfigureAwait(false);
                }
            }

            [MewdekoCommand, Usage, Description, Aliases]
            public Task ClubLeaderboard(int page = 1)
            {
                if (--page < 0)
                    return Task.CompletedTask;

                var clubs = _service.GetClubLeaderboardPage(page);

                var embed = new EmbedBuilder()
                    .WithTitle(GetText("club_leaderboard", page + 1))
                    .WithOkColor();

                var i = page * 9;
                foreach (var club in clubs)
                {
                    embed.AddField($"#{++i} " + club.ToString(), club.Xp.ToString() + " xp", false);
                }

                return ctx.Channel.EmbedAsync(embed);
            }
        }
    }
}