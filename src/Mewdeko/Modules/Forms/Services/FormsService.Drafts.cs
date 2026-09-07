using System.Text.Json;
using DataModel;
using LinqToDB;
using LinqToDB.Async;

namespace Mewdeko.Modules.Forms.Services;

/// <summary>
///     Partly filled forms, kept so a long submission survives a closed tab.
/// </summary>
/// <remarks>
///     A draft is never validated. It holds whatever someone had typed so far, which by definition
///     is usually incomplete and often wrong. Everything is checked when they actually submit.
/// </remarks>
public partial class FormsService
{
    /// <summary>
    ///     How long a draft is kept before it is treated as abandoned.
    /// </summary>
    private static readonly TimeSpan DraftLifetime = TimeSpan.FromDays(30);

    /// <summary>
    ///     Saves what a submitter has filled in so far, replacing whatever draft they already had.
    /// </summary>
    /// <param name="formId">The form being filled in.</param>
    /// <param name="userId">The submitter.</param>
    /// <param name="answers">The answers so far, keyed by question identifier.</param>
    /// <param name="page">The page they had reached.</param>
    /// <returns>When the draft was saved.</returns>
    public async Task<DateTime> SaveDraftAsync(
        int formId,
        ulong userId,
        Dictionary<int, object> answers,
        int page)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var savedAt = DateTime.UtcNow;
        var payload = JsonSerializer.Serialize(answers);

        var existing = await db.FormResponseDrafts
            .FirstOrDefaultAsync(d => d.FormId == formId && d.UserId == userId);

        if (existing == null)
        {
            await db.InsertAsync(new FormResponseDraft
            {
                FormId = formId,
                UserId = userId,
                Answers = payload,
                Page = page,
                UpdatedAt = savedAt
            });

            return savedAt;
        }

        await db.FormResponseDrafts
            .Where(d => d.Id == existing.Id)
            .Set(d => d.Answers, payload)
            .Set(d => d.Page, page)
            .Set(d => d.UpdatedAt, savedAt)
            .UpdateAsync();

        return savedAt;
    }

    /// <summary>
    ///     Reads a submitter's draft of a form.
    /// </summary>
    /// <param name="formId">The form.</param>
    /// <param name="userId">The submitter.</param>
    /// <returns>The draft, or null when there is none or it has gone stale.</returns>
    public async Task<FormResponseDraft?> GetDraftAsync(int formId, ulong userId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var draft = await db.FormResponseDrafts
            .FirstOrDefaultAsync(d => d.FormId == formId && d.UserId == userId);

        if (draft == null)
            return null;

        // A draft nobody has touched in a month is not worth offering back, and is more likely to
        // confuse than to help if the form has changed underneath it.
        if (DateTime.UtcNow - draft.UpdatedAt > DraftLifetime)
        {
            await DeleteDraftAsync(formId, userId);
            return null;
        }

        return draft;
    }

    /// <summary>
    ///     Discards a submitter's draft of a form, either because they submitted it or because they
    ///     asked to start over.
    /// </summary>
    /// <param name="formId">The form.</param>
    /// <param name="userId">The submitter.</param>
    /// <returns>True when a draft was discarded.</returns>
    public async Task<bool> DeleteDraftAsync(int formId, ulong userId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var deleted = await db.FormResponseDrafts
            .Where(d => d.FormId == formId && d.UserId == userId)
            .DeleteAsync();

        return deleted > 0;
    }

    /// <summary>
    ///     Reads the answers held in a draft back into the shape the rest of the service works with.
    /// </summary>
    /// <param name="draft">The draft to read.</param>
    /// <returns>The answers, keyed by question identifier. Empty when the draft cannot be read.</returns>
    public static Dictionary<int, object> ReadDraftAnswers(FormResponseDraft draft)
    {
        try
        {
            var raw = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(draft.Answers);
            if (raw == null)
                return [];

            var answers = new Dictionary<int, object>();

            foreach (var (key, value) in raw)
            {
                if (!int.TryParse(key, out var questionId))
                    continue;

                switch (value.ValueKind)
                {
                    case JsonValueKind.Array:
                        answers[questionId] = value.EnumerateArray()
                            .Select(e => e.ToString())
                            .ToArray();
                        break;

                    case JsonValueKind.String:
                    case JsonValueKind.Number:
                    case JsonValueKind.True:
                    case JsonValueKind.False:
                        answers[questionId] = value.ToString();
                        break;
                }
            }

            return answers;
        }
        catch (JsonException)
        {
            return [];
        }
    }
}