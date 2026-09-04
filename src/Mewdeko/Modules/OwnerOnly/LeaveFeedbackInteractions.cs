using DataModel;
using Discord.Interactions;
using Mewdeko.Common.Modals;
using Mewdeko.Modules.OwnerOnly.Services;

namespace Mewdeko.Modules.OwnerOnly;

/// <summary>
///     Handles the components on the "why was I removed" dm sent to a server owner after the bot
///     leaves their server. Everything here runs in dms, so no guild context is available.
/// </summary>
public class LeaveFeedbackInteractions : MewdekoSlashModuleBase<GuildLeaveFeedbackService>
{
    /// <summary>
    ///     Stores the reason picked from the select menu and offers to collect more detail.
    /// </summary>
    /// <param name="feedbackId">The id of the feedback record the dm was sent for.</param>
    /// <param name="values">The selected reason keys.</param>
    [ComponentInteraction("leavefb:reason:*", true)]
    public async Task ReasonSelected(string feedbackId, string[] values)
    {
        var record = await Resolve(feedbackId).ConfigureAwait(false);
        if (record is null)
            return;

        var reason = values.FirstOrDefault();
        if (reason is null)
            return;

        await Service.SetReason(record.Id, reason).ConfigureAwait(false);

        var eb = new EmbedBuilder()
            .WithOkColor()
            .WithDescription(Strings.LeaveFeedbackThanks(record.GuildId,
                Service.GetReasonLabel(reason, record.GuildId)));

        var components = new ComponentBuilder()
            .WithButton(Strings.LeaveFeedbackWriteButton(record.GuildId), $"leavefb:comment:{record.Id}",
                ButtonStyle.Secondary)
            .Build();

        await ((SocketMessageComponent)ctx.Interaction)
            .UpdateAsync(x =>
            {
                x.Embed = eb.Build();
                x.Components = components;
            }).ConfigureAwait(false);

        await Service.Report(record.Id, ctx.User).ConfigureAwait(false);
    }

    /// <summary>
    ///     Opens the modal the owner writes their explanation in.
    /// </summary>
    /// <param name="feedbackId">The id of the feedback record the dm was sent for.</param>
    [ComponentInteraction("leavefb:comment:*", true)]
    public async Task CommentButton(string feedbackId)
    {
        var record = await Resolve(feedbackId).ConfigureAwait(false);
        if (record is null)
            return;

        await ctx.Interaction.RespondWithModalAsync<LeaveFeedbackModal>($"leavefb:modal:{record.Id}")
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Stores the written explanation and closes out the prompt.
    /// </summary>
    /// <param name="feedbackId">The id of the feedback record the dm was sent for.</param>
    /// <param name="modal">The submitted modal.</param>
    [ModalInteraction("leavefb:modal:*", true)]
    public async Task CommentSubmitted(string feedbackId, LeaveFeedbackModal modal)
    {
        var record = await Resolve(feedbackId).ConfigureAwait(false);
        if (record is null)
            return;

        if (!string.IsNullOrWhiteSpace(modal.Comment))
            await Service.SetComment(record.Id, modal.Comment).ConfigureAwait(false);

        var eb = new EmbedBuilder()
            .WithOkColor()
            .WithDescription(Strings.LeaveFeedbackSubmitted(record.GuildId));

        await ((SocketModal)ctx.Interaction)
            .UpdateAsync(x =>
            {
                x.Embed = eb.Build();
                x.Components = new ComponentBuilder().Build();
            }).ConfigureAwait(false);

        await Service.Report(record.Id, ctx.User).ConfigureAwait(false);
    }

    /// <summary>
    ///     Marks the prompt as dismissed and clears the components.
    /// </summary>
    /// <param name="feedbackId">The id of the feedback record the dm was sent for.</param>
    [ComponentInteraction("leavefb:dismiss:*", true)]
    public async Task Dismiss(string feedbackId)
    {
        var record = await Resolve(feedbackId).ConfigureAwait(false);
        if (record is null)
            return;

        await Service.Dismiss(record.Id).ConfigureAwait(false);

        var eb = new EmbedBuilder()
            .WithOkColor()
            .WithDescription(Strings.LeaveFeedbackDismissed(record.GuildId));

        await ((SocketMessageComponent)ctx.Interaction)
            .UpdateAsync(x =>
            {
                x.Embed = eb.Build();
                x.Components = new ComponentBuilder().Build();
            }).ConfigureAwait(false);
    }

    private async Task<GuildLeaveFeedback?> Resolve(string feedbackId)
    {
        if (!int.TryParse(feedbackId, out var id))
            return null;

        return await Service.GetForUser(id, ctx.User.Id).ConfigureAwait(false);
    }
}