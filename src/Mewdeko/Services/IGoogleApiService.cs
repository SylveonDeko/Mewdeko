using Google.Apis.YouTube.v3.Data;

namespace Mewdeko.Services
{
    /// <summary>
    /// Represents a service for interacting with the Google API.
    /// </summary>
    public interface IGoogleApiService : INService
    {
        /// <summary>
        /// Gets the list of supported languages.
        /// </summary>
        IEnumerable<string?> Languages { get; }

        /// <summary>
        /// Translates the given text from the source language to the target language.
        /// </summary>
        /// <param name="sourceText">The text to translate.</param>
        /// <param name="sourceLanguage">The source language of the text (optional).</param>
        /// <param name="targetLanguage">The target language of the translation.</param>
        /// <returns>The translated text.</returns>
        Task<string> Translate(string sourceText, string? sourceLanguage, string? targetLanguage);

        /// <summary>
        /// Searches for videos on YouTube based on the given keywords.
        /// </summary>
        /// <param name="keywords">The keywords to search for.</param>
        /// <returns>An array of search results.</returns>
        Task<SearchResult[]> GetVideoLinksByKeywordAsync(string keywords);

        /// <summary>
        /// Shortens the given URL.
        /// </summary>
        /// <param name="url">The URL to shorten.</param>
        /// <returns>The shortened URL.</returns>
        Task<string> ShortenUrl(string url);
    }
}