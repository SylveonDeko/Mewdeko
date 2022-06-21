using System.Threading.Tasks;

namespace Mewdeko.Services;

public interface IGoogleApiService : INService
{
    IEnumerable<string> Languages { get; }
    Task<string> Translate(string sourceText, string sourceLanguage, string targetLanguage);
    Task<IEnumerable<string>> GetVideoLinksByKeywordAsync(string keywords, int count = 1);

    Task<string> ShortenUrl(string url);
    Task<string> ShortenUrl(Uri url);
}