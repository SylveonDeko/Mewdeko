using DataModel;
using LinqToDB;
using LinqToDB.Async;

namespace Mewdeko.Modules.Forms.Services;

/// <summary>
///     Comparing the people who began a form with the people who sent it.
/// </summary>
/// <remarks>
///     A form that records no submitter records no start either, because a start row names a person
///     and the form promised not to.
/// </remarks>
public partial class FormsService
{
    /// <summary>
    ///     Remembers that a person began filling in a form. Only the first time counts.
    /// </summary>
    /// <param name="formId">The form.</param>
    /// <param name="userId">The person.</param>
    public async Task RecordStartAsync(int formId, ulong userId)
    {
        var form = await GetFormAsync(formId, false);

        if (form == null || form.AllowAnonymous)
            return;

        await using var db = await dbFactory.CreateConnectionAsync();

        if (await db.FormStarts.AnyAsync(s => s.FormId == formId && s.UserId == userId))
            return;

        try
        {
            await db.InsertAsync(new FormStart
            {
                FormId = formId, UserId = userId, StartedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Start of form {FormId} by {UserId} was already recorded", formId, userId);
        }
    }

    /// <summary>
    ///     Counts, for every form that saw activity in the window, how many people started it, how many
    ///     of those went on to submit, and how many responses arrived.
    /// </summary>
    /// <param name="days">How many days back the window reaches.</param>
    /// <returns>One row per form, busiest first.</returns>
    public async Task<List<FormFunnelRow>> GetFunnelAsync(int days)
    {
        var cutoff = DateTime.UtcNow.AddDays(-Math.Clamp(days, 1, 365));

        await using var db = await dbFactory.CreateConnectionAsync();

        var started = await db.FormStarts
            .Where(s => s.StartedAt >= cutoff)
            .GroupBy(s => s.FormId)
            .Select(g => new
            {
                FormId = g.Key, Count = g.Count()
            })
            .ToListAsync();

        var completed = await (
                from s in db.FormStarts
                where s.StartedAt >= cutoff &&
                      db.FormResponses.Any(r => r.FormId == s.FormId && r.UserId == s.UserId)
                group s by s.FormId
                into g
                select new
                {
                    FormId = g.Key, Count = g.Count()
                })
            .ToListAsync();

        var submitted = await db.FormResponses
            .Where(r => r.SubmittedAt >= cutoff)
            .GroupBy(r => r.FormId)
            .Select(g => new
            {
                FormId = g.Key, Count = g.Count()
            })
            .ToListAsync();

        var formIds = started.Select(s => s.FormId).Union(submitted.Select(s => s.FormId)).ToList();

        var forms = await db.Forms
            .Where(f => formIds.Contains(f.Id))
            .Select(f => new
            {
                f.Id, f.GuildId, f.Name, f.AllowAnonymous
            })
            .ToListAsync();

        return forms
            .Select(f => new FormFunnelRow(
                f.Id,
                f.Name,
                f.GuildId,
                client.GetGuild(f.GuildId)?.Name,
                f.AllowAnonymous,
                started.FirstOrDefault(s => s.FormId == f.Id)?.Count ?? 0,
                completed.FirstOrDefault(s => s.FormId == f.Id)?.Count ?? 0,
                submitted.FirstOrDefault(s => s.FormId == f.Id)?.Count ?? 0))
            .OrderByDescending(row => row.Started + row.Submitted)
            .ToList();
    }

    /// <summary>
    ///     One form's started and submitted counts.
    /// </summary>
    /// <param name="FormId">The form.</param>
    /// <param name="Name">The form's name.</param>
    /// <param name="GuildId">The guild that owns it.</param>
    /// <param name="GuildName">The guild's name, or null when the bot cannot see it.</param>
    /// <param name="Anonymous">Whether the form records no submitter, so it has no starts.</param>
    /// <param name="Started">How many people began the form in the window.</param>
    /// <param name="StartedAndSubmitted">How many of those have submitted it.</param>
    /// <param name="Submitted">How many responses arrived in the window.</param>
    public record FormFunnelRow(
        int FormId,
        string Name,
        ulong GuildId,
        string? GuildName,
        bool Anonymous,
        int Started,
        int StartedAndSubmitted,
        int Submitted);
}
