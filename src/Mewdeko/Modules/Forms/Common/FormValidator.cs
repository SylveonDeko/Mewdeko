using System.Text.RegularExpressions;
using DataModel;
using Mewdeko.Database.Enums;

namespace Mewdeko.Modules.Forms.Common;

/// <summary>
///     Validates form and question data for logical consistency.
/// </summary>
public static partial class FormValidator
{
    /// <summary>
    ///     Matches an answer piping placeholder, which names the question it quotes by identifier.
    /// </summary>
    [GeneratedRegex(@"\{\{Q(\d+)\}\}")]
    public static partial Regex PipingPlaceholderRegex();

    /// <summary>
    ///     Reads the questions a piece of text pipes answers from.
    /// </summary>
    /// <param name="text">The text to scan, which may be null.</param>
    /// <returns>The identifiers of the questions quoted, in the order they appear.</returns>
    public static IReadOnlyList<int> ReadPipedQuestionIds(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        return PipingPlaceholderRegex()
            .Matches(text)
            .Select(m => int.TryParse(m.Groups[1].Value, out var id) ? id : 0)
            .Where(id => id > 0)
            .ToList();
    }

    /// <summary>
    ///     Validates the answer piping placeholders written into a form's questions. A placeholder
    ///     may only quote a question that comes earlier in the form, because a later answer does not
    ///     exist yet at the moment the quoting question is shown.
    /// </summary>
    /// <param name="questions">Every question on the form.</param>
    /// <returns>List of validation error messages.</returns>
    public static List<string> ValidatePiping(IReadOnlyList<FormQuestion> questions)
    {
        var errors = new List<string>();
        var byId = questions.Where(q => q.Id > 0).ToDictionary(q => q.Id);

        foreach (var question in questions.Where(q => q.EnableAnswerPiping))
        {
            foreach (var targetId in ReadPipedQuestionIds(question.QuestionText)
                         .Concat(ReadPipedQuestionIds(question.Placeholder))
                         .Distinct())
            {
                if (targetId == question.Id)
                {
                    errors.Add($"Question {question.DisplayOrder + 1} pipes its own answer");
                    continue;
                }

                if (!byId.TryGetValue(targetId, out var target))
                {
                    errors.Add(
                        $"Question {question.DisplayOrder + 1} pipes an answer from a question that is not on this form");
                    continue;
                }

                if (target.DisplayOrder >= question.DisplayOrder)
                {
                    errors.Add(
                        $"Question {question.DisplayOrder + 1} pipes an answer from question {target.DisplayOrder + 1}, which is not answered until later");
                }
            }
        }

        return errors;
    }

    /// <summary>
    ///     Validates a form question.
    /// </summary>
    /// <param name="question">The question to validate.</param>
    /// <returns>List of validation error messages.</returns>
    public static List<string> ValidateQuestion(FormQuestion question)
    {
        var errors = new List<string>();

        // Question text required
        if (string.IsNullOrWhiteSpace(question.QuestionText))
        {
            errors.Add("Question text is required");
        }

        if (question.QuestionText?.Length > 500)
        {
            errors.Add("Question text cannot exceed 500 characters");
        }

        // Validate min/max length for text questions
        if (question.QuestionType is "short_text" or "long_text")
        {
            if (question.MinLength.HasValue && question.MaxLength.HasValue &&
                question.MinLength.Value > question.MaxLength.Value)
            {
                errors.Add(
                    $"Minimum length ({question.MinLength}) cannot be greater than maximum length ({question.MaxLength})");
            }

            if (question.MinLength.HasValue && question.MinLength.Value < 0)
            {
                errors.Add("Minimum length cannot be negative");
            }

            if (question.MaxLength.HasValue && question.MaxLength.Value < 1)
            {
                errors.Add("Maximum length must be at least 1");
            }

            if (question.MaxLength.HasValue && question.MaxLength.Value > 5000)
            {
                errors.Add("Maximum length cannot exceed 5000 characters");
            }
        }

        // Validate min/max value for number questions
        if (question.QuestionType == "number")
        {
            if (question.MinValue.HasValue && question.MaxValue.HasValue &&
                question.MinValue.Value > question.MaxValue.Value)
            {
                errors.Add(
                    $"Minimum value ({question.MinValue}) cannot be greater than maximum value ({question.MaxValue})");
            }
        }

        // Validate placeholder length
        if (question.Placeholder?.Length > 200)
        {
            errors.Add("Placeholder text cannot exceed 200 characters");
        }

        // A question image is loaded by every visitor's browser, so only plain web addresses are allowed
        if (!string.IsNullOrWhiteSpace(question.ImageUrl) && !FormAnswerValidator.IsSafeUrl(question.ImageUrl))
        {
            errors.Add("Question image must be an http or https address");
        }

        // Checkboxes reuse min and max value as the number of boxes that may be ticked
        if (question.QuestionType == "checkboxes")
        {
            if (question.MinValue is < 0)
            {
                errors.Add("Minimum selections cannot be negative");
            }

            if (question.MinValue.HasValue && question.MaxValue.HasValue &&
                question.MinValue.Value > question.MaxValue.Value)
            {
                errors.Add("Minimum selections cannot be greater than maximum selections");
            }
        }

        // Validate conditional logic
        if (question.ConditionalParentQuestionId.HasValue)
        {
            if (string.IsNullOrWhiteSpace(question.ConditionalOperator))
            {
                errors.Add("Conditional operator is required when parent question is set");
            }

            if (string.IsNullOrWhiteSpace(question.ConditionalExpectedValue))
            {
                errors.Add("Expected value is required for conditional logic");
            }
        }

        return errors;
    }

    /// <summary>
    ///     Validates a form.
    /// </summary>
    /// <param name="form">The form to validate.</param>
    /// <returns>List of validation error messages.</returns>
    public static List<string> ValidateForm(Form form)
    {
        var errors = new List<string>();

        // Form name required
        if (string.IsNullOrWhiteSpace(form.Name))
        {
            errors.Add("Form name is required");
        }

        if (form.Name?.Length > 255)
        {
            errors.Add("Form name cannot exceed 255 characters");
        }

        // Validate max responses
        if (form.MaxResponses.HasValue && form.MaxResponses.Value < 1)
        {
            errors.Add("Maximum responses must be at least 1");
        }

        // A form that closes before it opens can never be filled in
        if (form.OpensAt.HasValue && form.ExpiresAt.HasValue && form.OpensAt.Value >= form.ExpiresAt.Value)
        {
            errors.Add("The form cannot close before it opens");
        }

        if (form.AnnounceChannelId.HasValue && !form.OpensAt.HasValue)
        {
            errors.Add("A launch announcement needs an opening time to announce");
        }

        if (form.MinAccountAgeDays is < 0)
        {
            errors.Add("Minimum account age cannot be negative");
        }

        if (form.MaxAppealAttempts is < 1)
        {
            errors.Add("Maximum appeal attempts must be at least 1");
        }

        if (form.ReappealCooldownDays is < 0)
        {
            errors.Add("Reappeal cooldown cannot be negative");
        }

        if (form.AppealDelayDays is < 0)
        {
            errors.Add("Appeal delay cannot be negative");
        }

        // Appeal policy only means anything on a form that reviews ban appeals
        if (form.FormType != (int)FormType.BanAppeal)
        {
            var appealSettings = form.BlockReappealAfterRejection
                                 || form.MaxAppealAttempts.HasValue
                                 || form.ReappealCooldownDays.HasValue
                                 || form.AppealDelayDays.HasValue;

            if (appealSettings)
            {
                errors.Add("Appeal limits only apply to ban appeal forms");
            }
        }

        // Anonymous responses carry no user to act on, so no decision can change anyone's roles
        if (form.AllowAnonymous)
        {
            var touchesRoles = !string.IsNullOrWhiteSpace(form.ApprovalAddRoleIds)
                               || !string.IsNullOrWhiteSpace(form.ApprovalRemoveRoleIds)
                               || !string.IsNullOrWhiteSpace(form.RejectionAddRoleIds)
                               || !string.IsNullOrWhiteSpace(form.RejectionRemoveRoleIds)
                               || !string.IsNullOrWhiteSpace(form.SubmitRoleIds)
                               || form.PendingRoleId.HasValue;

            if (touchesRoles)
            {
                errors.Add("An anonymous form cannot change anyone's roles, because it records no submitter");
            }
        }

        return errors;
    }

    /// <summary>
    ///     Validates question options for multiple choice/checkboxes/dropdown.
    /// </summary>
    /// <param name="questionType">The question type.</param>
    /// <param name="options">The options to validate.</param>
    /// <returns>List of validation error messages.</returns>
    public static List<string> ValidateQuestionOptions(string questionType, List<FormQuestionOption> options)
    {
        var errors = new List<string>();

        if (questionType is not ("multiple_choice" or "checkboxes" or "dropdown"))
        {
            return errors;
        }

        if (options == null || options.Count == 0)
        {
            errors.Add($"{GetQuestionTypeLabel(questionType)} questions must have at least one option");
        }

        if (options?.Count > 25)
        {
            errors.Add("Cannot have more than 25 options (Discord embed field limit)");
        }

        if (options != null)
        {
            // Check for empty option texts
            var emptyOptions = options.Where(o => string.IsNullOrWhiteSpace(o.OptionText)).ToList();
            if (emptyOptions.Any())
            {
                errors.Add("All options must have text");
            }

            // Check for duplicate option values
            var optionValues = options.Select(o => o.OptionValue).Where(v => !string.IsNullOrEmpty(v)).ToList();
            var uniqueValues = optionValues.Distinct().Count();
            if (optionValues.Count != uniqueValues)
            {
                errors.Add("Option values must be unique");
            }
        }

        return errors;
    }

    private static string GetQuestionTypeLabel(string type)
    {
        return type switch
        {
            "short_text" => "Short Text",
            "long_text" => "Long Text",
            "multiple_choice" => "Multiple Choice",
            "checkboxes" => "Checkboxes",
            "dropdown" => "Dropdown",
            "number" => "Number",
            "email" => "Email",
            "url" => "URL",
            "section_break" => "Section Break",
            _ => type
        };
    }
}