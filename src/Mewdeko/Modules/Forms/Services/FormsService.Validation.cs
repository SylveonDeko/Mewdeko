using DataModel;
using LinqToDB.Async;
using Mewdeko.Modules.Forms.Common;

namespace Mewdeko.Modules.Forms.Services;

/// <summary>
///     Server side checking of the answers a submitter gives, and the shaping of those answers into
///     the rows that get stored.
/// </summary>
/// <remarks>
///     A form is a public endpoint, so nothing the page enforces can be trusted on arrival. Every
///     answer is re-validated here, and the set of questions that were actually visible is
///     recomputed from the answers rather than taken from the request, so a submitter cannot answer
///     a question the form's own conditions hid from them.
/// </remarks>
public partial class FormsService
{
    /// <summary>
    ///     Loads the options of every question on a form in one query, so validating a submission
    ///     does not issue a query per question.
    /// </summary>
    /// <param name="formId">The form to load options for.</param>
    /// <returns>The options of each question, keyed by question identifier.</returns>
    public async Task<Dictionary<int, List<FormQuestionOption>>> GetFormOptionsAsync(int formId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var options = await db.FormQuestionOptions
            .Join(db.FormQuestions.Where(q => q.FormId == formId),
                o => o.QuestionId,
                q => q.Id,
                (o, _) => o)
            .OrderBy(o => o.DisplayOrder)
            .ToListAsync();

        return options
            .GroupBy(o => o.QuestionId)
            .ToDictionary(g => g.Key, g => g.ToList());
    }

    /// <summary>
    ///     Validates a whole submission and returns the answers that should be stored.
    /// </summary>
    /// <param name="form">The form being submitted.</param>
    /// <param name="answers">The answers as submitted, keyed by question identifier.</param>
    /// <param name="userId">The submitter, for the conditions that ask about Discord.</param>
    /// <returns>
    ///     The accepted answers and any per-question errors. A submission with errors must not be
    ///     stored.
    /// </returns>
    public async Task<AnswerCheck> PrepareAnswersAsync(
        Form form,
        Dictionary<int, object> answers,
        ulong userId)
    {
        var questions = (await GetFormQuestionsAsync(form.Id))
            .OrderBy(q => q.DisplayOrder)
            .ToList();

        var options = await GetFormOptionsAsync(form.Id);

        // The answers arrive as JSON values, which compare and stringify nothing like the plain
        // strings everything below expects.
        answers = FormAnswerValidator.Normalize(answers);

        var accepted = new Dictionary<int, object>();
        var errors = new Dictionary<int, string>();

        foreach (var question in questions)
        {
            // Visibility is judged against the answers accepted so far, which is exactly what the
            // submitter had in front of them when they reached this question.
            if (!await ShouldShowQuestionAsync(question, accepted, userId, form.GuildId))
                continue;

            if (FormAnswerValidator.IsPresentational(question.QuestionType))
                continue;

            answers.TryGetValue(question.Id, out var submitted);

            var isRequired = IsQuestionRequired(question, accepted);
            var questionOptions = options.GetValueOrDefault(question.Id, []);

            var error = FormAnswerValidator.ValidateAnswer(question, submitted, questionOptions, isRequired);

            if (error != null)
            {
                errors[question.Id] = error;
                continue;
            }

            if (submitted != null && !string.IsNullOrWhiteSpace(FormAnswerValidator.ReadText(submitted)))
                accepted[question.Id] = submitted;
        }

        return new AnswerCheck(accepted, errors, questions, options);
    }

    /// <summary>
    ///     Renders an answer the way it should read to a reviewer, resolving the stored option
    ///     values back to the labels the submitter actually saw.
    /// </summary>
    /// <param name="question">The question the answer belongs to.</param>
    /// <param name="options">The question's options, empty for types that take none.</param>
    /// <param name="answer">The submitted answer.</param>
    /// <returns>The answer as readable text.</returns>
    public static string FormatAnswerForDisplay(
        FormQuestion question,
        IReadOnlyList<FormQuestionOption> options,
        object? answer)
    {
        if (string.Equals(question.QuestionType, "checkboxes", StringComparison.OrdinalIgnoreCase))
        {
            var values = FormAnswerValidator.ReadValues(answer);
            return string.Join(", ", values.Select(v => LabelFor(options, v)));
        }

        var text = FormAnswerValidator.ReadText(answer)?.Trim() ?? string.Empty;

        return FormAnswerValidator.RequiresOptions(question.QuestionType)
            ? LabelFor(options, text)
            : text;
    }

    /// <summary>
    ///     Names the option a stored value refers to, falling back to the value itself when the
    ///     option has since been deleted.
    /// </summary>
    private static string LabelFor(IReadOnlyList<FormQuestionOption> options, string value)
    {
        return options.FirstOrDefault(o => string.Equals(o.OptionValue, value, StringComparison.Ordinal))
            ?.OptionText ?? value;
    }

    /// <summary>
    ///     The outcome of checking a submission, either the answers to store or the reasons it was refused.
    /// </summary>
    /// <param name="Accepted">
    ///     The answers that will be stored, keyed by question identifier. Questions the form's
    ///     conditions hid are absent, whatever the submitter sent for them.
    /// </param>
    /// <param name="Errors">
    ///     One message per question that failed, keyed by question identifier. Empty when the
    ///     submission is acceptable.
    /// </param>
    /// <param name="Questions">Every question on the form, in display order.</param>
    /// <param name="Options">The options of each question, keyed by question identifier.</param>
    public record AnswerCheck(
        Dictionary<int, object> Accepted,
        Dictionary<int, string> Errors,
        List<FormQuestion> Questions,
        Dictionary<int, List<FormQuestionOption>> Options);
}