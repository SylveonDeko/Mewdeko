using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using Discord.Interactions;
using Microsoft.Extensions.Caching.Memory;

namespace Mewdeko.Common.Autocompleters;

/// <summary>
///     Autocompleter for memegen template keys, backed by the memegen template list.
/// </summary>
/// <param name="factory">The http client factory.</param>
/// <param name="cache">The memory cache used to hold the template list.</param>
public class MemegenTemplateAutocompleter(IHttpClientFactory factory, IMemoryCache cache) : AutocompleteHandler
{
    private const string CacheKey = "memegen_templates";

    /// <summary>
    ///     Generates suggestions for autocomplete.
    /// </summary>
    /// <param name="context">The interaction context.</param>
    /// <param name="autocompleteInteraction">The autocomplete interaction.</param>
    /// <param name="parameter">The parameter info.</param>
    /// <param name="services">The service provider.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the autocomplete result.</returns>
    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context,
        IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services)
    {
        var input = (autocompleteInteraction.Data.Current.Value as string ?? "").ToLowerInvariant();
        var templates = await GetTemplatesAsync().ConfigureAwait(false);

        var suggestions = templates
            .Where(x => x.Id.Contains(input, StringComparison.OrdinalIgnoreCase)
                        || x.Name.Contains(input, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.Id.StartsWith(input, StringComparison.OrdinalIgnoreCase))
            .Take(25)
            .Select(x => new AutocompleteResult($"{x.Name} ({x.Id})".TrimTo(100), x.Id));

        return AutocompletionResult.FromSuccess(suggestions);
    }

    /// <summary>
    ///     Gets the memegen template list, fetching and caching it when needed.
    /// </summary>
    /// <returns>The list of memegen templates.</returns>
    public async Task<List<MemegenTemplate>> GetTemplatesAsync()
    {
        if (cache.TryGetValue(CacheKey, out List<MemegenTemplate>? cached) && cached is not null)
            return cached;

        try
        {
            using var http = factory.CreateClient("memelist");
            var rawJson = await http.GetStringAsync("https://api.memegen.link/templates/").ConfigureAwait(false);
            var data = JsonSerializer.Deserialize<List<MemegenTemplate>>(rawJson) ?? [];
            cache.Set(CacheKey, data, TimeSpan.FromHours(6));
            return data;
        }
        catch
        {
            return [];
        }
    }

    /// <summary>
    ///     Represents a memegen template entry.
    /// </summary>
    public class MemegenTemplate
    {
        /// <summary>
        ///     Gets or sets the template display name.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        /// <summary>
        ///     Gets or sets the template key.
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";
    }
}