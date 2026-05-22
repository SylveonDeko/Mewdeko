using System.Net.Http;
using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;

namespace Mewdeko.Modules.Administration;

public partial class Administration
{
    /// <summary>
    ///     Handles the greet and bye messages as well as boost settings bfor the server.
    /// </summary>
    /// <param name="fact">Client factory to avoid calling httpclient every time</param>
    /// <param name="guildSettings">The guild setting service</param>
    [Group]
    public class ServerGreetCommands(IHttpClientFactory fact, GuildSettingsService guildSettings)
        : MewdekoSubmodule<GreetSettingsService>
    {
        /// <summary>
        ///     Displays the current boost message.
        /// </summary>
        /// <remarks>
        ///     This command allows users to view the current boost message.
        /// </remarks>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task BoostMsg()
        {
            var boostMessage = await Service.GetBoostMessage(ctx.Guild.Id);
            await ReplyConfirmAsync(Strings.BoostmsgCur(ctx.Guild.Id, boostMessage.SanitizeMentions()));
        }

        /// <summary>
        ///     Enables or disables boost messages.
        /// </summary>
        /// <remarks>
        ///     This command allows users to enable or disable boost messages.
        /// </remarks>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task Boost()
        {
            var enabled = await Service.SetBoost(ctx.Guild.Id, ctx.Channel.Id).ConfigureAwait(false);

            if (enabled)
                await ReplyConfirmAsync(Strings.BoostOn(ctx.Guild.Id)).ConfigureAwait(false);
            else
                await ReplyConfirmAsync(Strings.BoostOff(ctx.Guild.Id)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets the timer for deleting boost messages.
        /// </summary>
        /// <remarks>
        ///     This command allows users to set the timer for deleting boost messages.
        /// </remarks>
        /// <param name="timer">The timer in seconds. Must be between 0 and 600.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task BoostDel(int timer = 30)
        {
            if (timer is < 0 or > 600)
            {
                await ctx.Channel.SendErrorAsync(Strings.Maxdeletetime(ctx.Guild.Id, "600 seconds"), Config)
                    .ConfigureAwait(false);
                return;
            }

            await Service.SetBoostDel(ctx.Guild.Id, timer).ConfigureAwait(false);

            if (timer > 0)
                await ReplyConfirmAsync(Strings.BoostdelOn(ctx.Guild.Id, timer)).ConfigureAwait(false);
            else
                await ReplyConfirmAsync(Strings.BoostdelOff(ctx.Guild.Id)).ConfigureAwait(false);
        }


        /// <summary>
        ///     Sets the boost message for the server.
        /// </summary>
        /// <remarks>
        ///     This command allows users to set the boost message for the server.
        /// </remarks>
        /// <param name="text">The new boost message.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task BoostMsg([Remainder] string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                await BoostMsg().ConfigureAwait(false);
                return;
            }

            var sendBoostEnabled = await Service.SetBoostMessage(ctx.Guild.Id, text);

            await ReplyConfirmAsync(Strings.BoostmsgNew(ctx.Guild.Id)).ConfigureAwait(false);
            if (!sendBoostEnabled)
                await ReplyConfirmAsync(Strings.BoostmsgEnable(ctx.Guild.Id,
                        $"{await guildSettings.GetPrefix(ctx.Guild)}boost"))
                    .ConfigureAwait(false);
        }

        private async Task<IWebhook> CreateWebhook(ITextChannel? chan, string? name, string imageUrl)
        {
            using var http = fact.CreateClient();
            var uri = new Uri(imageUrl);
            using var sr = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            var imgData = await sr.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            var imgStream = imgData.ToStream();
            await using var _ = imgStream.ConfigureAwait(false);
            return await chan.CreateWebhookAsync(name, imgStream).ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets up a webhook for leave messages with an optional image.
        /// </summary>
        /// <remarks>
        ///     This command allows users to set up a webhook for leave messages with an optional image.
        /// </remarks>
        /// <param name="chan">The text channel to set up the webhook in.</param>
        /// <param name="name">The name of the webhook.</param>
        /// <param name="image">The URL of the image to include in the webhook message.</param>
        /// <param name="text">The text to include in the webhook message.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task LeaveHook(ITextChannel? chan, string? name, string? image = null, string? text = null)
        {
            if (text?.Equals("disable", StringComparison.OrdinalIgnoreCase) == true)
            {
                await Service.SetWebLeaveUrl(ctx.Guild, "").ConfigureAwait(false);
                await ctx.Channel.SendConfirmAsync(Strings.Leavehookdisabled(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            if (image != null || ctx.Message.Attachments.Count > 0)
            {
                var imageUrl = image ?? ctx.Message.Attachments.FirstOrDefault()?.Url;
                var webhook = await CreateWebhook(chan, name, imageUrl).ConfigureAwait(false);
                var txt = $"https://discord.com/api/webhooks/{webhook.Id}/{webhook.Token}";
                await Service.SetWebLeaveUrl(ctx.Guild, txt).ConfigureAwait(false);
            }
            else if (image == null && text == null)
            {
                var webhook = await chan.CreateWebhookAsync(name).ConfigureAwait(false);
                var txt = $"https://discord.com/api/webhooks/{webhook.Id}/{webhook.Token}";
                await Service.SetWebLeaveUrl(ctx.Guild, txt).ConfigureAwait(false);
            }

            var enabled = await Service.SetBye(ctx.Guild.Id, ctx.Channel.Id).ConfigureAwait(false);
            var message = enabled
                ? Strings.Leavehookset(ctx.Guild.Id)
                : Strings.Leavehooksettwo(ctx.Guild.Id, await guildSettings.GetPrefix(ctx.Guild));
            await ctx.Channel.SendConfirmAsync(message)
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets up a leave message using the specified text.
        /// </summary>
        /// <remarks>
        ///     This command allows users to set up a leave message using the specified text.
        /// </remarks>
        /// <param name="text">The text to include in the leave message.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public Task LeaveHook(string text)
        {
            return LeaveHook(null, null, null, text);
        }

        /// <summary>
        ///     Toggles the sending of greeting messages via direct message.
        /// </summary>
        /// <remarks>
        ///     This command allows users to toggle the sending of greeting messages via direct message.
        /// </remarks>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task GreetDm()
        {
            if (!ctx.Client.CurrentUser.Flags.HasFlag(UserProperties.VerifiedBot))
            {
                if (!await PromptUserConfirmAsync(Strings.Dmgreetcheck(ctx.Guild.Id), ctx.User.Id))
                    return;
            }

            var enabled = await Service.SetGreetDm(ctx.Guild.Id).ConfigureAwait(false);

            if (enabled)
                await ReplyConfirmAsync(Strings.GreetdmOn(ctx.Guild.Id)).ConfigureAwait(false);
            else
                await ReplyConfirmAsync(Strings.GreetdmOff(ctx.Guild.Id)).ConfigureAwait(false);
        }


        /// <summary>
        ///     Displays the current direct message greeting message.
        /// </summary>
        /// <remarks>
        ///     This command displays the current direct message greeting message set for the guild.
        /// </remarks>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task GreetDmMsg()
        {
            var dmGreetMsg = await Service.GetDmGreetMsg(ctx.Guild.Id);
            await ReplyConfirmAsync(Strings.GreetdmmsgCur(ctx.Guild.Id, dmGreetMsg.SanitizeMentions()));
        }

        /// <summary>
        ///     Sets the direct message greeting message to the specified text.
        /// </summary>
        /// <remarks>
        ///     This command allows users to set the direct message greeting message to the specified text.
        /// </remarks>
        /// <param name="text">The text to set as the direct message greeting message.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task GreetDmMsg([Remainder] string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                await GreetDmMsg().ConfigureAwait(false);
                return;
            }

            var sendGreetEnabled = await Service.SetGreetDmMessage(ctx.Guild.Id, text);

            await ReplyConfirmAsync(Strings.GreetdmmsgNew(ctx.Guild.Id)).ConfigureAwait(false);
            if (!sendGreetEnabled)
                await ReplyConfirmAsync(Strings.GreetdmmsgEnable(ctx.Guild.Id,
                    $"`{await guildSettings.GetPrefix(ctx.Guild)}greetdm`")).ConfigureAwait(false);
        }

        /// <summary>
        ///     Toggles the sending of a bye message.
        /// </summary>
        /// <remarks>
        ///     This command allows users to toggle the sending of a bye message.
        /// </remarks>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task Bye()
        {
            var enabled = await Service.SetBye(ctx.Guild.Id, ctx.Channel.Id).ConfigureAwait(false);

            if (enabled)
                await ReplyConfirmAsync(Strings.ByeOn(ctx.Guild.Id)).ConfigureAwait(false);
            else
                await ReplyConfirmAsync(Strings.ByeOff(ctx.Guild.Id)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Displays the current bye message.
        /// </summary>
        /// <remarks>
        ///     This command displays the current bye message set for the guild.
        /// </remarks>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task ByeMsg()
        {
            var byeMsg = await Service.GetByeMessage(ctx.Guild.Id);
            await ReplyConfirmAsync(Strings.ByemsgCur(ctx.Guild.Id, byeMsg.SanitizeMentions()));
        }

        /// <summary>
        ///     Sets the bye message to the specified text.
        /// </summary>
        /// <remarks>
        ///     This command allows users to set the bye message to the specified text.
        /// </remarks>
        /// <param name="text">The text to set as the bye message.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task ByeMsg([Remainder] string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                await ByeMsg().ConfigureAwait(false);
                return;
            }

            var sendByeEnabled = await Service.SetByeMessage(ctx.Guild.Id, text);

            await ReplyConfirmAsync(Strings.ByemsgNew(ctx.Guild.Id)).ConfigureAwait(false);
            if (!sendByeEnabled)
                await ReplyConfirmAsync(Strings.ByemsgEnable(ctx.Guild.Id,
                        $"`{await guildSettings.GetPrefix(ctx.Guild)}bye`"))
                    .ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets the timer for deleting bye messages.
        /// </summary>
        /// <remarks>
        ///     This command allows users to set the timer for deleting bye messages.
        /// </remarks>
        /// <param name="timer">The timer duration in seconds.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task ByeDel(int timer = 30)
        {
            await Service.SetByeDel(ctx.Guild.Id, timer).ConfigureAwait(false);

            if (timer > 0)
                await ReplyConfirmAsync(Strings.ByedelOn(ctx.Guild.Id, timer)).ConfigureAwait(false);
            else
                await ReplyConfirmAsync(Strings.ByedelOff(ctx.Guild.Id)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Sends a test bye message.
        /// </summary>
        /// <remarks>
        ///     This command allows users to send a test bye message.
        /// </remarks>
        /// <param name="user">The user to send the test bye message to.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        [Ratelimit(5)]
        public async Task ByeTest([Remainder] IGuildUser? user = null)
        {
            user ??= (IGuildUser)Context.User;

            await Service.ByeTest((ITextChannel)Context.Channel, user).ConfigureAwait(false);
            var enabled = await Service.GetByeEnabled(Context.Guild.Id);
            if (!enabled)
                await ReplyConfirmAsync(Strings.ByemsgEnable(ctx.Guild.Id,
                        $"`{await guildSettings.GetPrefix(ctx.Guild)}bye`"))
                    .ConfigureAwait(false);
        }


        /// <summary>
        ///     Sends a test message for boosting.
        /// </summary>
        /// <remarks>
        ///     This command allows users to send a test message for boosting.
        /// </remarks>
        /// <param name="user">The user to send the test message to.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        [Ratelimit(5)]
        public async Task BoostTest([Remainder] IGuildUser? user = null)
        {
            user ??= (IGuildUser)Context.User;
            await Service.BoostTest(ctx.Channel as ITextChannel, user).ConfigureAwait(false);
            var enabled = await Service.GetBoostEnabled(Context.Guild.Id);
            if (!enabled)
                await ReplyConfirmAsync(Strings.BoostmsgEnable(ctx.Guild.Id,
                    $"`{await guildSettings.GetPrefix(ctx.Guild)}greet`")).ConfigureAwait(false);
        }

        /// <summary>
        ///     Sends a test direct message greet.
        /// </summary>
        /// <remarks>
        ///     This command allows users to send a test direct message greet. Has a rate limit of 5 seconds to prevent being used
        ///     for dm spam.
        /// </remarks>
        /// <param name="user">The user to send the test direct message greet to.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        [Ratelimit(5)]
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
                await ReplyConfirmAsync(Strings.GreetdmmsgEnable(ctx.Guild.Id,
                    $"`{await guildSettings.GetPrefix(ctx.Guild)}greetdm`")).ConfigureAwait(false);
        }
    }
}