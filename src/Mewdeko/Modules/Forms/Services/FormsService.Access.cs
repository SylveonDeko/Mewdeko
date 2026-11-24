using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Modules.Forms.Common;

namespace Mewdeko.Modules.Forms.Services;

/// <summary>
///     Finding which guild a form record belongs to, for the checks that run before an action.
/// </summary>
public partial class FormsService
{
    /// <summary>
    ///     Resolves a record id to the form and guild it belongs to.
    /// </summary>
    /// <param name="kind">What the id names.</param>
    /// <param name="id">The id.</param>
    /// <returns>The scope, or null when no such record exists.</returns>
    public async Task<FormAccessScope?> ResolveAccessScopeAsync(FormIdKind kind, int id)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        switch (kind)
        {
            case FormIdKind.Form:
                return await db.Forms
                    .Where(f => f.Id == id)
                    .Select(f => new FormAccessScope
                    {
                        FormId = f.Id, GuildId = f.GuildId, IsDraft = f.IsDraft
                    })
                    .FirstOrDefaultAsync();

            case FormIdKind.Question:
                return await (from q in db.FormQuestions
                    where q.Id == id
                    join f in db.Forms on q.FormId equals f.Id
                    select new FormAccessScope
                    {
                        FormId = f.Id, GuildId = f.GuildId, IsDraft = f.IsDraft, QuestionId = q.Id
                    }).FirstOrDefaultAsync();

            case FormIdKind.Option:
                return await (from o in db.FormQuestionOptions
                    where o.Id == id
                    join q in db.FormQuestions on o.QuestionId equals q.Id
                    join f in db.Forms on q.FormId equals f.Id
                    select new FormAccessScope
                    {
                        FormId = f.Id, GuildId = f.GuildId, IsDraft = f.IsDraft, QuestionId = q.Id
                    }).FirstOrDefaultAsync();

            case FormIdKind.Condition:
                return await (from c in db.FormQuestionConditions
                    where c.Id == id
                    join q in db.FormQuestions on c.QuestionId equals q.Id
                    join f in db.Forms on q.FormId equals f.Id
                    select new FormAccessScope
                    {
                        FormId = f.Id, GuildId = f.GuildId, IsDraft = f.IsDraft, QuestionId = q.Id
                    }).FirstOrDefaultAsync();

            case FormIdKind.Response:
                return await (from r in db.FormResponses
                    where r.Id == id
                    join f in db.Forms on r.FormId equals f.Id
                    select new FormAccessScope
                    {
                        FormId = f.Id, GuildId = f.GuildId, IsDraft = f.IsDraft, ResponseUserId = r.UserId
                    }).FirstOrDefaultAsync();

            default:
                return null;
        }
    }
}
