using System.Threading.Tasks;
using Google.Apis.YouTube.v3.Data;

namespace Mewdeko.Services;

public interface IGoogleApiService : INService
{
    IEnumerable<string?> Languages { get; }
    Task<string> Translate(string sourceText, string? sourceLanguage, string? targetLanguage);
    Task<SearchResult[]> GetVideoLinksByKeywordAsync(string keywords);

    Task<string> ShortenUrl(string url);
    Task<SearchResult[]> GetVideoLinksByVideoId(string id, int max);
}