using System.Net.Http;
using System.Threading.Tasks;
using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;

namespace Mewdeko.Modules.Administration;

public partial class Administration
{
    [Group]
    public class ServerGreetCommands : MewdekoSubmodule<GreetSettingsService>
    {
        private readonly IHttpClientFactory httpFactory;
        private readonly GuildSettingsService guildSettings;

        public ServerGreetCommands(IHttpClientFactory fact, GuildSettingsService guildSettings)
        {
            httpFactory = fact;
            this.guildSettings = guildSettings;
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageGuild)]
        public async Task GreetDel(int timer = 30)
        {
            if (timer is < 0 or > 600)
                return;

            await Service.SetGreetDel(ctx.Guild.Id, timer).ConfigureAwait(false);

            if (timer > 0)
                await ReplyConfirmLocalizedAsync("greetdel_on", timer).ConfigureAwait(false);
            else
                await ReplyConfirmLocalizedAsync("greetdel_off").ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.ManageGuild)]
        public async Task BoostMsg()
        {
            var boostMessage = await Service.GetBoostMessage(ctx.Guild.Id);
            await ReplyConfirmLocalizedAsync("boostmsg_cur", boostMessage.SanitizeMentions());
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.ManageGuild)]
        public async Task Boost()
        {
            var enabled = await Service.SetBoost(ctx.Guild.Id, ctx.Channel.Id).ConfigureAwait(false);

            if (enabled)
                await ReplyConfirmLocalizedAsync("boost_on").ConfigureAwait(false);
            else
                await ReplyConfirmLocalizedAsync("boost_off").ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.ManageGuild)]
        public async Task BoostDel(int timer = 30)
        {
            if (timer is < 0 or > 600)
            {
                await ctx.Channel.SendErrorAsync("The max delete time is 600 seconds!").ConfigureAwait(false);
                return;
            }

            await Service.SetBoostDel(ctx.Guild.Id, timer).ConfigureAwait(false);

            if (timer > 0)
                await ReplyConfirmLocalizedAsync("boostdel_on", timer).ConfigureAwait(false);
            else
                await ReplyConfirmLocalizedAsync("boostdel_off").ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.ManageGuild)]
        public async Task BoostMsg([Remainder] string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                await BoostMsg().ConfigureAwait(false);
                return;
            }

            var sendBoostEnabled = await Service.SetBoostMessage(ctx.Guild.Id, text);

            await ReplyConfirmLocalizedAsync("boostmsg_new").ConfigureAwait(false);
            if (!sendBoostEnabled)
                await ReplyConfirmLocalizedAsync("boostmsg_enable", $"{await guildSettings.GetPrefix(ctx.Guild)}boost").ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageGuild)]
        public async Task Greet()
        {
            var enabled = await Service.SetGreet(ctx.Guild.Id, ctx.Channel.Id).ConfigureAwait(false);

            if (enabled)
                await ReplyConfirmLocalizedAsync("greet_on").ConfigureAwait(false);
            else
                await ReplyConfirmLocalizedAsync("greet_off").ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageGuild)]
        public async Task GreetHook(ITextChannel? chan, string? name, string? image = null,
            string? text = null)
        {
            if (text is not null && text.ToLower() == "disable")
            {
                await Service.SetWebGreetUrl(ctx.Guild, "").ConfigureAwait(false);
                await ctx.Channel.SendConfirmAsync("Greet webhook disabled.").ConfigureAwait(false);
                return;
            }

            if (chan is not null && name is not null && image is not null && text is not null &&
                text.ToLower() != "disable")
            {
                return;
            }

            if (image is not null && text is null)
            {
                using var http = httpFactory.CreateClient();
                var uri = new Uri(image);
                using var sr = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead)
                    .ConfigureAwait(false);
                var imgData = await sr.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                var imgStream = imgData.ToStream();
                await using var _ = imgStream.ConfigureAwait(false);
                var webhook = await chan.CreateWebhookAsync(name, imgStream).ConfigureAwait(false);
                var txt = $"https://discord.com/api/webhooks/{webhook.Id}/{webhook.Token}";
                await Service.SetWebGreetUrl(ctx.Guild, txt).ConfigureAwait(false);
                var enabled = await Service.SetGreet(ctx.Guild.Id, ctx.Channel.Id).ConfigureAwait(false);
                if (enabled)
                {
                    await ctx.Channel.SendConfirmAsync("Set the greet webhook and enabled webhook greets").ConfigureAwait(false);
                }
                else
                {
                    await ctx.Channel.SendConfirmAsync(
                            $"Set the greet webhook and enabled webhook greets. Please use {await guildSettings.GetPrefix(ctx.Guild)}greet to enable greet messages.")
                        .ConfigureAwait(false);
                }
            }

            if (ctx.Message.Attachments.Count > 0 && image is null && text is null)
            {
                using var http = httpFactory.CreateClient();
                var tags = ctx.Message.Attachments.FirstOrDefault();
                var uri = new Uri(tags.Url);
                using var sr = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead)
                    .ConfigureAwait(false);
                var imgData = await sr.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                var imgStream = imgData.ToStream();
                await using var _ = imgStream.ConfigureAwait(false);
                var webhook = await chan.CreateWebhookAsync(name, imgStream).ConfigureAwait(false);
                var txt = $"https://discord.com/api/webhooks/{webhook.Id}/{webhook.Token}";
                await Service.SetWebGreetUrl(ctx.Guild, txt).ConfigureAwait(false);
                var enabled = await Service.SetGreet(ctx.Guild.Id, ctx.Channel.Id).ConfigureAwait(false);
                if (enabled)
                {
                    await ctx.Channel.SendConfirmAsync("Set the greet webhook and enabled webhook greets").ConfigureAwait(false);
                }
                else
                {
                    await ctx.Channel.SendConfirmAsync(
                            $"Set the greet webhook and enabled webhook greets. Please use {await guildSettings.GetPrefix(ctx.Guild)}greet to enable greet messages.")
                        .ConfigureAwait(false);
                }
            }

            if (ctx.Message.Attachments.Count == 0 && image is null && text is null)
            {
                var webhook = await chan.CreateWebhookAsync(name).ConfigureAwait(false);
                var txt = $"https://discord.com/api/webhooks/{webhook.Id}/{webhook.Token}";
                await Service.SetWebGreetUrl(ctx.Guild, txt).ConfigureAwait(false);
                var enabled = await Service.SetGreet(ctx.Guild.Id, ctx.Channel.Id).ConfigureAwait(false);
                if (enabled)
                {
                    await ctx.Channel.SendConfirmAsync("Set the greet webhook and enabled webhook greets").ConfigureAwait(false);
                }
                else
                {
                    await ctx.Channel.SendConfirmAsync(
                            $"Set the greet webhook and enabled webhook greets. Please use {await guildSettings.GetPrefix(ctx.Guild)}greet to enable greet messages.")
                        .ConfigureAwait(false);
                }
            }
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageGuild)]
        public async Task LeaveHook(ITextChannel? chan, string? name, string? image = null,
            string? text = null)
        {
            if (text is not null && text.ToLower() == "disable")
            {
                await Service.SetWebLeaveUrl(ctx.Guild, "").ConfigureAwait(false);
                await ctx.Channel.SendConfirmAsync("Leave webhook disabled.").ConfigureAwait(false);
                return;
            }

            if (chan is not null && name is not null && image is not null && text is not null &&
                text.ToLower() != "disable")
            {
                return;
            }

            if (image is not null && text is null)
            {
                using var http = httpFactory.CreateClient();
                var uri = new Uri(image);
                using var sr = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead)
                    .ConfigureAwait(false);
                var imgData = await sr.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                var imgStream = imgData.ToStream();
                await using var _ = imgStream.ConfigureAwait(false);
                var webhook = await chan.CreateWebhookAsync(name, imgStream).ConfigureAwait(false);
                var txt = $"https://discord.com/api/webhooks/{webhook.Id}/{webhook.Token}";
                await Service.SetWebLeaveUrl(ctx.Guild, txt).ConfigureAwait(false);
                var enabled = await Service.SetBye(ctx.Guild.Id, ctx.Channel.Id).ConfigureAwait(false);
                if (enabled)
                {
                    await ctx.Channel.SendConfirmAsync("Set the leave webhook and enabled webhook leaves").ConfigureAwait(false);
                }
                else
                {
                    await ctx.Channel.SendConfirmAsync(
                            $"Set the leave webhook and enabled webhook leaves. Please use {await guildSettings.GetPrefix(ctx.Guild)}bye to enable greet messages.")
                        .ConfigureAwait(false);
                }
            }

            if (ctx.Message.Attachments.Count > 0 && image is null && text is null)
            {
                using var http = httpFactory.CreateClient();
                var tags = ctx.Message.Attachments.FirstOrDefault();
                var uri = new Uri(tags.Url);
                using var sr = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead)
                    .ConfigureAwait(false);
                var imgData = await sr.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                var imgStream = imgData.ToStream();
                await using var _ = imgStream.ConfigureAwait(false);
                var webhook = await chan.CreateWebhookAsync(name, imgStream).ConfigureAwait(false);
                var txt = $"https://discord.com/api/webhooks/{webhook.Id}/{webhook.Token}";
                await Service.SetWebLeaveUrl(ctx.Guild, txt).ConfigureAwait(false);
                var enabled = await Service.SetBye(ctx.Guild.Id, ctx.Channel.Id).ConfigureAwait(false);
                if (enabled)
                {
                    await ctx.Channel.SendConfirmAsync("Set the leave webhook and enabled webhook leaves").ConfigureAwait(false);
                }
                else
                {
                    await ctx.Channel.SendConfirmAsync(
                            $"Set the leave webhook and enabled webhook leaves. Please use {await guildSettings.GetPrefix(ctx.Guild)}bye to enable greet messages.")
                        .ConfigureAwait(false);
                }
            }

            if (ctx.Message.Attachments.Count == 0 && image is null && text is null)
            {
                var webhook = await chan.CreateWebhookAsync(name).ConfigureAwait(false);
                var txt = $"https://discord.com/api/webhooks/{webhook.Id}/{webhook.Token}";
                await Service.SetWebLeaveUrl(ctx.Guild, txt).ConfigureAwait(false);
                var enabled = await Service.SetBye(ctx.Guild.Id, ctx.Channel.Id).ConfigureAwait(false);
                if (enabled)
                {
                    await ctx.Channel.SendConfirmAsync("Set the leave webhook and enabled webhook leaves").ConfigureAwait(false);
                }
                else
                {
                    await ctx.Channel.SendConfirmAsync(
                            $"Set the leave webhook and enabled webhook leaves. Please use {await guildSettings.GetPrefix(ctx.Guild)}bye to enable greet messages.")
                        .ConfigureAwait(false);
                }
            }
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageGuild)]
        public async Task GreetHook(string text) => await GreetHook(null, null, null, text).ConfigureAwait(false);

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageGuild)]
        public async Task LeaveHook(string text) => await LeaveHook(null, null, null, text).ConfigureAwait(false);

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageGuild)]
        public async Task GreetMsg()
        {
            var greetMsg = await Service.GetGreetMsg(ctx.Guild.Id);
            await ReplyConfirmLocalizedAsync("greetmsg_cur", greetMsg.SanitizeMentions());
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageGuild)]
        public async Task GreetMsg([Remainder] string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                await GreetMsg().ConfigureAwait(false);
                return;
            }

            var sendGreetEnabled = await Service.SetGreetMessage(ctx.Guild.Id, text);

            await ReplyConfirmLocalizedAsync("greetmsg_new").ConfigureAwait(false);
            if (!sendGreetEnabled)
                await ReplyConfirmLocalizedAsync("greetmsg_enable", $"`{await guildSettings.GetPrefix(ctx.Guild)}greet`").ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageGuild)]
        public async Task GreetDm()
        {
            if (!ctx.Client.CurrentUser.Flags.HasFlag(UserProperties.VerifiedBot))
            {
                if (!await PromptUserConfirmAsync(
                        "Bots that are not verified can get quarantined by Discord if they dm too many users at once, Do you still want to toggle this feature?", ctx.User.Id))
                    return;
            }

            var enabled = await Service.SetGreetDm(ctx.Guild.Id).ConfigureAwait(false);

            if (enabled)
                await ReplyConfirmLocalizedAsync("greetdm_on").ConfigureAwait(false);
            else
                await ReplyConfirmLocalizedAsync("greetdm_off").ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageGuild)]
        public async Task GreetDmMsg()
        {
            var dmGreetMsg = await Service.GetDmGreetMsg(ctx.Guild.Id);
            await ReplyConfirmLocalizedAsync("greetdmmsg_cur", dmGreetMsg.SanitizeMentions());
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageGuild)]
        public async Task GreetDmMsg([Remainder] string? text = null)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                await GreetDmMsg().ConfigureAwait(false);
                return;
            }

            var sendGreetEnabled = await Service.SetGreetDmMessage(ctx.Guild.Id, text);

            await ReplyConfirmLocalizedAsync("greetdmmsg_new").ConfigureAwait(false);
            if (!sendGreetEnabled)
                await ReplyConfirmLocalizedAsync("greetdmmsg_enable", $"`{await guildSettings.GetPrefix(ctx.Guild)}greetdm`").ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageGuild)]
        public async Task Bye()
        {
            var enabled = await Service.SetBye(ctx.Guild.Id, ctx.Channel.Id).ConfigureAwait(false);

            if (enabled)
                await ReplyConfirmLocalizedAsync("bye_on").ConfigureAwait(false);
            else
                await ReplyConfirmLocalizedAsync("bye_off").ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageGuild)]
        public async Task ByeMsg()
        {
            var byeMsg = await Service.GetByeMessage(ctx.Guild.Id);
            await ReplyConfirmLocalizedAsync("byemsg_cur", byeMsg.SanitizeMentions());
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageGuild)]
        public async Task ByeMsg([Remainder] string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                await ByeMsg().ConfigureAwait(false);
                return;
            }

            var sendByeEnabled = await Service.SetByeMessage(ctx.Guild.Id, text);

            await ReplyConfirmLocalizedAsync("byemsg_new").ConfigureAwait(false);
            if (!sendByeEnabled)
                await ReplyConfirmLocalizedAsync("byemsg_enable", $"`{await guildSettings.GetPrefix(ctx.Guild)}bye`").ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageGuild)]
        public async Task ByeDel(int timer = 30)
        {
            await Service.SetByeDel(ctx.Guild.Id, timer).ConfigureAwait(false);

            if (timer > 0)
                await ReplyConfirmLocalizedAsync("byedel_on", timer).ConfigureAwait(false);
            else
                await ReplyConfirmLocalizedAsync("byedel_off").ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageGuild), Ratelimit(5)]
        public async Task ByeTest([Remainder] IGuildUser? user = null)
        {
            user ??= (IGuildUser)Context.User;

            await Service.ByeTest((ITextChannel)Context.Channel, user).ConfigureAwait(false);
            var enabled = await Service.GetByeEnabled(Context.Guild.Id);
            if (!enabled) await ReplyConfirmLocalizedAsync("byemsg_enable", $"`{await guildSettings.GetPrefix(ctx.Guild)}bye`").ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageGuild), Ratelimit(5)]
        public async Task BoostTest([Remainder] IGuildUser? user = null)
        {
            user ??= (IGuildUser)Context.User;
            await Service.BoostTest(ctx.Channel as ITextChannel, user).ConfigureAwait(false);
            var enabled = await Service.GetBoostEnabled(Context.Guild.Id);
            if (!enabled)
                await ReplyConfirmLocalizedAsync("boostmsg_enable", $"`{await guildSettings.GetPrefix(ctx.Guild)}greet`").ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageGuild), Ratelimit(5)]
        public async Task GreetTest([Remainder] IGuildUser? user = null)
        {
            user ??= (IGuildUser)Context.User;

            await Service.GreetTest((ITextChannel)Context.Channel, user).ConfigureAwait(false);
            var enabled = await Service.GetGreetEnabled(Context.Guild.Id);
            if (!enabled)
                await ReplyConfirmLocalizedAsync("greetmsg_enable", $"`{await guildSettings.GetPrefix(ctx.Guild)}greet`").ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageGuild), Ratelimit(5)]
        public async Task GreetDmTest([Remainder] IGuildUser? user = null)
        {
            user ??= (IGuildUser)Context.User;

            var channel = await user.CreateDMChannelAsync().ConfigureAwait(false);
            var success = await Service.GreetDmTest(channel, user).ConfigureAwait(false);
            if (success)
                await Context.OkAsync().ConfigureAwait(false);
            else
                await Context.WarningAsync().ConfigureAwait(false);
            var enabled = await Service.GetGreetDmEnabled(Context.Guild.Id);
            if (!enabled)
                await ReplyConfirmLocalizedAsync("greetdmmsg_enable", $"`{await guildSettings.GetPrefix(ctx.Guild)}greetdm`").ConfigureAwait(false);
        }
    }
}