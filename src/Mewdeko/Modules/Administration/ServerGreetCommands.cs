using System.Net.Http;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Mewdeko._Extensions;
using Mewdeko.Common;
using Mewdeko.Common.Attributes;
using Mewdeko.Common.Replacements;

namespace Mewdeko.Modules.Administration;

public partial class Administration
{
    [Group]
    public class ServerGreetCommands : MewdekoSubmodule<GreetSettingsService>
    {
        private readonly IHttpClientFactory _httpFactory;

        public ServerGreetCommands(IHttpClientFactory fact) => _httpFactory = fact;

        public enum MultiGreetMode
        {
            MultiGreet,
            RandomGreet
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task GreetDel(int timer = 30)
        {
            if (timer < 0 || timer > 600)
                return;

            await Service.SetGreetDel(ctx.Guild.Id, timer).ConfigureAwait(false);

            if (timer > 0)
                await ReplyConfirmLocalizedAsync("greetdel_on", timer).ConfigureAwait(false);
            else
                await ReplyConfirmLocalizedAsync("greetdel_off").ConfigureAwait(false);
        }

        [MewdekoCommand]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public Task BoostMsg()
        {
            var boostMessage = Service.GetBoostMessage(ctx.Guild.Id);
            return ReplyConfirmLocalizedAsync("boostmsg_cur", boostMessage?.SanitizeMentions());
        }

        [MewdekoCommand]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task Boost()
        {
            var enabled = await Service.SetBoost(ctx.Guild.Id, ctx.Channel.Id);

            if (enabled)
                await ReplyConfirmLocalizedAsync("boost_on").ConfigureAwait(false);
            else
                await ReplyConfirmLocalizedAsync("boost_off").ConfigureAwait(false);
        }

        [MewdekoCommand]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task BoostDel(int timer = 30)
        {
            if (timer < 0 || timer > 600)
            {
                await ctx.Channel.SendErrorAsync("The max delete time is 600 seconds!");
                return;
            }

            await Service.SetBoostDel(ctx.Guild.Id, timer);

            if (timer > 0)
                await ReplyConfirmLocalizedAsync("boostdel_on", timer);
            else
                await ReplyConfirmLocalizedAsync("boostdel_off").ConfigureAwait(false);
        }

        [MewdekoCommand]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task BoostMsg([Remainder] string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                await BoostMsg().ConfigureAwait(false);
                return;
            }

            var sendBoostEnabled = Service.SetBoostMessage(ctx.Guild.Id, ref text);

            await ReplyConfirmLocalizedAsync("boostmsg_new").ConfigureAwait(false);
            if (!sendBoostEnabled)
                await ReplyConfirmLocalizedAsync("boostmsg_enable", $"{Prefix}boost");
        }


        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task Greet()
        {
            var enabled = await Service.SetGreet(ctx.Guild.Id, ctx.Channel.Id).ConfigureAwait(false);

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
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task GreetHook(ITextChannel chan, string name, string image = null,
            string text = null)
        {
            if (text is not null && text.ToLower() == "disable")
            {
                await Service.SetWebGreetUrl(ctx.Guild, "");
                await ctx.Channel.SendConfirmAsync("Greet webhook disabled.");
                return;
            }

            if (chan is not null && name is not null && image is not null && text is not null &&
                text?.ToLower() != "disable") return;
            if (image is not null && text is null)
            {
                using var http = _httpFactory.CreateClient();
                var uri = new Uri(image);
                using var sr = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead)
                    .ConfigureAwait(false);
                var imgData = await sr.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                await using var imgStream = imgData.ToStream();
                var webhook = await chan.CreateWebhookAsync(name, imgStream);
                var txt = $"https://discord.com/api/webhooks/{webhook.Id}/{webhook.Token}";
                await Service.SetWebGreetUrl(ctx.Guild, txt);
                var enabled = await Service.SetGreet(ctx.Guild.Id, ctx.Channel.Id).ConfigureAwait(false);
                if (enabled)
                    await ctx.Channel.SendConfirmAsync("Set the greet webhook and enabled webhook greets");
                else
                    await ctx.Channel.SendConfirmAsync(
                        $"Set the greet webhook and enabled webhook greets. Please use {Prefix}greet to enable greet messages.");
            }

            if (ctx.Message.Attachments.Any() && image is null && text is null)
            {
                using var http = _httpFactory.CreateClient();
                var tags = ctx.Message.Attachments.FirstOrDefault();
                var uri = new Uri(tags.Url);
                using var sr = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead)
                    .ConfigureAwait(false);
                var imgData = await sr.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                await using var imgStream = imgData.ToStream();
                var webhook = await chan.CreateWebhookAsync(name, imgStream);
                var txt = $"https://discord.com/api/webhooks/{webhook.Id}/{webhook.Token}";
                await Service.SetWebGreetUrl(ctx.Guild, txt);
                var enabled = await Service.SetGreet(ctx.Guild.Id, ctx.Channel.Id).ConfigureAwait(false);
                if (enabled)
                    await ctx.Channel.SendConfirmAsync("Set the greet webhook and enabled webhook greets");
                else
                    await ctx.Channel.SendConfirmAsync(
                        $"Set the greet webhook and enabled webhook greets. Please use {Prefix}greet to enable greet messages.");
            }

            if (!ctx.Message.Attachments.Any() && image is null && text is null)
            {
                var webhook = await chan.CreateWebhookAsync(name);
                var txt = $"https://discord.com/api/webhooks/{webhook.Id}/{webhook.Token}";
                await Service.SetWebGreetUrl(ctx.Guild, txt);
                var enabled = await Service.SetGreet(ctx.Guild.Id, ctx.Channel.Id).ConfigureAwait(false);
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
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task LeaveHook(ITextChannel chan, string name, string image = null,
            string text = null)
        {
            if (text is not null && text.ToLower() == "disable")
            {
                await Service.SetWebLeaveUrl(ctx.Guild, "");
                await ctx.Channel.SendConfirmAsync("Leave webhook disabled.");
                return;
            }

            if (chan is not null && name is not null && image is not null && text is not null &&
                text?.ToLower() != "disable") return;
            if (image is not null && text is null)
            {
                using var http = _httpFactory.CreateClient();
                var uri = new Uri(image);
                using var sr = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead)
                    .ConfigureAwait(false);
                var imgData = await sr.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                await using var imgStream = imgData.ToStream();
                var webhook = await chan.CreateWebhookAsync(name, imgStream);
                var txt = $"https://discord.com/api/webhooks/{webhook.Id}/{webhook.Token}";
                await Service.SetWebLeaveUrl(ctx.Guild, txt);
                var enabled = await Service.SetBye(ctx.Guild.Id, ctx.Channel.Id).ConfigureAwait(false);
                if (enabled)
                    await ctx.Channel.SendConfirmAsync("Set the leave webhook and enabled webhook leaves");
                else
                    await ctx.Channel.SendConfirmAsync(
                        $"Set the leave webhook and enabled webhook leaves. Please use {Prefix}bye to enable greet messages.");
            }

            if (ctx.Message.Attachments.Any() && image is null && text is null)
            {
                using var http = _httpFactory.CreateClient();
                var tags = ctx.Message.Attachments.FirstOrDefault();
                var uri = new Uri(tags.Url);
                using var sr = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead)
                    .ConfigureAwait(false);
                var imgData = await sr.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                await using var imgStream = imgData.ToStream();
                var webhook = await chan.CreateWebhookAsync(name, imgStream);
                var txt = $"https://discord.com/api/webhooks/{webhook.Id}/{webhook.Token}";
                await Service.SetWebLeaveUrl(ctx.Guild, txt);
                var enabled = await Service.SetBye(ctx.Guild.Id, ctx.Channel.Id).ConfigureAwait(false);
                if (enabled)
                    await ctx.Channel.SendConfirmAsync("Set the leave webhook and enabled webhook leaves");
                else
                    await ctx.Channel.SendConfirmAsync(
                        $"Set the leave webhook and enabled webhook leaves. Please use {Prefix}bye to enable greet messages.");
            }

            if (!ctx.Message.Attachments.Any() && image is null && text is null)
            {
                var webhook = await chan.CreateWebhookAsync(name);
                var txt = $"https://discord.com/api/webhooks/{webhook.Id}/{webhook.Token}";
                await Service.SetWebLeaveUrl(ctx.Guild, txt);
                var enabled = await Service.SetBye(ctx.Guild.Id, ctx.Channel.Id).ConfigureAwait(false);
                if (enabled)
                    await ctx.Channel.SendConfirmAsync("Set the leave webhook and enabled webhook leaves");
                else
                    await ctx.Channel.SendConfirmAsync(
                        $"Set the leave webhook and enabled webhook leaves. Please use {Prefix}bye to enable greet messages.");
            }
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task GreetHook(string text) => await GreetHook(null, null, null, text);

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task LeaveHook(string text) => await LeaveHook(null, null, null, text);

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public Task GreetMsg()
        {
            var greetMsg = Service.GetGreetMsg(ctx.Guild.Id);
            return ReplyConfirmLocalizedAsync("greetmsg_cur", greetMsg?.SanitizeMentions());
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task GreetMsg([Remainder] string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                await GreetMsg().ConfigureAwait(false);
                return;
            }

            var sendGreetEnabled = Service.SetGreetMessage(ctx.Guild.Id, ref text);

            await ReplyConfirmLocalizedAsync("greetmsg_new").ConfigureAwait(false);
            if (!sendGreetEnabled)
                await ReplyConfirmLocalizedAsync("greetmsg_enable", $"`{Prefix}greet`").ConfigureAwait(false);
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task GreetDm()
        {
            var enabled = await Service.SetGreetDm(ctx.Guild.Id).ConfigureAwait(false);

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
        [UserPerm(GuildPermission.ManageGuild)]
        public Task GreetDmMsg()
        {
            var dmGreetMsg = Service.GetDmGreetMsg(ctx.Guild.Id);
            return ReplyConfirmLocalizedAsync("greetdmmsg_cur", dmGreetMsg?.SanitizeMentions());
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task GreetDmMsg([Remainder] string text = null)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                await GreetDmMsg().ConfigureAwait(false);
                return;
            }

            var sendGreetEnabled = Service.SetGreetDmMessage(ctx.Guild.Id, ref text);

            await ReplyConfirmLocalizedAsync("greetdmmsg_new").ConfigureAwait(false);
            if (!sendGreetEnabled)
                await ReplyConfirmLocalizedAsync("greetdmmsg_enable", $"`{Prefix}greetdm`").ConfigureAwait(false);
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task Bye()
        {
            var enabled = await Service.SetBye(ctx.Guild.Id, ctx.Channel.Id).ConfigureAwait(false);

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
        [UserPerm(GuildPermission.ManageGuild)]
        public Task ByeMsg()
        {
            var byeMsg = Service.GetByeMessage(ctx.Guild.Id);
            return ReplyConfirmLocalizedAsync("byemsg_cur", byeMsg?.SanitizeMentions());
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task ByeMsg([Remainder] string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                await ByeMsg().ConfigureAwait(false);
                return;
            }

            var sendByeEnabled = Service.SetByeMessage(ctx.Guild.Id, ref text);

            await ReplyConfirmLocalizedAsync("byemsg_new").ConfigureAwait(false);
            if (!sendByeEnabled)
                await ReplyConfirmLocalizedAsync("byemsg_enable", $"`{Prefix}bye`").ConfigureAwait(false);
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task ByeDel(int timer = 30)
        {
            await Service.SetByeDel(ctx.Guild.Id, timer).ConfigureAwait(false);

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
        [UserPerm(GuildPermission.ManageGuild)]
        [Ratelimit(5)]
        public async Task ByeTest([Remainder] IGuildUser user = null)
        {
            user = user ?? (IGuildUser) Context.User;

            await Service.ByeTest((ITextChannel) Context.Channel, user);
            var enabled = Service.GetByeEnabled(Context.Guild.Id);
            if (!enabled) await ReplyConfirmLocalizedAsync("byemsg_enable", $"`{Prefix}bye`").ConfigureAwait(false);
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        [Ratelimit(5)]
        public async Task BoostTest([Remainder] IGuildUser user = null)
        {
            user ??= (IGuildUser)Context.User;
            await Service.BoostTest(ctx.Channel as ITextChannel, user);
            var enabled = Service.GetBoostEnabled(Context.Guild.Id);
            if (!enabled)
                await ReplyConfirmLocalizedAsync("boostmsg_enable", $"`{Prefix}greet`").ConfigureAwait(false);
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        [Ratelimit(5)]
        public async Task GreetTest([Remainder] IGuildUser user = null)
        {
            user ??= (IGuildUser) Context.User;

            await Service.GreetTest((ITextChannel) Context.Channel, user);
            var enabled = Service.GetGreetEnabled(Context.Guild.Id);
            if (!enabled)
                await ReplyConfirmLocalizedAsync("greetmsg_enable", $"`{Prefix}greet`").ConfigureAwait(false);
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        [Ratelimit(5)]
        public async Task GreetDmTest([Remainder] IGuildUser user = null)
        {
            user = user ?? (IGuildUser) Context.User;

            var channel = await user.CreateDMChannelAsync();
            var success = await Service.GreetDmTest(channel, user);
            if (success)
                await Context.OkAsync();
            else
                await Context.WarningAsync();
            var enabled = Service.GetGreetDmEnabled(Context.Guild.Id);
            if (!enabled)
                await ReplyConfirmLocalizedAsync("greetdmmsg_enable", $"`{Prefix}greetdm`").ConfigureAwait(false);
        }
    }
}