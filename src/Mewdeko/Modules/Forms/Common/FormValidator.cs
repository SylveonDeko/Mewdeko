using DataModel;

namespace Mewdeko.Modules.Forms.Common;

/// <summary>
///     Validates form and question data for logical consistency.
/// </summary>
public static class FormValidator
{
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
            _ => type
        };
    }
}