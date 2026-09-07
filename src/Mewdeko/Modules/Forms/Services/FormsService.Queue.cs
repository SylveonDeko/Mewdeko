using DataModel;
using LinqToDB.Async;
using Mewdeko.Database.Enums;

namespace Mewdeko.Modules.Forms.Services;

/// <summary>
///     The queue a reviewer works through: responses with their review state, filtered and paged
///     together rather than fetched separately and stitched up afterwards.
/// </summary>
public partial class FormsService
{
    /// <summary>
    ///     Reads the card counts for every form in a guild.
    /// </summary>
    /// <remarks>
    ///     Three grouped queries rather than three per form. The previous shape counted each form
    ///     separately and materialised whole response rows just to take their length, which turned
    ///     one page load into dozens of round trips for a guild with a few forms.
    /// </remarks>
    /// <param name="guildId">The guild.</param>
    /// <returns>The counts for each form, keyed by form identifier.</returns>
    public async Task<Dictionary<int, FormCounts>> GetFormCountsAsync(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var formIds = await db.Forms
            .Where(f => f.GuildId == guildId)
            .Select(f => f.Id)
            .ToListAsync();

        if (formIds.Count == 0)
            return [];

        var questionCounts = (await db.FormQuestions
                .Where(q => formIds.Contains(q.FormId))
                .GroupBy(q => q.FormId)
                .Select(g => new
                {
                    FormId = g.Key, Count = g.Count()
                })
                .ToListAsync())
            .ToDictionary(x => x.FormId, x => x.Count);

        var responseCounts = (await db.FormResponses
                .Where(r => formIds.Contains(r.FormId))
                .GroupBy(r => r.FormId)
                .Select(g => new
                {
                    FormId = g.Key, Count = g.Count()
                })
                .ToListAsync())
            .ToDictionary(x => x.FormId, x => x.Count);

        // A response with no workflow row has not been decided, so it counts as pending.
        var pendingCounts = (await (
                from response in db.FormResponses
                where formIds.Contains(response.FormId)
                from workflow in db.FormResponseWorkflows
                    .Where(w => w.ResponseId == response.Id)
                    .DefaultIfEmpty()
                where workflow == null
                      || workflow.Status == (int)ResponseStatus.Pending
                      || workflow.Status == (int)ResponseStatus.UnderReview
                group response by response.FormId
                into grouped
                select new
                {
                    FormId = grouped.Key, Count = grouped.Count()
                }).ToListAsync())
            .ToDictionary(x => x.FormId, x => x.Count);

        return formIds.ToDictionary(id => id, id => new FormCounts(
            questionCounts.GetValueOrDefault(id, 0),
            responseCounts.GetValueOrDefault(id, 0),
            pendingCounts.GetValueOrDefault(id, 0)));
    }

    /// <summary>
    ///     Reads a page of a form's responses along with their review state and answers.
    /// </summary>
    /// <remarks>
    ///     Everything is loaded in a handful of queries rather than one per response. The previous
    ///     shape issued a query per row to fetch its workflow, which is fine for a form with twenty
    ///     responses and not fine for one with two thousand.
    /// </remarks>
    /// <param name="formId">The form.</param>
    /// <param name="status">Only responses in this review state, or null for all of them.</param>
    /// <param name="page">The page to read, counting from one.</param>
    /// <param name="pageSize">How many responses to read.</param>
    /// <returns>The page, with the counts for each review state.</returns>
    public async Task<ResponseQueue> GetResponseQueueAsync(
        int formId,
        ResponseStatus? status = null,
        int page = 1,
        int pageSize = 25)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        await using var db = await dbFactory.CreateConnectionAsync();

        // Counts are taken across the whole form, not the filtered set, because they are what the
        // filter chips are labelled with.
        var statusCounts = await (
            from response in db.FormResponses
            where response.FormId == formId
            from workflow in db.FormResponseWorkflows
                .Where(w => w.ResponseId == response.Id)
                .DefaultIfEmpty()
            group response by workflow == null ? (int)ResponseStatus.Pending : workflow.Status
            into grouped
            select new
            {
                Status = grouped.Key, Count = grouped.Count()
            }).ToListAsync();

        var counts = statusCounts.ToDictionary(
            c => ((ResponseStatus)c.Status).ToString(),
            c => c.Count);

        var filtered = from response in db.FormResponses
            where response.FormId == formId
            from workflow in db.FormResponseWorkflows
                .Where(w => w.ResponseId == response.Id)
                .DefaultIfEmpty()
            where status == null
                  || (workflow == null
                      ? status == ResponseStatus.Pending
                      : workflow.Status == (int)status)
            select new
            {
                Response = response, Workflow = workflow
            };

        var totalCount = await filtered.CountAsync();

        var rows = await filtered
            .OrderByDescending(r => r.Response.SubmittedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var responseIds = rows.Select(r => r.Response.Id).ToList();

        var answers = await db.FormAnswers
            .Where(a => responseIds.Contains(a.ResponseId))
            .ToListAsync();

        var revisionCounts = (await db.FormResponseRevisions
                .Where(r => responseIds.Contains(r.ResponseId))
                .ToListAsync())
            .GroupBy(r => r.ResponseId)
            .ToDictionary(g => g.Key, g => g.Count());

        // Answers are ordered by the question they belong to, so a response reads down the page in
        // the order it was filled in rather than in insertion order.
        var questionOrder = (await db.FormQuestions
                .Where(q => q.FormId == formId)
                .ToListAsync())
            .ToDictionary(q => q.Id, q => q.DisplayOrder);

        var queued = rows.Select(row => new QueuedResponse(
            row.Response,
            row.Workflow,
            answers
                .Where(a => a.ResponseId == row.Response.Id)
                .OrderBy(a => questionOrder.GetValueOrDefault(a.QuestionId, int.MaxValue))
                .ToList(),
            revisionCounts.GetValueOrDefault(row.Response.Id, 0))).ToList();

        return new ResponseQueue(
            queued,
            totalCount,
            page,
            pageSize,
            (int)Math.Ceiling(totalCount / (double)pageSize),
            counts);
    }

    /// <summary>
    ///     One response as it appears in the review queue.
    /// </summary>
    /// <param name="Response">The response itself.</param>
    /// <param name="Workflow">Its review state, or null when the form does not review responses.</param>
    /// <param name="Answers">The answers given, in question order.</param>
    /// <param name="RevisionCount">How many times the submitter has edited it.</param>
    public record QueuedResponse(
        FormResponse Response,
        FormResponseWorkflow? Workflow,
        List<FormAnswer> Answers,
        int RevisionCount);

    /// <summary>
    ///     A page of the review queue, with the counts the filter chips need.
    /// </summary>
    /// <param name="Responses">The responses on this page.</param>
    /// <param name="TotalCount">How many responses match the current filter.</param>
    /// <param name="Page">The page returned, counting from one.</param>
    /// <param name="PageSize">How many responses a page holds.</param>
    /// <param name="TotalPages">How many pages the filter yields.</param>
    /// <param name="StatusCounts">How many responses sit in each review state, ignoring the filter.</param>
    public record ResponseQueue(
        List<QueuedResponse> Responses,
        int TotalCount,
        int Page,
        int PageSize,
        int TotalPages,
        Dictionary<string, int> StatusCounts);

    /// <summary>
    ///     The counts shown on a form's card in the guild's list of forms.
    /// </summary>
    /// <param name="QuestionCount">How many questions the form asks.</param>
    /// <param name="ResponseCount">How many responses it has received.</param>
    /// <param name="PendingCount">How many of those still await a decision.</param>
    public record FormCounts(int QuestionCount, int ResponseCount, int PendingCount);
}