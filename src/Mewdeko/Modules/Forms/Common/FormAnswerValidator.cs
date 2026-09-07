using System.Text.Json;
using System.Text.RegularExpressions;
using DataModel;

namespace Mewdeko.Modules.Forms.Common;

/// <summary>
///     Validates the answers a submitter gives, as opposed to <see cref="FormValidator" />, which
///     validates the form a guild builds.
/// </summary>
/// <remarks>
///     Everything here runs on the server on submission. The dashboard checks the same rules while
///     someone types, but a form is a public endpoint and the page enforcing a rule is not the same
///     as the rule being enforced.
/// </remarks>
public static partial class FormAnswerValidator
{
    /// <summary>
    ///     The longest single answer that will be stored, whatever the question's own limit says.
    /// </summary>
    public const int MaxAnswerLength = 4000;

    /// <summary>
    ///     Question types that present the submitter a fixed list of options.
    /// </summary>
    private static readonly HashSet<string> OptionTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "multiple_choice", "checkboxes", "dropdown"
        };

    /// <summary>
    ///     Determines whether a question type offers a fixed list of options.
    /// </summary>
    /// <param name="questionType">The question type to inspect.</param>
    /// <returns>True when the type needs options configured.</returns>
    public static bool RequiresOptions(string? questionType)
    {
        return questionType != null && OptionTypes.Contains(questionType);
    }

    /// <summary>
    ///     Determines whether a question type is page furniture rather than something to answer.
    /// </summary>
    /// <param name="questionType">The question type to inspect.</param>
    /// <returns>True when the type takes no answer.</returns>
    public static bool IsPresentational(string? questionType)
    {
        return string.Equals(questionType, "section_break", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Validates one answer against the question it was given for.
    /// </summary>
    /// <param name="question">The question the answer belongs to.</param>
    /// <param name="answer">The submitted answer, either a string or a string array.</param>
    /// <param name="options">The question's options, empty for types that take none.</param>
    /// <param name="isRequired">
    ///     Whether an answer is required. This may differ from the question's own flag when the
    ///     question is conditionally required, so it is passed in rather than read here.
    /// </param>
    /// <returns>An error message, or null when the answer is acceptable.</returns>
    public static string? ValidateAnswer(
        FormQuestion question,
        object? answer,
        IReadOnlyList<FormQuestionOption> options,
        bool isRequired)
    {
        if (IsPresentational(question.QuestionType))
            return null;

        if (string.Equals(question.QuestionType, "checkboxes", StringComparison.OrdinalIgnoreCase))
            return ValidateMultiSelect(question, ReadValues(answer), options, isRequired);

        var text = ReadText(answer)?.Trim();

        if (string.IsNullOrEmpty(text))
            return isRequired ? "This question requires an answer" : null;

        if (text.Length > MaxAnswerLength)
            return $"Answer cannot exceed {MaxAnswerLength} characters";

        if (RequiresOptions(question.QuestionType))
        {
            return options.Any(o => string.Equals(o.OptionValue, text, StringComparison.Ordinal))
                ? null
                : "That is not one of the available options";
        }

        return question.QuestionType?.ToLowerInvariant() switch
        {
            "number" => ValidateNumber(question, text),
            "email" => EmailRegex().IsMatch(text) ? null : "Enter a valid email address",
            "url" => IsSafeUrl(text) ? null : "Enter a valid http or https address",
            _ => ValidateTextLength(question, text)
        };
    }

    /// <summary>
    ///     Whether an address may be linked to or loaded. Only absolute http and https addresses
    ///     qualify, which rules out javascript and data URLs.
    /// </summary>
    /// <param name="url">The address to check.</param>
    /// <returns>True when the address is safe.</returns>
    public static bool IsSafeUrl(string? url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var parsed)
               && (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps);
    }

    /// <summary>
    ///     Turns the answers as they arrive over the wire into plain strings and string arrays.
    /// </summary>
    /// <remarks>
    ///     A JSON request body deserializes its values as <see cref="JsonElement" />, which compares
    ///     and stringifies nothing like the values the rest of the service expects. Normalizing once
    ///     on arrival keeps every later reader, the condition evaluator included, working on plain
    ///     values rather than each having to know where the answer came from.
    /// </remarks>
    /// <param name="answers">The answers as submitted.</param>
    /// <returns>The same answers as strings and string arrays, skipping anything unusable.</returns>
    public static Dictionary<int, object> Normalize(Dictionary<int, object>? answers)
    {
        var normalized = new Dictionary<int, object>();

        if (answers == null)
            return normalized;

        foreach (var (questionId, value) in answers)
        {
            if (value is not JsonElement element)
            {
                if (value != null)
                    normalized[questionId] = value;

                continue;
            }

            switch (element.ValueKind)
            {
                case JsonValueKind.Array:
                    normalized[questionId] = element.EnumerateArray()
                        .Select(e => e.ValueKind == JsonValueKind.String ? e.GetString() ?? string.Empty : e.ToString())
                        .Where(v => !string.IsNullOrWhiteSpace(v))
                        .ToArray();
                    break;

                case JsonValueKind.String:
                    normalized[questionId] = element.GetString() ?? string.Empty;
                    break;

                case JsonValueKind.Number:
                case JsonValueKind.True:
                case JsonValueKind.False:
                    normalized[questionId] = element.ToString();
                    break;
            }
        }

        return normalized;
    }

    /// <summary>
    ///     Reads an answer as the single string that conditions and text questions work against.
    /// </summary>
    /// <param name="answer">The submitted answer.</param>
    /// <returns>The answer as text, or null when there is none.</returns>
    public static string? ReadText(object? answer)
    {
        return answer switch
        {
            null => null,
            string single => single,
            string[] many => many.Length > 0 ? string.Join(", ", many) : null,
            IEnumerable<string> many => string.Join(", ", many),
            _ => answer.ToString()
        };
    }

    /// <summary>
    ///     Reads an answer as the list of values a multi-select question stores.
    /// </summary>
    /// <param name="answer">The submitted answer.</param>
    /// <returns>The chosen values, empty when nothing was chosen.</returns>
    public static List<string> ReadValues(object? answer)
    {
        return answer switch
        {
            null => [],
            string[] many => many.Where(v => !string.IsNullOrWhiteSpace(v)).ToList(),
            IEnumerable<string> many => many.Where(v => !string.IsNullOrWhiteSpace(v)).ToList(),
            string single => string.IsNullOrWhiteSpace(single) ? [] : [single],
            _ => []
        };
    }

    private static string? ValidateMultiSelect(
        FormQuestion question,
        List<string> values,
        IReadOnlyList<FormQuestionOption> options,
        bool isRequired)
    {
        if (values.Count == 0)
            return isRequired ? "Choose at least one option" : null;

        if (values.Distinct(StringComparer.Ordinal).Count() != values.Count)
            return "The same option was chosen more than once";

        var unknown = values.FirstOrDefault(v =>
            options.All(o => !string.Equals(o.OptionValue, v, StringComparison.Ordinal)));

        if (unknown != null)
            return "One of the chosen options does not belong to this question";

        // A checkbox question reuses min and max value as the number of boxes that may be ticked.
        if (question.MinValue.HasValue && values.Count < question.MinValue.Value)
            return $"Choose at least {question.MinValue.Value} options";

        if (question.MaxValue.HasValue && values.Count > question.MaxValue.Value)
            return $"Choose at most {question.MaxValue.Value} options";

        return null;
    }

    private static string? ValidateNumber(FormQuestion question, string text)
    {
        if (!double.TryParse(text, out var value))
            return "Enter a number";

        if (question.MinValue.HasValue && value < question.MinValue.Value)
            return $"Enter a number no lower than {question.MinValue.Value}";

        if (question.MaxValue.HasValue && value > question.MaxValue.Value)
            return $"Enter a number no higher than {question.MaxValue.Value}";

        return null;
    }

    private static string? ValidateTextLength(FormQuestion question, string text)
    {
        if (question.MinLength.HasValue && text.Length < question.MinLength.Value)
            return $"Answer must be at least {question.MinLength.Value} characters";

        if (question.MaxLength.HasValue && text.Length > question.MaxLength.Value)
            return $"Answer cannot exceed {question.MaxLength.Value} characters";

        return null;
    }

    [GeneratedRegex(@"^[^@\s]+@[^@\s.]+(\.[^@\s.]+)+$", RegexOptions.IgnoreCase)]
    private static partial Regex EmailRegex();
}