using System.Text.RegularExpressions;

namespace Mewdeko.Common.TriggerPlaceholders;

/// <summary>
///     Supplies placeholders that need per-invocation context and asynchronous lookups, such as a user's current XP or
///     balance.
/// </summary>
/// <remarks>
///     This complements <see cref="IPlaceholderProvider" />, which is limited to synchronous, context-free values.
///     Implementations are discovered through dependency injection, so a module only has to implement this interface for
///     its placeholders to become available in chat trigger responses.
/// </remarks>
public interface ITriggerPlaceholderProvider
{
    /// <summary>
    ///     Gets the placeholders this provider supplies, keyed by the literal token used in a response.
    /// </summary>
    /// <returns>The placeholder tokens and the functions that resolve them.</returns>
    IEnumerable<(string Name, Func<TriggerPlaceholderContext, Task<string?>> Func)> GetPlaceholders();

    /// <summary>
    ///     Gets the pattern-based placeholders this provider supplies, for placeholders that carry an argument such as
    ///     a counter name.
    /// </summary>
    /// <returns>The patterns and the functions that resolve a match of them.</returns>
    /// <remarks>
    ///     Providers that only supply fixed tokens do not need to implement this.
    /// </remarks>
    IEnumerable<(Regex Pattern, Func<Match, TriggerPlaceholderContext, Task<string?>> Func)> GetRegexPlaceholders()
    {
        return [];
    }
}