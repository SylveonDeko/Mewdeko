using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;

namespace Mewdeko.Common.TriggerPlaceholders;

/// <summary>
///     Resolves contextual placeholders supplied by <see cref="ITriggerPlaceholderProvider" /> implementations.
/// </summary>
/// <remarks>
///     Providers are resolved lazily on first use rather than injected, so that a module contributing placeholders can
///     depend on services that themselves consume this one without creating a dependency cycle at startup.
/// </remarks>
public sealed class TriggerPlaceholderService : INService
{
    private readonly ILogger<TriggerPlaceholderService> logger;
    private readonly IServiceProvider services;
    private IReadOnlyList<(Regex Pattern, Func<Match, TriggerPlaceholderContext, Task<string?>> Func)>? patterns;

    private IReadOnlyDictionary<string, Func<TriggerPlaceholderContext, Task<string?>>>? placeholders;

    /// <summary>
    ///     Initializes a new instance of the <see cref="TriggerPlaceholderService" /> class.
    /// </summary>
    /// <param name="services">The service provider used to discover placeholder providers.</param>
    /// <param name="logger">The logger.</param>
    public TriggerPlaceholderService(IServiceProvider services, ILogger<TriggerPlaceholderService> logger)
    {
        this.services = services;
        this.logger = logger;
    }

    /// <summary>
    ///     Gets the tokens every registered provider supplies, for use in help output and validation.
    /// </summary>
    /// <returns>The available placeholder tokens, ordered alphabetically.</returns>
    public IReadOnlyCollection<string> GetAvailablePlaceholders()
    {
        return GetPlaceholders().Keys.OrderBy(x => x, StringComparer.Ordinal).ToList();
    }

    /// <summary>
    ///     Replaces every contextual placeholder present in the input.
    /// </summary>
    /// <param name="input">The text to replace placeholders in.</param>
    /// <param name="context">The context to resolve the placeholders against.</param>
    /// <returns>The input with all recognised placeholders replaced.</returns>
    /// <remarks>
    ///     Only placeholders that actually appear in the input are resolved, so an unused placeholder never costs a
    ///     database round trip. A provider that throws is logged and its placeholder resolves to an empty string, so one
    ///     misbehaving module cannot stop a trigger from responding.
    /// </remarks>
    public async Task<string?> ReplaceAsync(string? input, TriggerPlaceholderContext context)
    {
        if (string.IsNullOrWhiteSpace(input))
            return input;

        foreach (var (token, resolver) in GetPlaceholders())
        {
            if (!input.Contains(token, StringComparison.OrdinalIgnoreCase))
                continue;

            string? value;
            try
            {
                value = await resolver(context).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Contextual placeholder {Token} failed to resolve", token);
                value = "";
            }

            input = input.Replace(token, value ?? "", StringComparison.OrdinalIgnoreCase);
        }

        return await ReplacePatternsAsync(input, context).ConfigureAwait(false);
    }

    /// <summary>
    ///     Replaces the pattern-based placeholders present in the input.
    /// </summary>
    /// <param name="input">The text to replace placeholders in.</param>
    /// <param name="context">The context to resolve the placeholders against.</param>
    /// <returns>The input with all recognised pattern placeholders replaced.</returns>
    private async Task<string?> ReplacePatternsAsync(string? input, TriggerPlaceholderContext context)
    {
        if (string.IsNullOrWhiteSpace(input))
            return input;

        foreach (var (pattern, resolver) in GetPatterns())
        {
            // Matches are resolved one at a time rather than through Regex.Replace, since resolution is asynchronous
            // and may have side effects such as incrementing a counter.
            while (true)
            {
                var match = pattern.Match(input);
                if (!match.Success)
                    break;

                string? value;
                try
                {
                    value = await resolver(match, context).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Contextual placeholder pattern {Pattern} failed to resolve", pattern);
                    value = "";
                }

                input = string.Concat(input.AsSpan(0, match.Index), value ?? "",
                    input.AsSpan(match.Index + match.Length));
            }
        }

        return input;
    }

    private IReadOnlyDictionary<string, Func<TriggerPlaceholderContext, Task<string?>>> GetPlaceholders()
    {
        if (placeholders is not null)
            return placeholders;

        var map = new Dictionary<string, Func<TriggerPlaceholderContext, Task<string?>>>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var provider in services.GetServices<ITriggerPlaceholderProvider>())
        {
            try
            {
                foreach (var (name, func) in provider.GetPlaceholders())
                    map.TryAdd(name, func);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to load placeholders from {Provider}", provider.GetType().Name);
            }
        }

        return placeholders = map;
    }

    private IReadOnlyList<(Regex Pattern, Func<Match, TriggerPlaceholderContext, Task<string?>> Func)> GetPatterns()
    {
        if (patterns is not null)
            return patterns;

        var list = new List<(Regex, Func<Match, TriggerPlaceholderContext, Task<string?>>)>();

        foreach (var provider in services.GetServices<ITriggerPlaceholderProvider>())
        {
            try
            {
                list.AddRange(provider.GetRegexPlaceholders());
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to load placeholder patterns from {Provider}", provider.GetType().Name);
            }
        }

        return patterns = list;
    }
}