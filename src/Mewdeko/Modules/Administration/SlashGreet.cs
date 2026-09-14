using System.Net.Http;
using Discord.Interactions;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Common.Modals;

namespace Mewdeko.Modules.Administration;

/// <summary>
///     Slash commands for the boost, bye and DM greet messages as well as the leave webhook.
/// </summary>
[Group("greet", "Boost, bye and DM greet messages")]
public class SlashGreet : MewdekoSlashModuleBase<GreetSettingsService>
{
    /// <summary>
    ///     Saves the message submitted through the greet message modal for the given kind of greet.
    /// </summary>
    /// <param name="kind">The greet kind: boost, bye or dm.</param>
    /// <param name="modal">The submitted modal.</param>
    [ModalInteraction("greet_message:*", true)]
    [RequireContext(ContextType.Guild)]
    public async Task GreetMessageSubmitted(string kind, GreetMessageModal modal)
    {
        var text = modal.Message;
        if (string.IsNullOrWhiteSpace(text))
        {
            await EphemeralReplyErrorAsync(Strings.MessageEmpty(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        switch (kind)
        {
            case "boost":
            {
                var enabled = await Service.SetBoostMessage(ctx.Guild.Id, text);
                await ReplyConfirmAsync(Strings.BoostmsgNew(ctx.Guild.Id)).ConfigureAwait(false);
                if (!enabled)
                {
                    await ReplyConfirmAsync(Strings.BoostmsgEnable(ctx.Guild.Id, "`/greet boost toggle`"))
                        .ConfigureAwait(false);
                }

                break;
            }
            case "bye":
            {
                var enabled = await Service.SetByeMessage(ctx.Guild.Id, text);
                await ReplyConfirmAsync(Strings.ByemsgNew(ctx.Guild.Id)).ConfigureAwait(false);
                if (!enabled)
                {
                    await ReplyConfirmAsync(Strings.ByemsgEnable(ctx.Guild.Id, "`/greet bye toggle`"))
                        .ConfigureAwait(false);
                }

                break;
            }
            case "dm":
            {
                var enabled = await Service.SetGreetDmMessage(ctx.Guild.Id, text);
                await ReplyConfirmAsync(Strings.GreetdmmsgNew(ctx.Guild.Id)).ConfigureAwait(false);
                if (!enabled)
                {
                    await ReplyConfirmAsync(Strings.GreetdmmsgEnable(ctx.Guild.Id, "`/greet dm toggle`"))
                        .ConfigureAwait(false);
                }

                break;
            }
        }
    }

    /// <summary>
    ///     Boost message settings.
    /// </summary>
    [Group("boost", "Messages sent when someone boosts the server")]
    public class GreetBoost : MewdekoSlashSubmodule<GreetSettingsService>
    {
        /// <summary>
        ///     Enables or disables boost messages. They are sent in the channel the command is used in.
        /// </summary>
        [SlashCommand("toggle", "Enables or disables boost messages in this channel")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public async Task Boost()
        {
            var enabled = await Service.SetBoost(ctx.Guild.Id, ctx.Channel.Id).ConfigureAwait(false);

            if (enabled)
                await ReplyConfirmAsync(Strings.BoostOn(ctx.Guild.Id)).ConfigureAwait(false);
            else
                await ReplyConfirmAsync(Strings.BoostOff(ctx.Guild.Id)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Opens a modal to set the boost message for the server, or displays the current boost message when show is
        ///     true.
        /// </summary>
        /// <param name="show">Whether to display the current message instead of setting a new one.</param>
        [SlashCommand("message", "Sets the boost message, or shows the current one")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public async Task BoostMsg(
            [Summary("show", "Show the current message instead of setting it")]
            bool show = false)
        {
            if (!show)
            {
                await RespondWithModalAsync<GreetMessageModal>("greet_message:boost").ConfigureAwait(false);
                return;
            }

            var boostMessage = await Service.GetBoostMessage(ctx.Guild.Id);
            await ReplyConfirmAsync(Strings.BoostmsgCur(ctx.Guild.Id, boostMessage.SanitizeMentions()));
        }

        /// <summary>
        ///     Sets the timer for deleting boost messages.
        /// </summary>
        /// <param name="timer">The timer in seconds. Must be between 0 and 600.</param>
        [SlashCommand("delete-after", "Sets how many seconds until boost messages are deleted, 0 to keep them")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public async Task BoostDel([Summary("seconds", "Seconds before deletion, 0 to 600")] int timer = 30)
        {
            if (timer is < 0 or > 600)
            {
                await ErrorAsync(Strings.Maxdeletetime(ctx.Guild.Id, "600 seconds")).ConfigureAwait(false);
                return;
            }

            await Service.SetBoostDel(ctx.Guild.Id, timer).ConfigureAwait(false);

            if (timer > 0)
                await ReplyConfirmAsync(Strings.BoostdelOn(ctx.Guild.Id, timer)).ConfigureAwait(false);
            else
                await ReplyConfirmAsync(Strings.BoostdelOff(ctx.Guild.Id)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Sends a test boost message.
        /// </summary>
        /// <param name="user">The user to send the test message for. Defaults to the caller.</param>
        [SlashCommand("test", "Sends a test boost message")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        [InteractionRatelimit(5)]
        public async Task BoostTest([Summary("user", "The user to test with, defaults to you")] IGuildUser? user = null)
        {
            user ??= (IGuildUser)ctx.User;
            await DeferAsync().ConfigureAwait(false);
            await Service.BoostTest((ITextChannel)ctx.Channel, user).ConfigureAwait(false);
            var enabled = await Service.GetBoostEnabled(ctx.Guild.Id);
            if (!enabled)
            {
                await ReplyConfirmAsync(Strings.BoostmsgEnable(ctx.Guild.Id, "`/greet boost toggle`"))
                    .ConfigureAwait(false);
            }
            else
            {
                await ReplyConfirmAsync(Strings.GreetTestSent(ctx.Guild.Id)).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    ///     Bye message settings.
    /// </summary>
    [Group("bye", "Messages sent when someone leaves the server")]
    public class GreetBye : MewdekoSlashSubmodule<GreetSettingsService>
    {
        /// <summary>
        ///     Toggles the sending of a bye message. It is sent in the channel the command is used in.
        /// </summary>
        [SlashCommand("toggle", "Enables or disables bye messages in this channel")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public async Task Bye()
        {
            var enabled = await Service.SetBye(ctx.Guild.Id, ctx.Channel.Id).ConfigureAwait(false);

            if (enabled)
                await ReplyConfirmAsync(Strings.ByeOn(ctx.Guild.Id)).ConfigureAwait(false);
            else
                await ReplyConfirmAsync(Strings.ByeOff(ctx.Guild.Id)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Opens a modal to set the bye message, or displays the current bye message when show is true.
        /// </summary>
        /// <param name="show">Whether to display the current message instead of setting a new one.</param>
        [SlashCommand("message", "Sets the bye message, or shows the current one")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public async Task ByeMsg([Summary("show", "Show the current message instead of setting it")] bool show = false)
        {
            if (!show)
            {
                await RespondWithModalAsync<GreetMessageModal>("greet_message:bye").ConfigureAwait(false);
                return;
            }

            var byeMsg = await Service.GetByeMessage(ctx.Guild.Id);
            await ReplyConfirmAsync(Strings.ByemsgCur(ctx.Guild.Id, byeMsg.SanitizeMentions()));
        }

        /// <summary>
        ///     Sets the timer for deleting bye messages.
        /// </summary>
        /// <param name="timer">The timer duration in seconds.</param>
        [SlashCommand("delete-after", "Sets how many seconds until bye messages are deleted, 0 to keep them")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public async Task ByeDel([Summary("seconds", "Seconds before deletion, 0 to keep")] int timer = 30)
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
        /// <param name="user">The user to send the test bye message for. Defaults to the caller.</param>
        [SlashCommand("test", "Sends a test bye message")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        [InteractionRatelimit(5)]
        public async Task ByeTest([Summary("user", "The user to test with, defaults to you")] IGuildUser? user = null)
        {
            user ??= (IGuildUser)ctx.User;
            await DeferAsync().ConfigureAwait(false);
            await Service.ByeTest((ITextChannel)ctx.Channel, user).ConfigureAwait(false);
            var enabled = await Service.GetByeEnabled(ctx.Guild.Id);
            if (!enabled)
            {
                await ReplyConfirmAsync(Strings.ByemsgEnable(ctx.Guild.Id, "`/greet bye toggle`"))
                    .ConfigureAwait(false);
            }
            else
            {
                await ReplyConfirmAsync(Strings.GreetTestSent(ctx.Guild.Id)).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    ///     DM greet message settings.
    /// </summary>
    [Group("dm", "Greeting messages sent to new members by direct message")]
    public class GreetDmCommands : MewdekoSlashSubmodule<GreetSettingsService>
    {
        /// <summary>
        ///     Toggles the sending of greeting messages via direct message.
        /// </summary>
        [SlashCommand("toggle", "Enables or disables DM greet messages")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageGuild)]
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
        ///     Opens a modal to set the direct message greeting message, or displays the current one when show is true.
        /// </summary>
        /// <param name="show">Whether to display the current message instead of setting a new one.</param>
        [SlashCommand("message", "Sets the DM greet message, or shows the current one")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public async Task GreetDmMsg(
            [Summary("show", "Show the current message instead of setting it")]
            bool show = false)
        {
            if (!show)
            {
                await RespondWithModalAsync<GreetMessageModal>("greet_message:dm").ConfigureAwait(false);
                return;
            }

            var dmGreetMsg = await Service.GetDmGreetMsg(ctx.Guild.Id);
            await ReplyConfirmAsync(Strings.GreetdmmsgCur(ctx.Guild.Id, dmGreetMsg.SanitizeMentions()));
        }

        /// <summary>
        ///     Sends a test direct message greet. Rate limited to prevent being used for dm spam.
        /// </summary>
        /// <param name="user">The user to send the test direct message greet to. Defaults to the caller.</param>
        [SlashCommand("test", "Sends a test DM greet")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        [InteractionRatelimit(5)]
        public async Task GreetDmTest([Summary("user", "The user to dm, defaults to you")] IGuildUser? user = null)
        {
            user ??= (IGuildUser)ctx.User;
            await DeferAsync().ConfigureAwait(false);

            var channel = await user.CreateDMChannelAsync().ConfigureAwait(false);
            var success = await Service.GreetDmTest(channel, user).ConfigureAwait(false);
            if (success)
                await ReplyConfirmAsync(Strings.GreetTestSent(ctx.Guild.Id)).ConfigureAwait(false);
            else
                await ReplyErrorAsync(Strings.CantDm(ctx.Guild.Id)).ConfigureAwait(false);
            var enabled = await Service.GetGreetDmEnabled(ctx.Guild.Id);
            if (!enabled)
            {
                await ReplyConfirmAsync(Strings.GreetdmmsgEnable(ctx.Guild.Id, "`/greet dm toggle`"))
                    .ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    ///     Leave webhook settings.
    /// </summary>
    /// <param name="fact">Client factory used to download the webhook avatar.</param>
    [Group("leave-hook", "Webhook used to send bye messages")]
    public class GreetLeaveHook(IHttpClientFactory fact) : MewdekoSlashSubmodule<GreetSettingsService>
    {
        /// <summary>
        ///     Creates a webhook in the given channel and uses it for leave messages, optionally with an avatar. Bye messages
        ///     are toggled in the channel the command is used in. Omitting the channel clears the leave webhook so bye
        ///     messages are sent by the bot again.
        /// </summary>
        /// <param name="channel">The text channel to create the webhook in. Omit to clear the webhook.</param>
        /// <param name="name">The name of the webhook. Defaults to the bot's name.</param>
        /// <param name="image">The URL of the webhook avatar.</param>
        /// <param name="attachment">The webhook avatar as an attachment, used when no URL is given.</param>
        [SlashCommand("set", "Creates a webhook for leave messages, or clears it when no channel is given")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public async Task LeaveHook(
            [Summary("channel", "The channel the webhook posts in, omit to clear the webhook")]
            ITextChannel? channel = null,
            [Summary("name", "The webhook name")] string? name = null,
            [Summary("image", "The webhook avatar url")]
            string? image = null,
            [Summary("attachment", "The webhook avatar, used when no url is given")]
            IAttachment? attachment = null)
        {
            if (channel is null)
            {
                await Service.SetWebLeaveUrl(ctx.Guild, "").ConfigureAwait(false);
                await ConfirmAsync(Strings.Leavehookdisabled(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await DeferAsync().ConfigureAwait(false);

            name ??= ctx.Client.CurrentUser.Username;
            var imageUrl = image ?? attachment?.Url;
            if (imageUrl != null)
            {
                var webhook = await CreateWebhook(channel, name, imageUrl).ConfigureAwait(false);
                var txt = $"https://discord.com/api/webhooks/{webhook.Id}/{webhook.Token}";
                await Service.SetWebLeaveUrl(ctx.Guild, txt).ConfigureAwait(false);
            }
            else
            {
                var webhook = await channel.CreateWebhookAsync(name).ConfigureAwait(false);
                var txt = $"https://discord.com/api/webhooks/{webhook.Id}/{webhook.Token}";
                await Service.SetWebLeaveUrl(ctx.Guild, txt).ConfigureAwait(false);
            }

            var enabled = await Service.SetBye(ctx.Guild.Id, ctx.Channel.Id).ConfigureAwait(false);
            var message = enabled
                ? Strings.Leavehookset(ctx.Guild.Id)
                : Strings.Leavehooksettwo(ctx.Guild.Id, "`/greet bye toggle`");
            await ConfirmAsync(message).ConfigureAwait(false);
        }

        private async Task<IWebhook> CreateWebhook(ITextChannel chan, string name, string imageUrl)
        {
            using var http = fact.CreateClient();
            var uri = new Uri(imageUrl);
            using var sr = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            var imgData = await sr.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            var imgStream = imgData.ToStream();
            await using var _ = imgStream.ConfigureAwait(false);
            return await chan.CreateWebhookAsync(name, imgStream).ConfigureAwait(false);
        }
    }
}