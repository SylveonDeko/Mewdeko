using System.Text.Json;
using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Database.Enums;
using Mewdeko.Modules.Forms.Common;

namespace Mewdeko.Modules.Forms.Services;

/// <summary>
///     Changing a response after it has been submitted, and looking up what someone has submitted.
/// </summary>
/// <remarks>
///     A reviewer asking a submitter to clarify an answer is the ordinary case here. Editing keeps
///     the conversation on the response that is already under review rather than forcing a second
///     submission, and every edit snapshots what it replaced, so the wording a reviewer originally
///     read is never quietly rewritten underneath them.
/// </remarks>
public partial class FormsService
{
    /// <summary>
    ///     Whether a response may still be changed by the person who submitted it.
    /// </summary>
    /// <param name="responseId">The response.</param>
    /// <returns>Whether it may be edited, and why not when it may not.</returns>
    public async Task<(bool CanEdit, string? Reason)> CanEditResponseAsync(int responseId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var response = await db.FormResponses.FirstOrDefaultAsync(r => r.Id == responseId);
        if (response == null)
            return (false, "Response not found");

        var form = await GetFormAsync(response.FormId);
        if (form == null)
            return (false, "Form not found");

        if (form.AllowAnonymous)
            return (false, "Anonymous responses cannot be edited, because there is no way to tell who owns one");

        var workflow = await db.FormResponseWorkflows.FirstOrDefaultAsync(w => w.ResponseId == responseId);

        // A decided response is part of the record of that decision, so it stops being editable the
        // moment somebody rules on it.
        if (workflow != null && (ResponseStatus)workflow.Status is ResponseStatus.Approved or ResponseStatus.Rejected)
            return (false, "This response has already been reviewed and can no longer be changed");

        if (form.ExpiresAt is { } expiresAt && expiresAt <= DateTime.UtcNow)
            return (false, "This form has closed, so its responses can no longer be changed");

        return (true, null);
    }

    /// <summary>
    ///     Finds a form's public share code, so a submitter can be pointed back at the form itself
    ///     from a page that only knows their response.
    /// </summary>
    /// <param name="formId">The form.</param>
    /// <returns>An active share code, or null when the form has never been shared.</returns>
    public async Task<string?> GetShareCodeAsync(int formId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        return await db.FormShareLinks
            .Where(l => l.FormId == formId
                        && l.IsActive
                        && (l.ExpiresAt == null || l.ExpiresAt > DateTime.UtcNow))
            .OrderByDescending(l => l.CreatedAt)
            .Select(l => l.ShareCode)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    ///     Replaces the answers of a response with a corrected set, keeping the original as a revision.
    /// </summary>
    /// <param name="responseId">The response to change.</param>
    /// <param name="userId">Who is making the change, which must be the person who submitted it.</param>
    /// <param name="answers">The corrected answers, keyed by question identifier.</param>
    /// <returns>The updated response.</returns>
    /// <exception cref="FormSubmissionException">Thrown when the corrected answers do not pass validation.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the response cannot be edited.</exception>
    public async Task<FormResponse> EditResponseAsync(
        int responseId,
        ulong userId,
        Dictionary<int, object> answers)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var response = await db.FormResponses.FirstOrDefaultAsync(r => r.Id == responseId)
                       ?? throw new InvalidOperationException("Response not found");

        if (response.UserId != userId)
            throw new InvalidOperationException("This response was submitted by somebody else");

        var (canEdit, reason) = await CanEditResponseAsync(responseId);
        if (!canEdit)
            throw new InvalidOperationException(reason);

        var form = await GetFormAsync(response.FormId)
                   ?? throw new InvalidOperationException("Form not found");

        var check = await PrepareAnswersAsync(form, answers, userId);

        if (check.Errors.Count > 0)
            throw new FormSubmissionException(check.Errors);

        var existing = await db.FormAnswers
            .Where(a => a.ResponseId == responseId)
            .ToListAsync();

        // The original is captured before anything is overwritten, so a failure below leaves the
        // response as it was rather than half replaced.
        await db.InsertAsync(new FormResponseRevision
        {
            ResponseId = responseId,
            Snapshot = JsonSerializer.Serialize(existing),
            EditedBy = userId,
            CreatedAt = DateTime.UtcNow
        });

        await db.FormAnswers.Where(a => a.ResponseId == responseId).DeleteAsync();

        await StoreAnswersAsync(db, responseId, response.FormVersionId, check);

        var editedAt = DateTime.UtcNow;

        await db.FormResponses
            .Where(r => r.Id == responseId)
            .Set(r => r.EditedAt, editedAt)
            .UpdateAsync();

        response.EditedAt = editedAt;

        logger.LogInformation("User {UserId} edited response {ResponseId}", userId, responseId);

        return response;
    }

    /// <summary>
    ///     Lists the earlier versions of a response's answers, newest first.
    /// </summary>
    /// <param name="responseId">The response.</param>
    /// <returns>The revisions, newest first.</returns>
    public async Task<List<FormResponseRevision>> GetResponseRevisionsAsync(int responseId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        return await db.FormResponseRevisions
            .Where(r => r.ResponseId == responseId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    ///     Reads the answers held in a revision.
    /// </summary>
    /// <param name="revision">The revision to read.</param>
    /// <returns>The answers as they stood, or an empty list when the snapshot cannot be read.</returns>
    public static List<FormAnswer> ReadRevisionAnswers(FormResponseRevision revision)
    {
        try
        {
            return JsonSerializer.Deserialize<List<FormAnswer>>(revision.Snapshot) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>
    ///     Lists everything a person has submitted, across every guild, newest first.
    /// </summary>
    /// <remarks>
    ///     Without this a submitter can only reach a response through the status link they were
    ///     handed once at submission time, which is a link people lose.
    /// </remarks>
    /// <param name="userId">The submitter.</param>
    /// <param name="guildId">A guild to narrow the list to, or null for everything.</param>
    /// <returns>Their submissions, newest first.</returns>
    public async Task<List<UserSubmission>> GetUserSubmissionsAsync(ulong userId, ulong? guildId = null)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var rows = await (
            from response in db.FormResponses
            join form in db.Forms on response.FormId equals form.Id
            where response.UserId == userId && (guildId == null || form.GuildId == guildId)
            from workflow in db.FormResponseWorkflows
                .Where(w => w.ResponseId == response.Id)
                .DefaultIfEmpty()
            orderby response.SubmittedAt descending
            select new
            {
                response.Id,
                form.GuildId,
                FormId = form.Id,
                form.Name,
                form.ExpiresAt,
                form.AllowAnonymous,
                response.SubmittedAt,
                response.EditedAt,
                Status = workflow == null ? (int)ResponseStatus.Pending : workflow.Status,
                ReviewedAt = workflow == null ? null : workflow.ReviewedAt,
                ReviewNotes = workflow == null ? null : workflow.ReviewNotes,
                StatusCheckToken = workflow == null ? null : workflow.StatusCheckToken
            }).ToListAsync();

        var now = DateTime.UtcNow;

        return rows.Select(row =>
        {
            var status = (ResponseStatus)row.Status;

            var canEdit = !row.AllowAnonymous
                          && status is ResponseStatus.Pending or ResponseStatus.UnderReview
                          && (row.ExpiresAt == null || row.ExpiresAt > now);

            var guild = client.GetGuild(row.GuildId);

            return new UserSubmission(
                row.Id,
                row.FormId,
                row.Name,
                row.GuildId,
                guild?.Name,
                guild?.IconUrl,
                status,
                row.SubmittedAt,
                row.EditedAt,
                row.ReviewedAt,
                row.ReviewNotes,
                row.StatusCheckToken,
                canEdit);
        }).ToList();
    }

    /// <summary>
    ///     One of a person's submissions, as it appears on the page listing everything they have sent.
    /// </summary>
    /// <param name="ResponseId">The response.</param>
    /// <param name="FormId">The form it answers.</param>
    /// <param name="FormName">The name of that form.</param>
    /// <param name="GuildId">The guild the form belongs to.</param>
    /// <param name="GuildName">The name of that guild, or null when the bot is no longer in it.</param>
    /// <param name="GuildIconUrl">The guild's icon, so a row is recognisable at a glance.</param>
    /// <param name="Status">Where the response stands.</param>
    /// <param name="SubmittedAt">When it was submitted.</param>
    /// <param name="EditedAt">When it was last changed, or null when it never was.</param>
    /// <param name="ReviewedAt">When it was decided, or null while it awaits review.</param>
    /// <param name="ReviewNotes">What the reviewer wrote, when they wrote anything.</param>
    /// <param name="StatusToken">The token addressing this response's own status page.</param>
    /// <param name="CanEdit">Whether the submitter may still change it.</param>
    public record UserSubmission(
        int ResponseId,
        int FormId,
        string FormName,
        ulong GuildId,
        string? GuildName,
        string? GuildIconUrl,
        ResponseStatus Status,
        DateTime SubmittedAt,
        DateTime? EditedAt,
        DateTime? ReviewedAt,
        string? ReviewNotes,
        string? StatusToken,
        bool CanEdit);
}