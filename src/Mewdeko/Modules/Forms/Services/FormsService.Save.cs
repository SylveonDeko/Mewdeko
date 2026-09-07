using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Controllers.Common.Forms;
using Mewdeko.Modules.Forms.Common;

namespace Mewdeko.Modules.Forms.Services;

/// <summary>
///     Saving a whole form at once: its settings, questions, options and conditions.
/// </summary>
public partial class FormsService
{
    /// <summary>
    ///     Creates or updates a form and everything belonging to it.
    /// </summary>
    /// <remarks>
    ///     Questions present in the request are updated in place where they already exist, so the
    ///     identifiers that conditions, conditional requirements, answer piping and stored answers
    ///     all point at survive the save. Questions absent from the request are deleted. Options
    ///     and conditions are replaced wholesale, since nothing refers to them by identifier.
    /// </remarks>
    /// <param name="guildId">The guild the form belongs to.</param>
    /// <param name="request">The form and its questions.</param>
    /// <returns>The saved form and any validation errors.</returns>
    public async Task<(Form? Form, List<string> Errors)> SaveFormAsync(ulong guildId, FormSaveRequest request)
    {
        var form = request.Form;
        form.GuildId = guildId;

        var errors = FormValidator.ValidateForm(form);

        // Display order comes from the order questions arrive in, so a reordered list cannot
        // disagree with the numbers stored on each question.
        for (var index = 0; index < request.Questions.Count; index++)
        {
            var definition = request.Questions[index];
            definition.Question.DisplayOrder = index;

            errors.AddRange(FormValidator.ValidateQuestion(definition.Question));
            errors.AddRange(
                FormValidator.ValidateQuestionOptions(definition.Question.QuestionType, definition.Options));
        }

        errors.AddRange(FormValidator.ValidatePiping(request.Questions.Select(q => q.Question).ToList()));

        if (errors.Count > 0)
            return (null, errors);

        await using var db = await dbFactory.CreateConnectionAsync();

        var isNew = form.Id <= 0;

        if (isNew)
        {
            form.CreatedAt = DateTime.UtcNow;
            form.UpdatedAt = DateTime.UtcNow;
            form.Id = await db.InsertWithInt32IdentityAsync(form);
        }
        else
        {
            var existing = await db.Forms.FirstOrDefaultAsync(f => f.Id == form.Id && f.GuildId == guildId);

            if (existing == null)
                return (null, ["Form not found"]);

            form.CreatedBy = existing.CreatedBy;
            form.CreatedAt = existing.CreatedAt;
            form.UpdatedAt = DateTime.UtcNow;

            // A restore or a launch that already went out must not be undone by an ordinary save.
            form.AnnouncedAt = existing.AnnouncedAt;

            await db.UpdateAsync(form);
        }

        var existingQuestionIds = await db.FormQuestions
            .Where(q => q.FormId == form.Id)
            .Select(q => q.Id)
            .ToListAsync();

        var keptIds = new List<int>();

        foreach (var definition in request.Questions)
        {
            var question = definition.Question;
            question.FormId = form.Id;

            if (question.Id > 0 && existingQuestionIds.Contains(question.Id))
            {
                await db.UpdateAsync(question);
            }
            else
            {
                question.Id = 0;
                question.CreatedAt = DateTime.UtcNow;
                question.Id = await db.InsertWithInt32IdentityAsync(question);
            }

            keptIds.Add(question.Id);

            await db.FormQuestionOptions.Where(o => o.QuestionId == question.Id).DeleteAsync();
            await db.FormQuestionConditions.Where(c => c.QuestionId == question.Id).DeleteAsync();

            for (var index = 0; index < definition.Options.Count; index++)
            {
                var option = definition.Options[index];

                if (string.IsNullOrWhiteSpace(option.OptionText))
                    continue;

                option.Id = 0;
                option.QuestionId = question.Id;
                option.DisplayOrder = index;
                option.OptionValue = string.IsNullOrWhiteSpace(option.OptionValue)
                    ? option.OptionText
                    : option.OptionValue;

                await db.InsertAsync(option);
            }

            foreach (var condition in definition.Conditions)
            {
                condition.Id = 0;
                condition.QuestionId = question.Id;
                condition.CreatedAt = DateTime.UtcNow;

                await db.InsertAsync(condition);
            }
        }

        var removed = existingQuestionIds.Except(keptIds).ToList();

        if (removed.Count > 0)
        {
            await db.FormQuestionOptions.Where(o => removed.Contains(o.QuestionId)).DeleteAsync();
            await db.FormQuestionConditions.Where(c => removed.Contains(c.QuestionId)).DeleteAsync();
            await db.FormQuestions.Where(q => removed.Contains(q.Id)).DeleteAsync();
        }

        await SaveVersionAsync(form.Id, request.UserId);

        logger.LogInformation("Saved form {FormId} with {QuestionCount} questions", form.Id,
            request.Questions.Count);

        return (form, []);
    }
}