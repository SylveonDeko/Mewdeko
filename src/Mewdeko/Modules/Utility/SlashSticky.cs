using Discord.Interactions;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Common.Modals;
using Mewdeko.Modules.Utility.Common;
using Mewdeko.Modules.Utility.Services;

namespace Mewdeko.Modules.Utility;

/// <summary>
///     Slash commands for sticky messages that stay at the bottom of a channel.
/// </summary>
[Group("sticky", "Messages that stay at the bottom of a channel")]
public class SlashSticky : MewdekoSlashModuleBase<StickyService>
{
    /// <summary>
    ///     Sticks a message to a channel. Opens a modal when no message is given.
    /// </summary>
    /// <param name="message">The message text or embed json. Leave empty to write it in a form.</param>
    /// <param name="channel">The channel to stick it to. Defaults to this channel.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [SlashCommand("stick", "Sticks a message to a channel")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageMessages)]
    public async Task Stick(
        [Summary("message", "Text or embed json. Leave empty to write it in a form")]
        string? message = null,
        [Summary("channel", "Channel to stick it to. Defaults to this channel")]
        ITextChannel? channel = null)
    {
        var channelId = channel?.Id ?? ctx.Channel.Id;
        if (string.IsNullOrWhiteSpace(message))
        {
            await RespondWithModalAsync<StickyModal>($"sticky_stick:{channelId}").ConfigureAwait(false);
            return;
        }

        await StickTo(channelId, message).ConfigureAwait(false);
    }

    /// <summary>
    ///     Handles the sticky modal submission.
    /// </summary>
    /// <param name="channelId">The ID of the channel to stick the message to.</param>
    /// <param name="modal">The submitted modal.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [ModalInteraction("sticky_stick:*", true)]
    public async Task StickSubmitted(string channelId, StickyModal modal)
    {
        if (!ulong.TryParse(channelId, out var id))
            return;

        await StickTo(id, modal.Content).ConfigureAwait(false);
    }

    /// <summary>
    ///     Changes the sticky message in a channel. Opens a modal when no message is given.
    /// </summary>
    /// <param name="channel">The channel the sticky is in.</param>
    /// <param name="message">The new message text or embed json. Leave empty to write it in a form.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [SlashCommand("edit", "Changes the sticky message in a channel")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageMessages)]
    public async Task Edit(
        [Summary("channel", "The channel the sticky is in")]
        ITextChannel channel,
        [Summary("message", "Text or embed json. Leave empty to write it in a form")]
        string? message = null)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            if (Service.GetSticky(ctx.Guild.Id, channel.Id) == null)
            {
                await EphemeralReplyErrorAsync(Strings.StickNone(ctx.Guild.Id, channel.Id)).ConfigureAwait(false);
                return;
            }

            await RespondWithModalAsync<StickyModal>($"sticky_stick:{channel.Id}").ConfigureAwait(false);
            return;
        }

        if (!Service.Ready)
            return;

        if (!await Service.EditAsync(ctx.Guild.Id, channel.Id, message).ConfigureAwait(false))
        {
            await EphemeralReplyErrorAsync(Strings.StickNone(ctx.Guild.Id, channel.Id)).ConfigureAwait(false);
            return;
        }

        await ReplyConfirmAsync(Strings.StickEdited(ctx.Guild.Id, channel.Id)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Removes the sticky message from a channel.
    /// </summary>
    /// <param name="channel">The channel to unstick. Defaults to this channel.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [SlashCommand("remove", "Removes the sticky message from a channel")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageMessages)]
    public async Task Unstick(
        [Summary("channel", "Channel to unstick. Defaults to this channel")]
        ITextChannel? channel = null)
    {
        if (!Service.Ready)
            return;

        var channelId = channel?.Id ?? ctx.Channel.Id;
        await DeferAsync().ConfigureAwait(false);
        if (!await Service.UnstickAsync(ctx.Guild.Id, channelId).ConfigureAwait(false))
        {
            await ctx.Interaction.FollowupAsync(Strings.StickNone(ctx.Guild.Id, channelId), ephemeral: true)
                .ConfigureAwait(false);
            return;
        }

        await ctx.Interaction.FollowupAsync(Strings.StickRemoved(ctx.Guild.Id, channelId)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Lists the sticky messages in the server.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [SlashCommand("list", "Lists the sticky messages in the server")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageMessages)]
    public async Task List()
    {
        if (!Service.Ready)
            return;

        var stickies = Service.GetStickies(ctx.Guild.Id);
        if (stickies.Count == 0)
        {
            await EphemeralReplyErrorAsync(Strings.StickListEmpty(ctx.Guild.Id)).ConfigureAwait(false);
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

        await RespondAsync(embed: embed.Build()).ConfigureAwait(false);
    }

    /// <summary>
    ///     Removes every sticky message in the server.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [SlashCommand("clear", "Removes every sticky message in the server")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageMessages)]
    public async Task Clear()
    {
        if (!Service.Ready)
            return;

        await DeferAsync().ConfigureAwait(false);
        var removed = await Service.ClearAsync(ctx.Guild.Id).ConfigureAwait(false);
        if (removed == 0)
        {
            await ctx.Interaction.FollowupAsync(Strings.StickListEmpty(ctx.Guild.Id), ephemeral: true)
                .ConfigureAwait(false);
            return;
        }

        await ctx.Interaction.FollowupAsync(Strings.StickCleared(ctx.Guild.Id, removed)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets after how many messages or how much time a channel's sticky returns to the bottom.
    /// </summary>
    /// <param name="channel">The channel the sticky is in.</param>
    /// <param name="messages">How many new messages bring the sticky back.</param>
    /// <param name="seconds">How many seconds after the first new message the sticky comes back.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [SlashCommand("settings", "Sets when a sticky returns to the bottom")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageMessages)]
    public async Task Settings(
        [Summary("channel", "The channel the sticky is in")]
        ITextChannel channel,
        [Summary("messages", "New messages before it returns")] [MinValue(1)] [MaxValue(1000)]
        int messages,
        [Summary("seconds", "Seconds of new chat before it returns")] [MinValue(5)] [MaxValue(21600)]
        int seconds)
    {
        if (!Service.Ready)
            return;

        if (!await Service.TuneAsync(ctx.Guild.Id, channel.Id, messages, TimeSpan.FromSeconds(seconds))
                .ConfigureAwait(false))
        {
            await EphemeralReplyErrorAsync(Strings.StickNone(ctx.Guild.Id, channel.Id)).ConfigureAwait(false);
            return;
        }

        await ReplyConfirmAsync(Strings.StickSettingsSet(ctx.Guild.Id, channel.Id, messages,
            StickyFormat.Window(TimeSpan.FromSeconds(seconds).ToString()))).ConfigureAwait(false);
    }

    /// <summary>
    ///     Saves a sticky template. Opens a modal when no message is given.
    /// </summary>
    /// <param name="name">The template name.</param>
    /// <param name="message">The message text or embed json. Leave empty to write it in a form.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [SlashCommand("template-save", "Saves a sticky template")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageMessages)]
    public async Task TemplateSave(
        [Summary("name", "The template name")] string name,
        [Summary("message", "Text or embed json. Leave empty to write it in a form")]
        string? message = null)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            await RespondWithModalAsync<StickyModal>($"sticky_template:{name}").ConfigureAwait(false);
            return;
        }

        await SaveTemplate(name, message).ConfigureAwait(false);
    }

    /// <summary>
    ///     Handles the sticky template modal submission.
    /// </summary>
    /// <param name="name">The template name.</param>
    /// <param name="modal">The submitted modal.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [ModalInteraction("sticky_template:*", true)]
    public Task TemplateSaveSubmitted(string name, StickyModal modal)
    {
        return SaveTemplate(name, modal.Content);
    }

    /// <summary>
    ///     Lists the sticky templates in the server.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [SlashCommand("template-list", "Lists the sticky templates")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageMessages)]
    public async Task TemplateList()
    {
        var templates = await Service.GetTemplatesAsync(ctx.Guild.Id).ConfigureAwait(false);
        if (templates.Count == 0)
        {
            await EphemeralReplyErrorAsync(Strings.StickTemplateNone(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await RespondAsync(embed: new EmbedBuilder()
            .WithOkColor()
            .WithTitle(Strings.StickTemplateListTitle(ctx.Guild.Id))
            .WithDescription(string.Join("\n", templates.Select(t => Format.Code(t.Name)))).Build()).ConfigureAwait(false);
    }

    /// <summary>
    ///     Deletes a sticky template.
    /// </summary>
    /// <param name="name">The template name.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [SlashCommand("template-delete", "Deletes a sticky template")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageMessages)]
    public async Task TemplateDelete([Summary("name", "The template name")] string name)
    {
        if (!await Service.DeleteTemplateAsync(ctx.Guild.Id, name).ConfigureAwait(false))
        {
            await EphemeralReplyErrorAsync(Strings.StickTemplateNotFound(ctx.Guild.Id, name)).ConfigureAwait(false);
            return;
        }

        await ReplyConfirmAsync(Strings.StickTemplateDeleted(ctx.Guild.Id, name)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Sticks a saved template to a channel.
    /// </summary>
    /// <param name="name">The template name.</param>
    /// <param name="channel">The channel to stick it to. Defaults to this channel.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [SlashCommand("template-use", "Sticks a saved template to a channel")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageMessages)]
    public async Task TemplateUse(
        [Summary("name", "The template name")] string name,
        [Summary("channel", "Channel to stick it to. Defaults to this channel")]
        ITextChannel? channel = null)
    {
        var template = await Service.GetTemplateAsync(ctx.Guild.Id, name).ConfigureAwait(false);
        if (template == null)
        {
            await EphemeralReplyErrorAsync(Strings.StickTemplateNotFound(ctx.Guild.Id, name)).ConfigureAwait(false);
            return;
        }

        await StickTo(channel?.Id ?? ctx.Channel.Id, template.Content).ConfigureAwait(false);
    }

    private async Task SaveTemplate(string name, string content)
    {
        var added = await Service.SaveTemplateAsync(ctx.Guild.Id, name, content).ConfigureAwait(false);
        await ReplyConfirmAsync(added
            ? Strings.StickTemplateSaved(ctx.Guild.Id, name)
            : Strings.StickTemplateUpdated(ctx.Guild.Id, name)).ConfigureAwait(false);
    }

    private async Task StickTo(ulong channelId, string content)
    {
        if (!Service.Ready)
            return;

        await DeferAsync().ConfigureAwait(false);
        var result = await Service.StickAsync(ctx.Guild.Id, channelId, content).ConfigureAwait(false);
        var text = result switch
        {
            StickyResult.Created => Strings.StickCreated(ctx.Guild.Id, channelId, StickyDefaults.Messages,
                StickyFormat.Window(StickyDefaults.Window.ToString())),
            StickyResult.Replaced => Strings.StickEdited(ctx.Guild.Id, channelId),
            _ => Strings.RepeatCreationFailed(ctx.Guild.Id)
        };
        await ctx.Interaction.FollowupAsync(text, ephemeral: result == StickyResult.Failed).ConfigureAwait(false);
    }
}
