using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Mewdeko.Common.Attributes;
using Mewdeko.Core.Common.Attributes;
using Mewdeko.Core.Services;
using Mewdeko.Extensions;

namespace Mewdeko.Modules.Administration
{
    public partial class Administration
    {
        [Group]
        public class ServerGreetCommands : MewdekoSubmodule<GreetSettingsService>
        {
            private readonly IHttpClientFactory _httpFactory;

            public ServerGreetCommands(IHttpClientFactory fact)
            {
                _httpFactory = fact;
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageGuild)]
            public async Task GreetDel(int timer = 30)
            {
                if (timer < 0 || timer > 600)
                    return;

                await _service.SetGreetDel(ctx.Guild.Id, timer).ConfigureAwait(false);

                if (timer > 0)
                    await ReplyConfirmLocalizedAsync("greetdel_on", timer).ConfigureAwait(false);
                else
                    await ReplyConfirmLocalizedAsync("greetdel_off").ConfigureAwait(false);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageGuild)]
            public async Task Greet()
            {
                var enabled = await _service.SetGreet(ctx.Guild.Id, ctx.Channel.Id).ConfigureAwait(false);

                if (enabled)
                    await ReplyConfirmLocalizedAsync("greet_on").ConfigureAwait(false);
                else
                    await ReplyConfirmLocalizedAsync("greet_off").ConfigureAwait(false);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageGuild)]
            public async Task GreetHook(ITextChannel chan = null, string name = null, string image = null,
                string text = null)
            {
                if (text is not null && text.ToLower() == "disable")
                {
                    await _service.SetWebGreet(ctx.Guild, 0);
                    await ctx.Channel.SendConfirmAsync("Greet webhook disabled.");
                    return;
                }

                if (chan is not null && name is not null && image is not null && text is not null &&
                    text?.ToLower() != "disable") return;
                if (image is not null && text is null)
                {
                    using var http = _httpFactory.CreateClient();
                    var uri = new Uri(image);
                    using (var sr = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead)
                        .ConfigureAwait(false))
                    {
                        var imgData = await sr.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                        using var imgStream = imgData.ToStream();
                        var webhook = await chan.CreateWebhookAsync(name, imgStream);
                        var txt = $"https://discord.com/api/webhooks/{webhook.Id}/{webhook.Token}";
                        await _service.SetWebGreet(ctx.Guild, 1);
                        await _service.SetWebGreetURL(ctx.Guild, txt);
                        var enabled = await _service.SetGreet(ctx.Guild.Id, ctx.Channel.Id).ConfigureAwait(false);
                        if (enabled)
                            await ctx.Channel.SendConfirmAsync("Set the greet webhook and enabled webhook greets");
                        else
                            await ctx.Channel.SendConfirmAsync(
                                $"Set the greet webhook and enabled webhook greets. Please use {Prefix}greet to enable greet messages.");
                    }
                }

                if (ctx.Message.Attachments.Any() && image is null && text is null)
                {
                    using var http = _httpFactory.CreateClient();
                    var tags = ctx.Message.Attachments.FirstOrDefault();
                    var uri = new Uri(tags.Url);
                    using (var sr = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead)
                        .ConfigureAwait(false))
                    {
                        var imgData = await sr.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                        using var imgStream = imgData.ToStream();
                        var webhook = await chan.CreateWebhookAsync(name, imgStream);
                        var txt = $"https://discord.com/api/webhooks/{webhook.Id}/{webhook.Token}";
                        await _service.SetWebGreet(ctx.Guild, 1);
                        await _service.SetWebGreetURL(ctx.Guild, txt);
                        var enabled = await _service.SetGreet(ctx.Guild.Id, ctx.Channel.Id).ConfigureAwait(false);
                        if (enabled)
                            await ctx.Channel.SendConfirmAsync("Set the greet webhook and enabled webhook greets");
                        else
                            await ctx.Channel.SendConfirmAsync(
                                $"Set the greet webhook and enabled webhook greets. Please use {Prefix}greet to enable greet messages.");
                    }
                }

                if (!ctx.Message.Attachments.Any() && image is null && text is null)
                {
                    var webhook = await chan.CreateWebhookAsync(name);
                    var txt = $"https://discord.com/api/webhooks/{webhook.Id}/{webhook.Token}";
                    await _service.SetWebGreet(ctx.Guild, 1);
                    await _service.SetWebGreetURL(ctx.Guild, txt);
                    var enabled = await _service.SetGreet(ctx.Guild.Id, ctx.Channel.Id).ConfigureAwait(false);
                    if (enabled)
                        await ctx.Channel.SendConfirmAsync("Set the greet webhook and enabled webhook greets");
                    else
                        await ctx.Channel.SendConfirmAsync(
                            $"Set the greet webhook and enabled webhook greets. Please use {Prefix}greet to enable greet messages.");
                }
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageGuild)]
            public async Task GreetHook(string text)
            {
                await GreetHook(null, null, null, text);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageGuild)]
            public Task GreetMsg()
            {
                var greetMsg = _service.GetGreetMsg(ctx.Guild.Id);
                return ReplyConfirmLocalizedAsync("greetmsg_cur", greetMsg?.SanitizeMentions());
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageGuild)]
            public async Task GreetMsg([Leftover] string text)
            {
                if (string.IsNullOrWhiteSpace(text))
                {
                    await GreetMsg().ConfigureAwait(false);
                    return;
                }

                var sendGreetEnabled = _service.SetGreetMessage(ctx.Guild.Id, ref text);

                await ReplyConfirmLocalizedAsync("greetmsg_new").ConfigureAwait(false);
                if (!sendGreetEnabled)
                    await ReplyConfirmLocalizedAsync("greetmsg_enable", $"`{Prefix}greet`").ConfigureAwait(false);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageGuild)]
            public async Task GreetDm()
            {
                var enabled = await _service.SetGreetDm(ctx.Guild.Id).ConfigureAwait(false);

                if (enabled)
                    await ReplyConfirmLocalizedAsync("greetdm_on").ConfigureAwait(false);
                else
                    await ReplyConfirmLocalizedAsync("greetdm_off").ConfigureAwait(false);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageGuild)]
            public Task GreetDmMsg()
            {
                var dmGreetMsg = _service.GetDmGreetMsg(ctx.Guild.Id);
                return ReplyConfirmLocalizedAsync("greetdmmsg_cur", dmGreetMsg?.SanitizeMentions());
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageGuild)]
            public async Task GreetDmMsg([Leftover] string text = null)
            {
                if (string.IsNullOrWhiteSpace(text))
                {
                    await GreetDmMsg().ConfigureAwait(false);
                    return;
                }

                var sendGreetEnabled = _service.SetGreetDmMessage(ctx.Guild.Id, ref text);

                await ReplyConfirmLocalizedAsync("greetdmmsg_new").ConfigureAwait(false);
                if (!sendGreetEnabled)
                    await ReplyConfirmLocalizedAsync("greetdmmsg_enable", $"`{Prefix}greetdm`").ConfigureAwait(false);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageGuild)]
            public async Task Bye()
            {
                var enabled = await _service.SetBye(ctx.Guild.Id, ctx.Channel.Id).ConfigureAwait(false);

                if (enabled)
                    await ReplyConfirmLocalizedAsync("bye_on").ConfigureAwait(false);
                else
                    await ReplyConfirmLocalizedAsync("bye_off").ConfigureAwait(false);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageGuild)]
            public Task ByeMsg()
            {
                var byeMsg = _service.GetByeMessage(ctx.Guild.Id);
                return ReplyConfirmLocalizedAsync("byemsg_cur", byeMsg?.SanitizeMentions());
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageGuild)]
            public async Task ByeMsg([Leftover] string text)
            {
                if (string.IsNullOrWhiteSpace(text))
                {
                    await ByeMsg().ConfigureAwait(false);
                    return;
                }

                var sendByeEnabled = _service.SetByeMessage(ctx.Guild.Id, ref text);

                await ReplyConfirmLocalizedAsync("byemsg_new").ConfigureAwait(false);
                if (!sendByeEnabled)
                    await ReplyConfirmLocalizedAsync("byemsg_enable", $"`{Prefix}bye`").ConfigureAwait(false);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageGuild)]
            public async Task ByeDel(int timer = 30)
            {
                await _service.SetByeDel(ctx.Guild.Id, timer).ConfigureAwait(false);

                if (timer > 0)
                    await ReplyConfirmLocalizedAsync("byedel_on", timer).ConfigureAwait(false);
                else
                    await ReplyConfirmLocalizedAsync("byedel_off").ConfigureAwait(false);
            }


            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageGuild)]
            [Ratelimit(5)]
            public async Task ByeTest([Leftover] IGuildUser user = null)
            {
                user = user ?? (IGuildUser) Context.User;

                await _service.ByeTest((ITextChannel) Context.Channel, user);
                var enabled = _service.GetByeEnabled(Context.Guild.Id);
                if (!enabled) await ReplyConfirmLocalizedAsync("byemsg_enable", $"`{Prefix}bye`").ConfigureAwait(false);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageGuild)]
            [Ratelimit(5)]
            public async Task GreetTest([Leftover] IGuildUser user = null)
            {
                user = user ?? (IGuildUser) Context.User;

                await _service.GreetTest((ITextChannel) Context.Channel, user);
                var enabled = _service.GetGreetEnabled(Context.Guild.Id);
                if (!enabled)
                    await ReplyConfirmLocalizedAsync("greetmsg_enable", $"`{Prefix}greet`").ConfigureAwait(false);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageGuild)]
            [Ratelimit(5)]
            public async Task GreetDmTest([Leftover] IGuildUser user = null)
            {
                user = user ?? (IGuildUser) Context.User;

                var channel = await user.CreateDMChannelAsync();
                var success = await _service.GreetDmTest(channel, user);
                if (success)
                    await Context.OkAsync();
                else
                    await Context.WarningAsync();
                var enabled = _service.GetGreetDmEnabled(Context.Guild.Id);
                if (!enabled)
                    await ReplyConfirmLocalizedAsync("greetdmmsg_enable", $"`{Prefix}greetdm`").ConfigureAwait(false);
            }
        }
    }
}