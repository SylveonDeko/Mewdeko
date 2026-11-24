namespace Mewdeko.Modules.Forms.Common;

/// <summary>
///     The comparisons a form condition can make against an earlier answer.
/// </summary>
public static class FormConditionOperators
{
    /// <summary>The answer matches the expected value, ignoring case.</summary>
    public const string EqualTo = "equals";

    /// <summary>The answer does not match the expected value, ignoring case.</summary>
    public const string NotEqualTo = "not_equals";

    /// <summary>The answer contains the expected value, ignoring case.</summary>
    public const string Contains = "contains";

    /// <summary>The answer is a number above the expected value.</summary>
    public const string GreaterThan = "greater_than";

    /// <summary>The answer is a number below the expected value.</summary>
    public const string LessThan = "less_than";

    /// <summary>The question was answered with anything at all.</summary>
    public const string IsAnswered = "is_answered";

    /// <summary>The question was left blank.</summary>
    public const string IsNotAnswered = "is_not_answered";

    private static readonly HashSet<string> Known =
        new(StringComparer.OrdinalIgnoreCase)
        {
            EqualTo, NotEqualTo, Contains, GreaterThan, LessThan, IsAnswered, IsNotAnswered
        };

    /// <summary>
    ///     Whether an operator is one the form system understands.
    /// </summary>
    /// <param name="value">The operator as stored.</param>
    /// <returns>True for a known operator.</returns>
    public static bool IsKnown(string? value)
    {
        return value != null && Known.Contains(value);
    }

    /// <summary>
    ///     Whether an operator only asks if the question was answered, so it compares against nothing.
    /// </summary>
    /// <param name="value">The operator as stored.</param>
    /// <returns>True for the answered and not answered operators.</returns>
    public static bool IsPresence(string? value)
    {
        return string.Equals(value, IsAnswered, StringComparison.OrdinalIgnoreCase)
               || string.Equals(value, IsNotAnswered, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Compares an answer against an expected value.
    /// </summary>
    /// <param name="actual">The answer as text, empty when the question was not answered.</param>
    /// <param name="value">The operator.</param>
    /// <param name="expected">The expected value, ignored by the answered and not answered operators.</param>
    /// <returns>True when the comparison holds. An unknown operator holds, as it always has.</returns>
    public static bool Compare(string actual, string value, string? expected)
    {
        expected ??= string.Empty;

        return value.ToLowerInvariant() switch
        {
            EqualTo => actual.Equals(expected, StringComparison.OrdinalIgnoreCase),
            NotEqualTo => !actual.Equals(expected, StringComparison.OrdinalIgnoreCase),
            Contains => actual.Contains(expected, StringComparison.OrdinalIgnoreCase),
            GreaterThan => double.TryParse(actual, out var a1) && double.TryParse(expected, out var e1) && a1 > e1,
            LessThan => double.TryParse(actual, out var a2) && double.TryParse(expected, out var e2) && a2 < e2,
            IsAnswered => !string.IsNullOrWhiteSpace(actual),
            IsNotAnswered => string.IsNullOrWhiteSpace(actual),
            _ => true
        };
    }
}
