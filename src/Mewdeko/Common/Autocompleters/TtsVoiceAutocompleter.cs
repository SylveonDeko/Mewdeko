using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using Discord.Interactions;
using Microsoft.Extensions.Caching.Memory;

namespace Mewdeko.Common.Autocompleters;

/// <summary>
///     Autocompleter for the TTS voices available from the Flowery TTS API.
/// </summary>
/// <param name="factory">The http client factory.</param>
/// <param name="cache">The memory cache used to hold the voice list.</param>
public class TtsVoiceAutocompleter(IHttpClientFactory factory, IMemoryCache cache) : AutocompleteHandler
{
    private const string CacheKey = "tts_flowery_voices";

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
        var input = autocompleteInteraction.Data.Current.Value as string ?? "";
        var voices = await GetVoicesAsync(factory, cache).ConfigureAwait(false);

        var suggestions = voices
            .Where(v => v.Name.Contains(input, StringComparison.OrdinalIgnoreCase)
                        || (v.Gender?.Contains(input, StringComparison.OrdinalIgnoreCase) ?? false)
                        || (v.Language?.Name?.Contains(input, StringComparison.OrdinalIgnoreCase) ?? false)
                        || (v.Language?.Code?.Contains(input, StringComparison.OrdinalIgnoreCase) ?? false))
            .OrderByDescending(v => v.Name.StartsWith(input, StringComparison.OrdinalIgnoreCase))
            .ThenBy(v => v.Name)
            .Take(25)
            .Select(v =>
            {
                var display = $"{v.Name} ({v.Gender}, {v.Language?.Name}, {v.Source})";
                return new AutocompleteResult(display.Length > 100 ? display[..97] + "..." : display, v.Name);
            });

        return AutocompletionResult.FromSuccess(suggestions);
    }

    /// <summary>
    ///     Gets the Flowery TTS voice list, fetching and caching it when needed. SAM voices are excluded.
    /// </summary>
    /// <param name="factory">The http client factory.</param>
    /// <param name="cache">The memory cache used to hold the voice list.</param>
    /// <returns>The list of available voices, or an empty list when the API cannot be reached.</returns>
    public static async Task<List<TtsVoice>> GetVoicesAsync(IHttpClientFactory factory, IMemoryCache cache)
    {
        if (cache.TryGetValue(CacheKey, out List<TtsVoice>? cached) && cached is not null)
            return cached;

        try
        {
            using var http = factory.CreateClient();
            http.DefaultRequestHeaders.UserAgent.ParseAdd("Mewdeko/1.0");
            var json = await http.GetStringAsync("https://api.flowery.pw/v1/tts/voices").ConfigureAwait(false);
            var response = JsonSerializer.Deserialize<TtsVoiceResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            var voices = (response?.Voices ?? []).Where(v => v.Source is not "SAM").ToList();
            cache.Set(CacheKey, voices, TimeSpan.FromHours(6));
            return voices;
        }
        catch
        {
            return [];
        }
    }

    /// <summary>
    ///     Represents the Flowery TTS voices response.
    /// </summary>
    public class TtsVoiceResponse
    {
        /// <summary>
        ///     Gets or sets the available voices.
        /// </summary>
        [JsonPropertyName("voices")]
        public List<TtsVoice> Voices { get; set; } = [];
    }

    /// <summary>
    ///     Represents a single Flowery TTS voice.
    /// </summary>
    public class TtsVoice
    {
        /// <summary>
        ///     Gets or sets the voice name.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        /// <summary>
        ///     Gets or sets the voice gender.
        /// </summary>
        [JsonPropertyName("gender")]
        public string? Gender { get; set; }

        /// <summary>
        ///     Gets or sets the voice source provider.
        /// </summary>
        [JsonPropertyName("source")]
        public string? Source { get; set; }

        /// <summary>
        ///     Gets or sets the voice language.
        /// </summary>
        [JsonPropertyName("language")]
        public TtsVoiceLanguage? Language { get; set; }
    }

    /// <summary>
    ///     Represents the language of a Flowery TTS voice.
    /// </summary>
    public class TtsVoiceLanguage
    {
        /// <summary>
        ///     Gets or sets the language display name.
        /// </summary>
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        /// <summary>
        ///     Gets or sets the language code.
        /// </summary>
        [JsonPropertyName("code")]
        public string? Code { get; set; }
    }
}