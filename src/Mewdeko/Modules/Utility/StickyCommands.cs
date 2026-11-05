using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Common.TypeReaders.Models;
using Mewdeko.Modules.Utility.Common;
using Mewdeko.Modules.Utility.Services;

namespace Mewdeko.Modules.Utility;

public partial class Utility
{
    /// <summary>
    ///     Commands for sticky messages that stay at the bottom of a channel.
    /// </summary>
    [Group]
    public class StickyCommands : MewdekoSubmodule<StickyService>
    {
        /// <summary>
        ///     Sticks a message to the current channel.
        /// </summary>
        /// <param name="content">The message text or embed json. Can span several lines.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageMessages)]
        public Task Stick([Remainder] string content)
        {
            return StickTo(ctx.Channel.Id, content);
        }

        /// <summary>
        ///     Sticks a message to a channel.
        /// </summary>
        /// <param name="channel">The channel to stick the message to.</param>
        /// <param name="content">The message text or embed json. Can span several lines.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageMessages)]
        public Task StickIn(ITextChannel channel, [Remainder] string content)
        {
            return StickTo(channel.Id, content);
        }

        /// <summary>
        ///     Changes the sticky message in a channel.
        /// </summary>
        /// <param name="channel">The channel the sticky is in.</param>
        /// <param name="content">The new message text or embed json.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageMessages)]
        public async Task StickyEdit(ITextChannel channel, [Remainder] string content)
        {
            if (!Service.Ready)
                return;

            if (!await Service.EditAsync(ctx.Guild.Id, channel.Id, content).ConfigureAwait(false))
            {
                await ReplyErrorAsync(Strings.StickNone(ctx.Guild.Id, channel.Id)).ConfigureAwait(false);
                return;
            }

            await ReplyConfirmAsync(Strings.StickEdited(ctx.Guild.Id, channel.Id)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Removes the sticky message from a channel.
        /// </summary>
        /// <param name="channel">The channel to unstick. Defaults to the current channel.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageMessages)]
        public async Task Unstick(ITextChannel? channel = null)
        {
            if (!Service.Ready)
                return;

            var channelId = channel?.Id ?? ctx.Channel.Id;
            if (!await Service.UnstickAsync(ctx.Guild.Id, channelId).ConfigureAwait(false))
            {
                await ReplyErrorAsync(Strings.StickNone(ctx.Guild.Id, channelId)).ConfigureAwait(false);
                return;
            }

            await ReplyConfirmAsync(Strings.StickRemoved(ctx.Guild.Id, channelId)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Lists the sticky messages in the server.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageMessages)]
        public async Task StickyList()
        {
            if (!Service.Ready)
                return;

            var stickies = Service.GetStickies(ctx.Guild.Id);
            if (stickies.Count == 0)
            {
                await ReplyErrorAsync(Strings.StickListEmpty(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var embed = new EmbedBuilder().WithOkColor().WithTitle(Strings.StickListTitle(ctx.Guild.Id));
            foreach (var sticky in stickies.Take(25))
            {
                var preview = sticky.Repeater.Message ?? "";
                if (preview.Length > 100)
                    preview = $"{preview[..100]}...";

                var every = Strings.StickListEvery(ctx.Guild.Id, sticky.Repeater.ActivityThreshold,
                    StickyFormat.Window(sticky.Repeater.ActivityTimeWindow));
                embed.AddField($"<#{sticky.Repeater.ChannelId}>", $"{every}\n{Format.Sanitize(preview)}");
            }

            await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        /// <summary>
        ///     Removes every sticky message in the server.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageMessages)]
        public async Task StickyClear()
        {
            if (!Service.Ready)
                return;

            var removed = await Service.ClearAsync(ctx.Guild.Id).ConfigureAwait(false);
            if (removed == 0)
            {
                await ReplyErrorAsync(Strings.StickListEmpty(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await ReplyConfirmAsync(Strings.StickCleared(ctx.Guild.Id, removed)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets after how many messages or how much time a channel's sticky returns to the bottom.
        /// </summary>
        /// <param name="channel">The channel the sticky is in.</param>
        /// <param name="messages">How many new messages bring the sticky back.</param>
        /// <param name="window">How long after the first new message the sticky comes back.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageMessages)]
        public async Task StickySet(ITextChannel channel, int messages, StoopidTime window)
        {
            if (!Service.Ready)
                return;

            if (messages < 1 || messages > 1000 || window.Time < TimeSpan.FromSeconds(5) ||
                window.Time > TimeSpan.FromHours(6))
            {
                await ReplyErrorAsync(Strings.StickSettingsInvalid(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            if (!await Service.TuneAsync(ctx.Guild.Id, channel.Id, messages, window.Time).ConfigureAwait(false))
            {
                await ReplyErrorAsync(Strings.StickNone(ctx.Guild.Id, channel.Id)).ConfigureAwait(false);
                return;
            }

            await ReplyConfirmAsync(Strings.StickSettingsSet(ctx.Guild.Id, channel.Id, messages,
                StickyFormat.Window(window.Time.ToString()))).ConfigureAwait(false);
        }

        /// <summary>
        ///     Saves a sticky template.
        /// </summary>
        /// <param name="name">The template name.</param>
        /// <param name="content">The message text or embed json.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageMessages)]
        public async Task StickyTemplateSave(string name, [Remainder] string content)
        {
            var added = await Service.SaveTemplateAsync(ctx.Guild.Id, name, content).ConfigureAwait(false);
            await ReplyConfirmAsync(added
                ? Strings.StickTemplateSaved(ctx.Guild.Id, name)
                : Strings.StickTemplateUpdated(ctx.Guild.Id, name)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Lists the sticky templates in the server.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageMessages)]
        public async Task StickyTemplateList()
        {
            var templates = await Service.GetTemplatesAsync(ctx.Guild.Id).ConfigureAwait(false);
            if (templates.Count == 0)
            {
                await ReplyErrorAsync(Strings.StickTemplateNone(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await ctx.Channel.EmbedAsync(new EmbedBuilder()
                .WithOkColor()
                .WithTitle(Strings.StickTemplateListTitle(ctx.Guild.Id))
                .WithDescription(string.Join("\n", templates.Select(t => Format.Code(t.Name))))).ConfigureAwait(false);
        }

        /// <summary>
        ///     Deletes a sticky template.
        /// </summary>
        /// <param name="name">The template name.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageMessages)]
        public async Task StickyTemplateDelete(string name)
        {
            if (!await Service.DeleteTemplateAsync(ctx.Guild.Id, name).ConfigureAwait(false))
            {
                await ReplyErrorAsync(Strings.StickTemplateNotFound(ctx.Guild.Id, name)).ConfigureAwait(false);
                return;
            }

            await ReplyConfirmAsync(Strings.StickTemplateDeleted(ctx.Guild.Id, name)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Sticks a saved template to a channel.
        /// </summary>
        /// <param name="name">The template name.</param>
        /// <param name="channel">The channel to stick it to. Defaults to the current channel.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageMessages)]
        public async Task StickyTemplateUse(string name, ITextChannel? channel = null)
        {
            var template = await Service.GetTemplateAsync(ctx.Guild.Id, name).ConfigureAwait(false);
            if (template == null)
            {
                await ReplyErrorAsync(Strings.StickTemplateNotFound(ctx.Guild.Id, name)).ConfigureAwait(false);
                return;
            }

            await StickTo(channel?.Id ?? ctx.Channel.Id, template.Content).ConfigureAwait(false);
        }

        private async Task StickTo(ulong channelId, string content)
        {
            if (!Service.Ready)
                return;

            var result = await Service.StickAsync(ctx.Guild.Id, channelId, content).ConfigureAwait(false);
            switch (result)
            {
                case StickyResult.Created:
                    await ReplyConfirmAsync(Strings.StickCreated(ctx.Guild.Id, channelId, StickyDefaults.Messages,
                        StickyFormat.Window(StickyDefaults.Window.ToString()))).ConfigureAwait(false);
                    break;
                case StickyResult.Replaced:
                    await ReplyConfirmAsync(Strings.StickEdited(ctx.Guild.Id, channelId)).ConfigureAwait(false);
                    break;
                default:
                    await ReplyErrorAsync(Strings.RepeatCreationFailed(ctx.Guild.Id)).ConfigureAwait(false);
                    break;
            }
        }
    }
}
