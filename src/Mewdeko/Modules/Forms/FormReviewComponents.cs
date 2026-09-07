using DataModel;
using Discord.Interactions;
using Mewdeko.Common.Modals;
using Mewdeko.Database.Enums;
using Mewdeko.Modules.Forms.Services;

namespace Mewdeko.Modules.Forms;

/// <summary>
///     Handles the approve and reject buttons posted alongside a form submission in Discord.
/// </summary>
/// <remarks>
///     Reviewing a queue is moderator work. Requiring the dashboard for it means handing out far
///     more access than the job needs, so the decision is available where the submission already
///     lands.
/// </remarks>
/// <param name="service">The forms service.</param>
public class FormReviewComponents(FormsService service) : MewdekoSlashCommandModule
{
    /// <summary>
    ///     Approves a response from the button on its submission message.
    /// </summary>
    /// <param name="responseId">The response being approved.</param>
    [ComponentInteraction($"{FormsService.ReviewComponentPrefix}:approve:*", true)]
    public async Task Approve(string responseId)
    {
        if (!int.TryParse(responseId, out var id))
            return;

        var context = await ResolveAsync(id);
        if (context == null)
            return;

        await DeferAsync(true);

        var (success, inviteCode) = await service.ApproveResponseAsync(id, ctx.User.Id, null);

        if (!success)
        {
            await FollowupAsync("That response could not be approved. It may already have been decided.",
                ephemeral: true);
            return;
        }

        await service.SettleReviewMessageAsync(context, id, true, ctx.User.Id, null);

        var confirmation = string.IsNullOrWhiteSpace(inviteCode)
            ? $"Approved response #{id}."
            : $"Approved response #{id} and created invite discord.gg/{inviteCode}.";

        await FollowupAsync(confirmation, ephemeral: true);
    }

    /// <summary>
    ///     Opens the reason modal for a rejection. The decision is not recorded until the reason is
    ///     submitted, because the reason is what the submitter is told.
    /// </summary>
    /// <param name="responseId">The response being rejected.</param>
    [ComponentInteraction($"{FormsService.ReviewComponentPrefix}:reject:*", true)]
    public async Task PromptReject(string responseId)
    {
        if (!int.TryParse(responseId, out var id))
            return;

        if (await ResolveAsync(id) == null)
            return;

        await ctx.Interaction.RespondWithModalAsync<FormRejectModal>(
            $"{FormsService.ReviewComponentPrefix}:reject_reason:{id}");
    }

    /// <summary>
    ///     Rejects a response once the reviewer has given a reason.
    /// </summary>
    /// <param name="responseId">The response being rejected.</param>
    /// <param name="modal">The reason the reviewer gave.</param>
    [ModalInteraction($"{FormsService.ReviewComponentPrefix}:reject_reason:*", true)]
    public async Task Reject(string responseId, FormRejectModal modal)
    {
        if (!int.TryParse(responseId, out var id))
            return;

        // Permission is checked again here, because the modal is a separate interaction and the
        // reviewer's roles could have changed between opening it and submitting it.
        var context = await ResolveAsync(id);
        if (context == null)
            return;

        await DeferAsync(true);

        var reason = modal.Reason?.Trim();

        if (string.IsNullOrWhiteSpace(reason))
        {
            await FollowupAsync("A reason is required, because it is sent to the submitter.", ephemeral: true);
            return;
        }

        if (!await service.RejectResponseAsync(id, ctx.User.Id, reason))
        {
            await FollowupAsync("That response could not be rejected. It may already have been decided.",
                ephemeral: true);
            return;
        }

        await service.SettleReviewMessageAsync(context, id, false, ctx.User.Id, reason);

        await FollowupAsync($"Rejected response #{id}.", ephemeral: true);
    }

    /// <summary>
    ///     Finds the form a response belongs to, having checked that the person pressing the button
    ///     is allowed to decide it and that the response is still open.
    /// </summary>
    /// <param name="responseId">The response.</param>
    /// <returns>The form, or null when the interaction was answered with a refusal instead.</returns>
    private async Task<Form?> ResolveAsync(int responseId)
    {
        var response = await service.GetResponseDetailsAsync(responseId);
        var form = response == null ? null : await service.GetFormAsync(response.FormId);

        if (form == null)
        {
            await RespondAsync("That form no longer exists.", ephemeral: true);
            return null;
        }

        if (ctx.User is not IGuildUser reviewer)
        {
            await RespondAsync("This can only be used in the server the form belongs to.", ephemeral: true);
            return null;
        }

        if (!FormsService.CanReview(form, reviewer))
        {
            var required = form.ReviewerRoleId is { } roleId and > 0
                ? $"the <@&{roleId}> role"
                : "the Manage Server permission";

            await RespondAsync($"You need {required} to decide responses to this form.", ephemeral: true);
            return null;
        }

        var workflow = await service.GetWorkflowByResponseIdAsync(responseId);

        if (workflow != null &&
            (ResponseStatus)workflow.Status is ResponseStatus.Approved or ResponseStatus.Rejected)
        {
            await RespondAsync(
                $"Response #{responseId} was already {((ResponseStatus)workflow.Status).ToString().ToLowerInvariant()}.",
                ephemeral: true);
            return null;
        }

        return form;
    }
}