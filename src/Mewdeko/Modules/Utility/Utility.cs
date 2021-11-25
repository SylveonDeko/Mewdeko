using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Humanizer;
using Mewdeko._Extensions;
using Mewdeko.Common;
using Mewdeko.Common.Attributes;
using Mewdeko.Common.Extensions.Interactive;
using Mewdeko.Common.Extensions.Interactive.Entities.Page;
using Mewdeko.Common.Extensions.Interactive.Pagination;
using Mewdeko.Common.Extensions.Interactive.Pagination.Lazy;
using Mewdeko.Services;
using Mewdeko.Services.Impl;
using Mewdeko.Modules.Utility.Services;
using Newtonsoft.Json;
using Serilog;
using SixLabors.ImageSharp.PixelFormats;
using Color = SixLabors.ImageSharp.Color;

namespace Mewdeko.Modules.Utility
{
    public partial class Utility : MewdekoModuleBase<UtilityService>
    {
        private static readonly SemaphoreSlim sem = new(1, 1);
        private readonly Mewdeko.Services.Mewdeko _bot;
        private readonly DiscordSocketClient _client;
        private readonly IBotCredentials _creds;
        private readonly IStatsService _stats;
        private readonly DownloadTracker _tracker;
        private readonly InteractiveService Interactivity;
        private readonly ICoordinator _coord;

        public Utility(Mewdeko.Services.Mewdeko Mewdeko, DiscordSocketClient client,
            IStatsService stats, IBotCredentials creds, DownloadTracker tracker, InteractiveService serv, ICoordinator coord)
        {
            _coord = coord;
            Interactivity = serv;
            _client = client;
            _stats = stats;
            _creds = creds;
            _bot = Mewdeko;
            _tracker = tracker;
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Alias]
        public async Task EmoteList([Remainder] string emotetype = null)
        {
            GuildEmote[] emotes;
            switch (emotetype)
            {
                case "animated":
                    emotes = ctx.Guild.Emotes.Where(x => x.Animated).ToArray();
                    break;
                case "nonanimated":
                    emotes = ctx.Guild.Emotes.Where(x => !x.Animated).ToArray();
                    break;
                default:
                    emotes = ctx.Guild.Emotes.ToArray();
                    break;
            }

            if (!emotes.Any())
            {
                await ctx.Channel.SendErrorAsync("No emotes found!");
                return;
            }

            var paginator = new LazyPaginatorBuilder()
                .AddUser(ctx.User)
                .WithPageFactory(PageFactory)
                .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                .WithMaxPageIndex(emotes.Length / 10)
                .WithDefaultEmotes()
                .Build();

            await Interactivity.SendPaginatorAsync(paginator, Context.Channel,
                TimeSpan.FromMinutes(60));

            Task<PageBuilder> PageFactory(int page)
            {
                string titleText;
                switch (emotetype)
                {
                    case "animated":
                        titleText = $"{emotes.Length} Animated Emotes";
                        break;
                    case "nonanimated":
                        titleText = $"{emotes.Length} Non Animated Emotes";
                        break;
                    default:
                        titleText =
                            $"{emotes.Count(x => x.Animated)} Animated Emotes | {emotes.Count(x => !x.Animated)} Non Animated Emotes";
                        break;
                }

                return Task.FromResult(new PageBuilder()
                    .WithTitle(titleText)
                    .WithDescription(string.Join("\n",
                        emotes.OrderBy(x => x.Name).Skip(10 * page).Take(10)
                            .Select(x => $"{x} `{x.Name}` [Link]({x.Url})")))
                    .WithOkColor());
            }
        }

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
            using var client = new HttpClient();
            var response = await client.GetAsync(url);

            var content = await response.Content.ReadAsStringAsync();
            var statusCode = response.StatusCode;
            if (statusCode.ToString() == "Forbidden")
                await ctx.Channel.SendErrorAsync("Sites down m8");
            else
                await ctx.Channel.SendConfirmAsync("Sites ok m8");
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [UserPerm(GuildPermission.ManageChannels)]
        public async Task ReactChannel(ITextChannel chan = null)
        {
            var e = Service.GetReactChans(ctx.Guild.Id);
            if (chan == null)
            {
                if (e == 0) return;
                await Service.SetReactChan(ctx.Guild, 0);
                ;
                await ctx.Channel.SendConfirmAsync("React Channel Disabled!");
            }
            else
            {
                if (e == 0)
                {
                    await Service.SetReactChan(ctx.Guild, chan.Id);
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
                        await Service.SetReactChan(ctx.Guild, chan.Id);
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
        [UserPerm(GuildPermission.Administrator)]
        [RequireContext(ContextType.Guild)]
        public async Task SnipeSet(string yesnt)
        {
            await Service.SnipeSet(ctx.Guild, yesnt);
            var t = Service.GetSnipeSet(ctx.Guild.Id);
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
            if (Service.GetSnipeSet(ctx.Guild.Id) == 0)
            {
                await ctx.Channel.SendErrorAsync(
                    $"Sniping is not enabled in this server! Use `{Prefix}snipeset enable` to enable it!");
                return;
            }

            var msgs = Service.Snipemsg(ctx.Guild.Id, ctx.Channel.Id);
            {
                if (!msgs.Any() || msgs == null)
                {
                    await ctx.Channel.SendErrorAsync("There's nothing to snipe!");
                    return;
                }

                Mewdeko.Services.Database.Models.SnipeStore msg = msgs.OrderByDescending(d => d.DateAdded)
                    .Where(x => x.Edited == 0).FirstOrDefault();
                var user = await ctx.Channel.GetUserAsync(msg.UserId) ?? await _client.Rest.GetUserAsync(msg.UserId);

                var em = new EmbedBuilder
                {
                    Author = new EmbedAuthorBuilder
                    {
                        IconUrl = user.GetAvatarUrl(),
                        Name = $"{user} said:"
                    },
                    Description = msg.Message,
                    Footer = new EmbedFooterBuilder
                    {
                        IconUrl = ctx.User.GetAvatarUrl(),
                        Text =
                            $"Snipe requested by {ctx.User} || Message deleted {(DateTime.UtcNow - msg.DateAdded.Value).Humanize()} ago"
                    },
                    Color = Mewdeko.Services.Mewdeko.OkColor
                };
                await ctx.Channel.SendMessageAsync("", embed: em.Build());
            }
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task SnipeList(int amount = 5)
        {
            if (Service.GetSnipeSet(ctx.Guild.Id) == 0)
            {
                await ctx.Channel.SendErrorAsync(
                    $"Sniping is not enabled in this server! Use `{Prefix}snipeset enable` to enable it!");
                return;
            }

            var msgs = Service.Snipemsg(ctx.Guild.Id, ctx.Channel.Id);
            {
                if (!msgs.Any() || msgs == null)
                {
                    await ctx.Channel.SendErrorAsync("There's nothing to snipe!");
                    return;
                }

                var msg = msgs.OrderByDescending(d => d.DateAdded).Where(x => x.Edited == 0).Take(amount);
                var paginator = new LazyPaginatorBuilder()
                    .AddUser(ctx.User)
                    .WithPageFactory(PageFactory)
                    .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                    .WithMaxPageIndex(msg.Count() - 1)
                    .WithDefaultEmotes()
                    .Build();

                await Interactivity.SendPaginatorAsync(paginator, Context.Channel,
                    TimeSpan.FromMinutes(60));

                Task<PageBuilder> PageFactory(int page)
                {
                    var user = ctx.Channel.GetUserAsync(msg.Skip(page).FirstOrDefault().UserId).Result ??
                               _client.Rest.GetUserAsync(msg.Skip(page).FirstOrDefault().UserId).Result;
                    return Task.FromResult(new PageBuilder()
                        .WithOkColor()
                        .WithAuthor(
                            new EmbedAuthorBuilder()
                                .WithIconUrl(user.RealAvatarUrl().AbsoluteUri)
                                .WithName($"{user} said:"))
                        .WithDescription(msg.Skip(page).FirstOrDefault().Message
                                         + $"\n\nMessage deleted {(DateTime.UtcNow - msg.Skip(page).FirstOrDefault().DateAdded.Value).Humanize()} ago"));
                }
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
            if (Service.GetSnipeSet(ctx.Guild.Id) == 0)
            {
                await ctx.Channel.SendErrorAsync(
                    $"Sniping is not enabled in this server! Use `{Prefix}snipeset enable` to enable it!");
                return;
            }

            var msgs = Service.Snipemsg(ctx.Guild.Id, ctx.Channel.Id);
            {
                if (!msgs.Any())
                {
                    await ctx.Channel.SendErrorAsync("There's nothing to snipe for this user!");
                    return;
                }

                var msg = msgs.OrderByDescending(d => d.DateAdded)
                    .Where(x => x.Edited == 0).First(x => x.UserId == user1.Id);
                var user = await ctx.Channel.GetUserAsync(msg.UserId) ?? await _client.Rest.GetUserAsync(msg.UserId);

                var em = new EmbedBuilder
                {
                    Author = new EmbedAuthorBuilder
                    {
                        IconUrl = user.GetAvatarUrl(),
                        Name = $"{user} said:"
                    },
                    Description = msg.Message,
                    Footer = new EmbedFooterBuilder
                    {
                        IconUrl = ctx.User.GetAvatarUrl(),
                        Text =
                            $"User specific snipe requested by {ctx.User} || Message deleted {(DateTime.UtcNow - msg.DateAdded.Value).Humanize()} ago"
                    },
                    Color = Mewdeko.Services.Mewdeko.OkColor
                };
                await ctx.Channel.SendMessageAsync("", embed: em.Build());
            }
        }

        public int GetRandom()
        {
            var r = new Random();
            return r.Next(60, 100);
        }
        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [Priority(2)]
        public async Task VCheck([Remainder] string url = null)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                await ctx.Channel.SendErrorAsync("You didn't specify a url");
            }
            else
            {
                var result = await Service.UrlChecker(url);
                var eb = new EmbedBuilder();
                eb.WithOkColor();
                eb.WithDescription(result.Permalink);
                eb.AddField("Virus Positives", result.Positives, true);
                eb.AddField("Number of scans", result.Total, true);
                await ctx.Channel.SendMessageAsync(embed: eb.Build());
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
            if (Service.GetSnipeSet(ctx.Guild.Id) == 0)
            {
                await ctx.Channel.SendErrorAsync(
                    $"Sniping is not enabled in this server! Tell an admin to use `{Prefix}snipeset enable` to enable it!");
                return;
            }

            var msgs = Service.Snipemsg(ctx.Guild.Id, chan.Id);
            {
                if (!msgs.Any() || msgs == null)
                {
                    await ctx.Channel.SendErrorAsync("There's nothing to snipe for that channel!");
                    return;
                }

                var msg = msgs.OrderByDescending(d => d.DateAdded)
                    .First(x => x.Edited == 0 && x.ChannelId == chan.Id);
                if (msg == null)
                {
                    await ctx.Channel.SendErrorAsync("There's nothing to snipe for that channel!");
                    return;
                }
                var user = await ctx.Channel.GetUserAsync(msg.UserId) ?? await _client.Rest.GetUserAsync(msg.UserId);

                var em = new EmbedBuilder
                {
                    Author = new EmbedAuthorBuilder
                    {
                        IconUrl = user.GetAvatarUrl(),
                        Name = $"{user} said:"
                    },
                    Description = msg.Message,
                    Footer = new EmbedFooterBuilder
                    {
                        IconUrl = ctx.User.GetAvatarUrl(),
                        Text =
                            $"Channel specific snipe requested by {ctx.User} || Message deleted {(DateTime.UtcNow - msg.DateAdded.Value).Humanize()} ago"
                    },
                    Color = Mewdeko.Services.Mewdeko.OkColor
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
            if (Service.GetSnipeSet(ctx.Guild.Id) == 0)
            {
                await ctx.Channel.SendErrorAsync(
                    $"Sniping is not enabled in this server! Tell an admin to use `{Prefix}snipeset enable` to enable it!");
                return;
            }

            var msgs = Service.Snipemsg(ctx.Guild.Id, chan.Id);
            {
                if (!msgs.Any() || msgs == null)
                {
                    await ctx.Channel.SendErrorAsync("There's nothing to snipe for that channel and user!");
                    return;
                }

                var msg = msgs.OrderByDescending(d => d.DateAdded).Where(x => x.Edited == 0)
                    .Where(x => x.UserId == user1.Id).First();
                if (msg == null)
                {
                    await ctx.Channel.SendErrorAsync("There's nothing to snipe for that channel and user!");
                    return;
                }
                var user = await ctx.Channel.GetUserAsync(msg.UserId) ?? await _client.Rest.GetUserAsync(msg.UserId);

                var em = new EmbedBuilder
                {
                    Author = new EmbedAuthorBuilder
                    {
                        IconUrl = user.GetAvatarUrl(),
                        Name = $"{user} said:"
                    },
                    Description = msg.Message,
                    Footer = new EmbedFooterBuilder
                    {
                        IconUrl = ctx.User.GetAvatarUrl(),
                        Text =
                            $"Channel and user specific snipe requested by {ctx.User} || Message deleted {(DateTime.UtcNow - msg.DateAdded.Value).Humanize()} ago"
                    },
                    Color = Mewdeko.Services.Mewdeko.OkColor
                };
                await ctx.Channel.SendMessageAsync("", embed: em.Build());
            }
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [UserPerm(GuildPermission.Administrator)]
        [RequireContext(ContextType.Guild)]
        public async Task PreviewLinks(string yesnt)
        {
            await Service.PreviewLinks(ctx.Guild, yesnt.Substring(0, 1).ToLower());
            var t = Service.GetPLinks(ctx.Guild.Id);
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
            if (Service.GetSnipeSet(ctx.Guild.Id) == 0)
            {
                await ctx.Channel.SendErrorAsync(
                    $"Sniping is not enabled in this server! Tell an admin to use `{Prefix}snipeset enable` to enable it!");
                return;
            }

            {
                var msgs = Service.Snipemsg(ctx.Guild.Id, ctx.Channel.Id);
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
                var user = await ctx.Channel.GetUserAsync(userid) ?? await _client.Rest.GetUserAsync(userid);

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
                        Text =
                            $"Edit snipe requested by {ctx.User} || Message edited {(DateTime.UtcNow - tstamp.Value).Humanize()} ago"
                    },
                    Color = Mewdeko.Services.Mewdeko.OkColor
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
            if (Service.GetSnipeSet(ctx.Guild.Id) == 0)
            {
                await ctx.Channel.SendErrorAsync(
                    $"Sniping is not enabled in this server! Tell an admin to use `{Prefix}snipeset enable` to enable it!");
                return;
            }

            {
                var msgs = Service.Snipemsg(ctx.Guild.Id, ctx.Channel.Id).Where(x => x.UserId == user1.Id);
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
                var user = await ctx.Channel.GetUserAsync(userid) ?? await _client.Rest.GetUserAsync(userid);

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
                        Text =
                            $"Edit snipe requested by {ctx.User} || Message edited {(DateTime.UtcNow - tstamp.Value).Humanize()} ago"
                    },
                    Color = Mewdeko.Services.Mewdeko.OkColor
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
            if (Service.GetSnipeSet(ctx.Guild.Id) == 0)
            {
                await ctx.Channel.SendErrorAsync(
                    $"Sniping != enabled in this server! Use `{Prefix}snipeset enable` to enable it!");
                return;
            }

            {
                var msgs = Service.Snipemsg(ctx.Guild.Id, chan.Id);
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
                var user = await ctx.Channel.GetUserAsync(userid) ?? await _client.Rest.GetUserAsync(userid);

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
                        Text =
                            $"Edit snipe requested by {ctx.User} || Message edited {(DateTime.UtcNow - tstamp.Value).Humanize()} ago"
                    },
                    Color = Mewdeko.Services.Mewdeko.OkColor
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
            if (Service.GetSnipeSet(ctx.Guild.Id) == 0)
            {
                await ctx.Channel.SendErrorAsync(
                    $"Sniping != enabled in this server! Use `{Prefix}snipeset enable` to enable it!");
                return;
            }

            {
                var msgs = Service.Snipemsg(ctx.Guild.Id, chan.Id).Where(x => x.UserId == user1.Id);
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
                var user = await ctx.Channel.GetUserAsync(userid) ?? await _client.Rest.GetUserAsync(userid);

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
                        Text =
                            $"Edit snipe requested by {ctx.User} || Message edited {(DateTime.UtcNow - tstamp.Value).Humanize()} ago"
                    },
                    Color = Mewdeko.Services.Mewdeko.OkColor
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

            if (ctx.Guild is not SocketGuild socketGuild)
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

            var paginator = new LazyPaginatorBuilder()
                .AddUser(ctx.User)
                .WithPageFactory(PageFactory)
                .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                .WithMaxPageIndex(roleUsers.Length / 20)
                .WithDefaultEmotes()
                .Build();

            await Interactivity.SendPaginatorAsync(paginator, Context.Channel,
                TimeSpan.FromMinutes(60));

            Task<PageBuilder> PageFactory(int page)
            {
                return Task.FromResult(new PageBuilder().WithOkColor()
                    .WithTitle(Format.Bold(GetText("inrole_list", Format.Bold(role.Name))) + $" - {roleUsers.Length}")
                    .WithDescription(string.Join("\n", roleUsers.Skip(page * 20).Take(20)))
                    .AddField("_ _", $"Online Users: {users.Count(x => x.Status == UserStatus.Offline)}\nDND Users: {users.Count(x => x.Status == UserStatus.DoNotDisturb)}\nIdle Users: {users.Count(x => x.Status == UserStatus.Idle)}\nOffline Users: {users.Count(x => x.Status == UserStatus.Offline)}"));
            }
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

            var paginator = new LazyPaginatorBuilder()
                .AddUser(ctx.User)
                .WithPageFactory(PageFactory)
                .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                .WithMaxPageIndex(roleUsers.Length / 20)
                .WithDefaultEmotes()
                .Build();

            await Interactivity.SendPaginatorAsync(paginator, Context.Channel,
                TimeSpan.FromMinutes(60));

            Task<PageBuilder> PageFactory(int page)
            {
                return Task.FromResult(new PageBuilder().WithOkColor()
                    .WithTitle(Format.Bold($"Users in the roles: {role.Name} | {role2.Name} - {roleUsers.Length}"))
                    .WithDescription(string.Join("\n", roleUsers.Skip(page * 20).Take(20))));
            }
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
                        .WithUrl("https://discord.gg/6n3aa9Xapf")
                        .WithIconUrl(_client.CurrentUser.GetAvatarUrl()))
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
                    .AddField(efb => efb.WithName(GetText("memory")).WithValue($"{_stats.Heap} MB").WithIsInline(false))
                    .AddField(efb =>
                        efb.WithName(GetText("uptime")).WithValue(_stats.GetUptimeString("\n")).WithIsInline(false))
                    .AddField(efb => efb.WithName(GetText("presence")).WithValue(
                        GetText("presence_txt",
                            _coord.GetGuildCount(), _stats.TextChannels, _stats.VoiceChannels)).WithIsInline(false))).ConfigureAwait(false);
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
        [UserPerm(GuildPermission.ManageMessages)]
        public async Task EmbedBuilder()
        {
                string title;
                string description;
                string footer;
                string thumbnailUrl;
                string footerUrl;
                Discord.Color color;

                //title
                await ReplyAsync("Enter your embed title, say none if you dont want one.");
                var response = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id);
                if (response != null && response != "none")
                {
                    title = response;
                }
                else if (response.ToLower() != null && response.ToLower() == "none")
                {
                    title = null;
                }
                else if (response == null)
                {
                    await ReplyAsync("You did not reply before the timeout");
                    return;
                }
                else { return; }

                //description
                await ReplyAsync("Enter your embed description:");
                var response1 = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id);
                if (response1 != null && response1.ToLower() != "none")
                {
                    description = response1;
                }
                else if (response1 != null && response1.ToLower() == "none")
                {
                    description = null;
                }
                else if (response1 == null)
                {
                    await ReplyAsync("You did not reply before the timeout");
                    return;
                }
                else { return; }

                //footer
                await ReplyAsync("Enter your embed footer:");
                var response2 = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id);
                if (response2 != null && response2.ToLower() != "none")
                {
                    footer = response2;
                }
                else if (response2 != null && response2.ToLower() == "none")
                {
                    footer = null;
                }
                else if (response2 == null)
                {
                    await ReplyAsync("You did not reply before the timeout");
                    return;
                }
                else { return; }
                await ReplyAsync("Enter your embed color");
                var response5 = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id);
                if (response5 != null && response5.ToLower() != "none")
                {
                    if (Color.TryParse(response5, out var e))
                    {
                        var rgba32 = e.ToPixel<Rgba32>();
                        color = new Discord.Color(rgba32.R, rgba32.G, rgba32.B);
                    }
                    else
                    {
                        color = Mewdeko.Services.Mewdeko.OkColor;
                    }
                } 
                else if (response5 != null && response5.ToLower() == "none")
                {
                    color = Mewdeko.Services.Mewdeko.OkColor;
                }
                else if (response2 == null)
                {
                    await ReplyAsync("You did not reply before the timeout");
                    return;
                }
                else { return; }


            //thumbnail url
            await ReplyAsync("Enter your embed thumbnail url:");
                var response3 = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id);
                if (response3 != null && response3.ToLower() != "none")
                {
                    thumbnailUrl = response3;
                }
                else if (response3 != null && response3.ToLower() == "none")
                {
                    thumbnailUrl = null;
                }
                else if (response3 == null)
                {
                    await ReplyAsync("You did not reply before the timeout");
                    return;
                }
                else { return; }

                //footerUrl
                await ReplyAsync("Enter your embed footer icon url:");
                var response4 = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id);
                if (response4 != null && response4.ToLower() != "none")
                {
                    footerUrl = response4;
                }
                else if (response4 != null && response4.ToLower()  == "none")
                {
                    footerUrl = null;
                }
                else if (response4 == null)
                {
                    await ReplyAsync("You did not reply before the timeout");
                    return;
                }
                else { return; }

                var messages = await Context.Channel.GetMessagesAsync(11).FlattenAsync();
                await (Context.Channel as SocketTextChannel).DeleteMessagesAsync(messages);

                var builder = new EmbedBuilder()
                    .WithThumbnailUrl(thumbnailUrl)
                    .WithTitle(title)
                    .WithColor(color)
                    .WithFooter(footer, iconUrl: footerUrl)
                    .WithDescription(description);
                var embed = builder.Build();
                await ReplyAsync(null, false, embed);

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
        [Ratelimit(30)]
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
                    .SendConfirmAsync($"Bot Ping {(int)sw.Elapsed.TotalMilliseconds}ms\nBot Latency {((DiscordSocketClient)ctx.Client).Latency}ms")
                    .ConfigureAwait(false);
            }
            finally
            {
                sem.Release();
            }
        }
    }
}