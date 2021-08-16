using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Mewdeko.Common;
using Mewdeko.Common.Attributes;
using Mewdeko.Core.Common;
using Mewdeko.Core.Common.TypeReaders.Models;
using Mewdeko.Core.Services;
using Mewdeko.Core.Services.Impl;
using Mewdeko.Extensions;
using Mewdeko.Modules.Utility.Services;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Humanizer;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace Mewdeko.Modules.Utility
{
    public partial class Utility : MewdekoModule<UtilityService>
    {
        private static readonly SemaphoreSlim sem = new(1, 1);
        private readonly Mewdeko _bot;
        private readonly DiscordSocketClient _client;
        private readonly IBotCredentials _creds;
        private readonly IStatsService _stats;
        private readonly DownloadTracker _tracker;

        public Utility(Mewdeko Mewdeko, DiscordSocketClient client,
            IStatsService stats, IBotCredentials creds, DownloadTracker tracker)
        {
            _client = client;
            _stats = stats;
            _creds = creds;
            _bot = Mewdeko;
            _tracker = tracker;
        }
        //public async Task GetShopInfo()
        //{

        //}
        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        public async Task Invite()
        {
            await ctx.Channel.SendConfirmAsync("Invite me using this link:\nhttps://mewdeko.tech/invite");
        }
        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        public async Task TestSite(string url)
        {
            using (HttpClient client = new HttpClient())
            {
                HttpResponseMessage response = await client.GetAsync(url);

                string content = await response.Content.ReadAsStringAsync();
                var statusCode = response.StatusCode;
                if (statusCode.ToString() == "Forbidden")
                {
                    await ctx.Channel.SendErrorAsync("Sites down m8");
                }
                else
                {
                    await ctx.Channel.SendConfirmAsync("Sites ok m8");
                }
            }
        }
        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        public async Task Request ([Remainder]string _)
        {
            var list = new List<string>();
            list.Add("No piransi");
            list.Add("<:omegalul2:873626819331498024>");
            var random = new Random();
            int index = random.Next(list.Count());
            await ctx.Channel.SendErrorAsync(list[index]);
        }
        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [UserPerm(GuildPerm.ManageChannels)]
        public async Task ReactChannel(ITextChannel chan = null)
        {
            var e = _service.GetReactChans(ctx.Guild.Id);
            if (chan == null)
            {
                if (e == 0) return;
                await _service.SetReactChan(ctx.Guild, 0);
                ;
                await ctx.Channel.SendConfirmAsync("React Channel Disabled!");
            }
            else
            {
                if (e == 0)
                {
                    await _service.SetReactChan(ctx.Guild, chan.Id);
                    await ctx.Channel.SendConfirmAsync($"Your React Channel has been set to {chan.Mention}!");
                }
                else
                {
                    var chan2 = await ctx.Guild.GetTextChannelAsync(e);
                    if (e == chan.Id)
                    {
                        await ctx.Channel.SendErrorAsync("This is already your React Channel!");
                    }
                    else
                    {
                        await _service.SetReactChan(ctx.Guild, chan.Id);
                        await ctx.Channel.SendConfirmAsync(
                            $"Your React Channel has been switched from {chan2.Mention} to {chan.Mention}!");
                    }
                }
            }
        }
        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [UserPerm(GuildPerm.Administrator)]
        [RequireContext(ContextType.Guild)]
        public async Task SnipeSet(string yesnt)
        {
            await _service.SnipeSet(ctx.Guild, yesnt);
            var t = _service.GetSnipeSet(ctx.Guild.Id);
            switch (t)
            {
                case 1:
                    await ctx.Channel.SendConfirmAsync("Sniping Enabled!");
                    break;
                case 0:
                    await ctx.Channel.SendConfirmAsync("Sniping Disabled!");
                    break;
            }
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Snipe()
        {
            if (_service.GetSnipeSet(ctx.Guild.Id) == 0)
            {
                await ctx.Channel.SendErrorAsync(
                    $"Sniping is not enabled in this server! Use `{Prefix}snipeset enable` to enable it!");
                return;
            }

            var msgs = _service.Snipemsg(ctx.Guild.Id, ctx.Channel.Id);
            {
                if (!msgs.Any() || msgs == null)
                {
                    await ctx.Channel.SendErrorAsync("There's nothing to snipe!");
                    return;
                }

                var msg = msgs.OrderByDescending(d => d.DateAdded).Where(x => x.Edited == 0).Select(x => x.Message)
                    .First();
                var tstamp = msgs.OrderByDescending(d => d.DateAdded).Where(x => x.Edited == 0).Select(x => x.DateAdded)
                   .First();
                var userid = msgs.OrderByDescending(d => d.DateAdded).Where(x => x.Edited == 0).Select(x => x.UserId)
                    .First();
                var user = await ctx.Channel.GetUserAsync(userid);

                var em = new EmbedBuilder
                {
                    Author = new EmbedAuthorBuilder
                    {
                        IconUrl = user.GetAvatarUrl(),
                        Name = $"{user} said:"
                    },
                    Description = msg,
                    Footer = new EmbedFooterBuilder
                    {
                        IconUrl = ctx.User.GetAvatarUrl(),
                        Text = $"Snipe requested by {ctx.User} || Message deleted {(DateTime.UtcNow - tstamp.Value).Humanize()} ago"
                    },
                    Color = Mewdeko.OkColor
                };
                await ctx.Channel.SendMessageAsync("", embed: em.Build());
            }
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [Priority(1)]
        public async Task Snipe(IUser user1)
        {
            if (_service.GetSnipeSet(ctx.Guild.Id) == 0)
            {
                await ctx.Channel.SendErrorAsync(
                    $"Sniping is not enabled in this server! Use `{Prefix}snipeset enable` to enable it!");
                return;
            }

            var msgs = _service.Snipemsg(ctx.Guild.Id, ctx.Channel.Id).Where(x => x.UserId == user1.Id);
            {
                if (!msgs.Any() || msgs == null)
                {
                    await ctx.Channel.SendErrorAsync("There's nothing to snipe for this user!");
                    return;
                }

                var msg = msgs.OrderByDescending(d => d.DateAdded).Where(x => x.Edited == 0)
                    .Where(x => x.UserId == user1.Id).Select(x => x.Message).First();
                var userid = msgs.OrderByDescending(d => d.DateAdded).Where(x => x.Edited == 0)
                    .Where(x => x.UserId == user1.Id).Select(x => x.UserId).First();
                var tstamp = msgs.OrderByDescending(d => d.DateAdded).Where(x => x.Edited == 0)
                    .Where(x => x.UserId == user1.Id).Select(x => x.DateAdded).First();
                var user = await ctx.Channel.GetUserAsync(userid);

                var em = new EmbedBuilder
                {
                    Author = new EmbedAuthorBuilder
                    {
                        IconUrl = user.GetAvatarUrl(),
                        Name = $"{user} said:"
                    },
                    Description = msg,
                    Footer = new EmbedFooterBuilder
                    {
                        IconUrl = ctx.User.GetAvatarUrl(),
                        Text = $"User specific snipe requested by {ctx.User} || Message deleted {(DateTime.UtcNow - tstamp.Value).Humanize()} ago"
                    },
                    Color = Mewdeko.OkColor
                };
                await ctx.Channel.SendMessageAsync("", embed: em.Build());
            }
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [Priority(2)]
        public async Task Snipe(ITextChannel chan)
        {
            if (_service.GetSnipeSet(ctx.Guild.Id) == 0)
            {
                await ctx.Channel.SendErrorAsync(
                    $"Sniping is not enabled in this server! Use `{Prefix}snipeset enable` to enable it!");
                return;
            }

            var msgs = _service.Snipemsg(ctx.Guild.Id, chan.Id);
            {
                if (!msgs.Any() || msgs == null)
                {
                    await ctx.Channel.SendErrorAsync("There's nothing to snipe for that channel!");
                    return;
                }

                var msg = msgs.OrderByDescending(d => d.DateAdded).Where(x => x.Edited == 0).Select(x => x.Message)
                    .First();
                var userid = msgs.OrderByDescending(d => d.DateAdded).Where(x => x.Edited == 0).Select(x => x.UserId)
                    .First();
                var tstamp = msgs.OrderByDescending(d => d.DateAdded).Where(x => x.Edited == 0).Select(x => x.DateAdded)
                    .First();
                var user = await ctx.Channel.GetUserAsync(userid);

                var em = new EmbedBuilder
                {
                    Author = new EmbedAuthorBuilder
                    {
                        IconUrl = user.GetAvatarUrl(),
                        Name = $"{user} said:"
                    },
                    Description = msg,
                    Footer = new EmbedFooterBuilder
                    {
                        IconUrl = ctx.User.GetAvatarUrl(),
                        Text = $"Channel specific snipe requested by {ctx.User} || Message deleted {(DateTime.UtcNow - tstamp.Value).Humanize()} ago"
                    },
                    Color = Mewdeko.OkColor
                };
                await ctx.Channel.SendMessageAsync("", embed: em.Build());
            }
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [Priority(2)]
        public async Task Snipe(ITextChannel chan, IUser user1)
        {
            if (_service.GetSnipeSet(ctx.Guild.Id) == 0)
            {
                await ctx.Channel.SendErrorAsync(
                    $"Sniping is not enabled in this server! Use `{Prefix}snipeset enable` to enable it!");
                return;
            }

            var msgs = _service.Snipemsg(ctx.Guild.Id, chan.Id);
            {
                if (!msgs.Any() || msgs == null)
                {
                    await ctx.Channel.SendErrorAsync("There's nothing to snipe for that channel and user!");
                    return;
                }

                var msg = msgs.OrderByDescending(d => d.DateAdded).Where(x => x.Edited == 0)
                    .Where(x => x.UserId == user1.Id).Select(x => x.Message).First();
                var userid = msgs.OrderByDescending(d => d.DateAdded).Where(x => x.Edited == 0)
                    .Where(x => x.UserId == user1.Id).Select(x => x.UserId).First();
                var tstamp = msgs.OrderByDescending(d => d.DateAdded).Where(x => x.Edited == 0)
                    .Where(x => x.UserId == user1.Id).Select(x => x.DateAdded).First();
                var user = await ctx.Channel.GetUserAsync(userid);

                var em = new EmbedBuilder
                {
                    Author = new EmbedAuthorBuilder
                    {
                        IconUrl = user.GetAvatarUrl(),
                        Name = $"{user} said:"
                    },
                    Description = msg,
                    Footer = new EmbedFooterBuilder
                    {
                        IconUrl = ctx.User.GetAvatarUrl(),
                        Text = $"Channel and user specific snipe requested by {ctx.User} || Message deleted {(DateTime.UtcNow - tstamp.Value).Humanize()} ago"
                    },
                    Color = Mewdeko.OkColor
                };
                await ctx.Channel.SendMessageAsync("", embed: em.Build());
            }
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [UserPerm(GuildPerm.Administrator)]
        [RequireContext(ContextType.Guild)]
        public async Task PreviewLinks(string yesnt)
        {
            await _service.PreviewLinks(ctx.Guild, yesnt.Substring(0, 1).ToLower());
            var t = _service.GetPLinks(ctx.Guild.Id);
            switch (t)
            {
                case 1:
                    await ctx.Channel.SendConfirmAsync("Link previews are now enabled!");
                    break;
                case 0:
                    await ctx.Channel.SendConfirmAsync("Link Previews are now disabled!");
                    break;
            }
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task EditSnipe()
        {
            if (_service.GetSnipeSet(ctx.Guild.Id) == 0)
            {
                await ctx.Channel.SendErrorAsync(
                    $"Sniping != enabled in this server! Use `{Prefix}snipeset enable` to enable it!");
                return;
            }

            {
                var msgs = _service.Snipemsg(ctx.Guild.Id, ctx.Channel.Id);
                if (!msgs.Any() || msgs == null)
                {
                    await ctx.Channel.SendErrorAsync("There's nothing to snipe!");
                    return;
                }

                var msg = msgs.OrderByDescending(d => d.DateAdded).Where(m => m.Edited == 1).Select(x => x.Message)
                    .First();
                var userid = msgs.OrderByDescending(d => d.DateAdded).Where(m => m.Edited == 1).Select(x => x.UserId)
                    .First();
                var tstamp = msgs.OrderByDescending(d => d.DateAdded).Where(m => m.Edited == 1).Select(x => x.DateAdded)
                   .First();
                var user = await ctx.Channel.GetUserAsync(userid);

                var em = new EmbedBuilder
                {
                    Author = new EmbedAuthorBuilder
                    {
                        IconUrl = user.GetAvatarUrl(),
                        Name = $"{user} originally said:"
                    },
                    Description = msg,
                    Footer = new EmbedFooterBuilder
                    {
                        IconUrl = ctx.User.GetAvatarUrl(),
                        Text = $"Edit snipe requested by {ctx.User} || Message edited {(DateTime.UtcNow - tstamp.Value).Humanize()} ago"
                    },
                    Color = Mewdeko.OkColor
                };
                await ctx.Channel.SendMessageAsync("", embed: em.Build());
            }
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [Priority(1)]
        public async Task EditSnipe(IUser user1)
        {
            if (_service.GetSnipeSet(ctx.Guild.Id) == 0)
            {
                await ctx.Channel.SendErrorAsync(
                    $"Sniping != enabled in this server! Use `{Prefix}snipeset enable` to enable it!");
                return;
            }

            {
                var msgs = _service.Snipemsg(ctx.Guild.Id, ctx.Channel.Id).Where(x => x.UserId == user1.Id);
                if (!msgs.Any() || msgs == null)
                {
                    await ctx.Channel.SendErrorAsync("There's nothing to snipe for that user!");
                    return;
                }

                var msg = msgs.OrderByDescending(d => d.DateAdded).Where(m => m.Edited == 1)
                    .Where(x => x.UserId == user1.Id).Select(x => x.Message).First();
                var userid = msgs.OrderByDescending(d => d.DateAdded).Where(m => m.Edited == 1)
                    .Where(x => x.UserId == user1.Id).Select(x => x.UserId).First();
                var tstamp = msgs.OrderByDescending(d => d.DateAdded).Where(m => m.Edited == 1)
                    .Where(x => x.UserId == user1.Id).Select(x => x.DateAdded).First();
                var user = await ctx.Channel.GetUserAsync(userid);

                var em = new EmbedBuilder
                {
                    Author = new EmbedAuthorBuilder
                    {
                        IconUrl = user.GetAvatarUrl(),
                        Name = $"{user} originally said:"
                    },
                    Description = msg,
                    Footer = new EmbedFooterBuilder
                    {
                        IconUrl = ctx.User.GetAvatarUrl(),
                        Text = $"Edit snipe requested by {ctx.User} || Message edited {(DateTime.UtcNow - tstamp.Value).Humanize()} ago"
                    },
                    Color = Mewdeko.OkColor
                };
                await ctx.Channel.SendMessageAsync("", embed: em.Build());
            }
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [Priority(1)]
        public async Task EditSnipe(ITextChannel chan)
        {
            if (_service.GetSnipeSet(ctx.Guild.Id) == 0)
            {
                await ctx.Channel.SendErrorAsync(
                    $"Sniping != enabled in this server! Use `{Prefix}snipeset enable` to enable it!");
                return;
            }

            {
                var msgs = _service.Snipemsg(ctx.Guild.Id, chan.Id);
                if (!msgs.Any() || msgs == null)
                {
                    await ctx.Channel.SendErrorAsync("There's nothing to snipe for that channel!");
                    return;
                }

                var msg = msgs.OrderByDescending(d => d.DateAdded).Where(m => m.Edited == 1).Select(x => x.Message)
                    .First();
                var userid = msgs.OrderByDescending(d => d.DateAdded).Where(m => m.Edited == 1).Select(x => x.UserId)
                    .First();
                var tstamp = msgs.OrderByDescending(d => d.DateAdded).Where(m => m.Edited == 1).Select(x => x.DateAdded)
                    .First();
                var user = await ctx.Channel.GetUserAsync(userid);

                var em = new EmbedBuilder
                {
                    Author = new EmbedAuthorBuilder
                    {
                        IconUrl = user.GetAvatarUrl(),
                        Name = $"{user} originally said:"
                    },
                    Description = msg,
                    Footer = new EmbedFooterBuilder
                    {
                        IconUrl = ctx.User.GetAvatarUrl(),
                        Text = $"Edit snipe requested by {ctx.User} || Message edited {(DateTime.UtcNow - tstamp.Value).Humanize()} ago"
                    },
                    Color = Mewdeko.OkColor
                };
                await ctx.Channel.SendMessageAsync("", embed: em.Build());
            }
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [Priority(1)]
        public async Task EditSnipe(ITextChannel chan, IUser user1)
        {
            if (_service.GetSnipeSet(ctx.Guild.Id) == 0)
            {
                await ctx.Channel.SendErrorAsync(
                    $"Sniping != enabled in this server! Use `{Prefix}snipeset enable` to enable it!");
                return;
            }

            {
                var msgs = _service.Snipemsg(ctx.Guild.Id, chan.Id).Where(x => x.UserId == user1.Id);
                if (!msgs.Any() || msgs == null)
                {
                    await ctx.Channel.SendErrorAsync("There's nothing to snipe for that user or channel!");
                    return;
                }

                var msg = msgs.OrderByDescending(d => d.DateAdded).Where(m => m.Edited == 1)
                    .Where(x => x.UserId == user1.Id).Select(x => x.Message).First();
                var userid = msgs.OrderByDescending(d => d.DateAdded).Where(m => m.Edited == 1)
                    .Where(x => x.UserId == user1.Id).Select(x => x.UserId).First();
                var tstamp = msgs.OrderByDescending(d => d.DateAdded).Where(m => m.Edited == 1)
                    .Where(x => x.UserId == user1.Id).Select(x => x.DateAdded).First();
                var user = await ctx.Channel.GetUserAsync(userid);

                var em = new EmbedBuilder
                {
                    Author = new EmbedAuthorBuilder
                    {
                        IconUrl = user.GetAvatarUrl(),
                        Name = $"{user} originally said:"
                    },
                    Description = msg,
                    Footer = new EmbedFooterBuilder
                    {
                        IconUrl = ctx.User.GetAvatarUrl(),
                        Text = $"Edit snipe requested by {ctx.User} || Message edited {(DateTime.UtcNow - tstamp.Value).Humanize()} ago"
                    },
                    Color = Mewdeko.OkColor
                };
                await ctx.Channel.SendMessageAsync("", embed: em.Build());
            }
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task WhosPlaying([Remainder] string game)
        {
            game = game?.Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(game))
                return;

            if (!(ctx.Guild is SocketGuild socketGuild))
            {
                Log.Warning("Can't cast guild to socket guild.");
                return;
            }

            var rng = new MewdekoRandom();
            var arr = await Task.Run(() => socketGuild.Users
                .Where(u => u.Activities?.FirstOrDefault()?.Name.ToUpperInvariant() == game)
                .Select(u => u.Username)
                .OrderBy(x => rng.Next())
                .Take(60)
                .ToArray()).ConfigureAwait(false);

            var i = 0;
            if (arr.Length == 0)
                await ReplyErrorLocalizedAsync("nobody_playing_game").ConfigureAwait(false);
            else
                await ctx.Channel.SendConfirmAsync("```css\n" + string.Join("\n", arr.GroupBy(item => i++ / 2)
                        .Select(ig => string.Concat(ig.Select(el => $"• {el,-27}")))) + "\n```")
                    .ConfigureAwait(false);
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Vote()
        {
            await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                .WithDescription(
                    "Vote here for Mewdeko!\n[Vote Link](https://top.gg/bot/752236274261426212)\nMake sure to join the support server! \n[Link](https://mewdeko.tech/support)"));
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task InRole([Remainder] IRole role)
        {
            await Context.Channel.TriggerTypingAsync().ConfigureAwait(false);
            await _tracker.EnsureUsersDownloadedAsync(ctx.Guild).ConfigureAwait(false);

            var users = await ctx.Guild.GetUsersAsync();
            var roleUsers = users
                .Where(u => u.RoleIds.Contains(role.Id))
                .Select(u => u.ToString())
                .ToArray();

            await ctx.SendPaginatedConfirmAsync(0, cur =>
            {
                return new EmbedBuilder().WithOkColor()
                    .WithTitle(Format.Bold(GetText("inrole_list", Format.Bold(role.Name))) + $" - {roleUsers.Length}")
                    .WithDescription(string.Join("\n", roleUsers.Skip(cur * 20).Take(20)));
            }, roleUsers.Length, 20).ConfigureAwait(false);
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task InRoles(IRole role, IRole role2)
        {
            await Context.Channel.TriggerTypingAsync().ConfigureAwait(false);
            await _tracker.EnsureUsersDownloadedAsync(ctx.Guild).ConfigureAwait(false);

            var users = await ctx.Guild.GetUsersAsync();
            var roleUsers = users
                .Where(u => u.RoleIds.Contains(role.Id) && u.RoleIds.Contains(role2.Id))
                .Select(u => u.ToString())
                .ToArray();

            await ctx.SendPaginatedConfirmAsync(0, cur =>
            {
                return new EmbedBuilder().WithOkColor()
                    .WithTitle(Format.Bold($"Users in the roles: {role.Name} | {role2.Name} - {roleUsers.Count()}"))
                    .WithDescription(string.Join("\n", roleUsers.Skip(cur * 20).Take(20)));
            }, roleUsers.Length, 20).ConfigureAwait(false);
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task UserId([Remainder] IGuildUser target = null)
        {
            var usr = target ?? ctx.User;
            await ReplyConfirmLocalizedAsync("userid", "🆔", Format.Bold(usr.ToString()),
                Format.Code(usr.Id.ToString())).ConfigureAwait(false);
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task RoleId([Remainder] IRole role)
        {
            await ReplyConfirmLocalizedAsync("roleid", "🆔", Format.Bold(role.ToString()),
                Format.Code(role.Id.ToString())).ConfigureAwait(false);
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        public async Task ChannelId()
        {
            await ReplyConfirmLocalizedAsync("channelid", "🆔", Format.Code(ctx.Channel.Id.ToString()))
                .ConfigureAwait(false);
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task ServerId()
        {
            await ReplyConfirmLocalizedAsync("serverid", "🆔", Format.Code(ctx.Guild.Id.ToString()))
                .ConfigureAwait(false);
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Roles(IGuildUser target, int page = 1)
        {
            var channel = (ITextChannel)ctx.Channel;
            var guild = channel.Guild;

            const int rolesPerPage = 20;

            if (page < 1 || page > 100)
                return;

            if (target != null)
            {
                var roles = target.GetRoles().Except(new[] { guild.EveryoneRole }).OrderBy(r => -r.Position)
                    .Skip((page - 1) * rolesPerPage).Take(rolesPerPage).ToArray();
                if (!roles.Any())
                    await ReplyErrorLocalizedAsync("no_roles_on_page").ConfigureAwait(false);
                else
                    await channel.SendConfirmAsync(GetText("roles_page", page, Format.Bold(target.ToString())),
                        "\n• " + string.Join("\n• ", (IEnumerable<IRole>)roles)).ConfigureAwait(false);
            }
            else
            {
                var roles = guild.Roles.Except(new[] { guild.EveryoneRole }).OrderBy(r => -r.Position)
                    .Skip((page - 1) * rolesPerPage).Take(rolesPerPage).ToArray();
                if (!roles.Any())
                    await ReplyErrorLocalizedAsync("no_roles_on_page").ConfigureAwait(false);
                else
                    await channel.SendConfirmAsync(GetText("roles_all_page", page),
                            "\n• " + string.Join("\n• ", (IEnumerable<IRole>)roles).SanitizeMentions())
                        .ConfigureAwait(false);
            }
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public Task Roles(int page = 1)
        {
            return Roles(null, page);
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task ChannelTopic([Remainder] ITextChannel channel = null)
        {
            if (channel == null)
                channel = (ITextChannel)ctx.Channel;

            var topic = channel.Topic;
            if (string.IsNullOrWhiteSpace(topic))
                await ReplyErrorLocalizedAsync("no_topic_set").ConfigureAwait(false);
            else
                await ctx.Channel.SendConfirmAsync(GetText("channel_topic"), topic).ConfigureAwait(false);
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        public async Task Stats()
        {
            var user = await _client.Rest.GetUserAsync(280835732728184843);
            var guilds = _client.Guilds;
            var ownerIds = string.Join("\n", _creds.OwnerIds);
            if (string.IsNullOrWhiteSpace(ownerIds))
                ownerIds = "-";

            await ctx.Channel.EmbedAsync(
                new EmbedBuilder().WithOkColor()
                    .WithAuthor(eab => eab.WithName($"{_client.CurrentUser.Username} v{StatsService.BotVersion}")
                        .WithUrl("https://discord.gg/UTStayT6tp")
                        .WithIconUrl(_client.CurrentUser.GetAvatarUrl()))
                    .WithThumbnailUrl(
                        "https://cdn.discordapp.com/attachments/802687899350990919/822503142549225553/nayofinalihope.png")
                    .AddField(efb =>
                        efb.WithName(GetText("author")).WithValue($"{user.Username}#{user.Discriminator}")
                            .WithIsInline(false))
                    .AddField(efb => efb.WithName("Library").WithValue(_stats.Library).WithIsInline(false))
                    .AddField(efb =>
                        efb.WithName(GetText("shard")).WithValue($"#{_client.ShardId} / {_creds.TotalShards}")
                            .WithIsInline(false))
                    .AddField(efb =>
                        efb.WithName(GetText("commands_ran")).WithValue(_stats.CommandsRan.ToString())
                            .WithIsInline(false))
                    .AddField(efb =>
                        efb.WithName(GetText("messages"))
                            .WithValue($"{_stats.MessageCounter} ({_stats.MessagesPerSecond:F2}/sec)")
                            .WithIsInline(false))
                    .AddField(efb => efb.WithName(GetText("memory")).WithValue($"{_stats.Heap} MB").WithIsInline(false))
                    .AddField(efb =>
                        efb.WithName(GetText("uptime")).WithValue(_stats.GetUptimeString("\n")).WithIsInline(false))
                    .AddField(efb => efb.WithName(GetText("presence")).WithValue(
                        GetText("presence_txt",
                            _bot.GuildCount, _stats.TextChannels, _stats.VoiceChannels) + "\n" +
                        guilds.Sum(x => x.MemberCount) + " Total Members").WithIsInline(false))).ConfigureAwait(false);
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        public async Task
            Showemojis([Remainder] string _) // need to have the parameter so that the message.tags gets populated
        {
            var tags = ctx.Message.Tags.Where(t => t.Type == TagType.Emoji).Select(t => (Emote)t.Value);

            var result = string.Join("\n", tags.Select(m => GetText("showemojis", m, m.Url)));

            if (string.IsNullOrWhiteSpace(result))
                await ReplyErrorLocalizedAsync("showemojis_none").ConfigureAwait(false);
            else
                await ctx.Channel.SendMessageAsync(result.TrimTo(2000)).ConfigureAwait(false);
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [OwnerOnly]
        public async Task ListServers(int page = 1)
        {
            page -= 1;

            if (page < 0)
                return;

            var guilds = await Task.Run(() => _client.Guilds.OrderBy(g => g.Name).Skip(page * 15).Take(15))
                .ConfigureAwait(false);

            if (!guilds.Any())
            {
                await ReplyErrorLocalizedAsync("listservers_none").ConfigureAwait(false);
                return;
            }

            await ctx.Channel.EmbedAsync(guilds.Aggregate(new EmbedBuilder().WithOkColor(),
                    (embed, g) => embed.AddField(efb => efb.WithName(g.Name)
                        .WithValue(
                            GetText("listservers", g.Id, g.MemberCount,
                                g.OwnerId))
                        .WithIsInline(false))))
                .ConfigureAwait(false);
        }


        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [OwnerOnly]
        public async Task SaveChat(int cnt)
        {
            var msgs = new List<IMessage>(cnt);
            await ctx.Channel.GetMessagesAsync(cnt).ForEachAsync(dled => msgs.AddRange(dled)).ConfigureAwait(false);

            var title = $"Chatlog-{ctx.Guild.Name}/#{ctx.Channel.Name}-{DateTime.Now}.txt";
            var grouping = msgs.GroupBy(x => $"{x.CreatedAt.Date:dd.MM.yyyy}")
                .Select(g => new
                {
                    date = g.Key,
                    messages = g.OrderBy(x => x.CreatedAt).Select(s =>
                    {
                        var msg = $"【{s.Timestamp:HH:mm:ss}】{s.Author}:";
                        if (string.IsNullOrWhiteSpace(s.ToString()))
                        {
                            if (s.Attachments.Any())
                                msg += "FILES_UPLOADED: " + string.Join("\n", s.Attachments.Select(x => x.Url));
                            else if (s.Embeds.Any())
                                msg += "EMBEDS: " + string.Join("\n--------\n",
                                    s.Embeds.Select(x => $"Description: {x.Description}"));
                        }
                        else
                        {
                            msg += s.ToString();
                        }

                        return msg;
                    })
                });
            using (var stream = await JsonConvert.SerializeObject(grouping, Formatting.Indented).ToStream()
                .ConfigureAwait(false))
            {
                await ctx.User.SendFileAsync(stream, title, title, false).ConfigureAwait(false);
            }
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
#if GLOBAL_Mewdeko
        [Ratelimit(30)]
#endif
        public async Task Ping()
        {
            await sem.WaitAsync(5000).ConfigureAwait(false);
            try
            {
                var sw = Stopwatch.StartNew();
                var msg = await ctx.Channel.SendMessageAsync("🏓").ConfigureAwait(false);
                sw.Stop();
                msg.DeleteAfter(0);

                await ctx.Channel
                    .SendConfirmAsync($"{Format.Bold(ctx.User.ToString())} 🏓 {(int)sw.Elapsed.TotalMilliseconds}ms")
                    .ConfigureAwait(false);
            }
            finally
            {
                sem.Release();
            }
        }
    }
}