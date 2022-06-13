using Google;
using Google.Apis.Services;
using Google.Apis.Urlshortener.v1;
using Google.Apis.Urlshortener.v1.Data;
using Google.Apis.YouTube.v3;
using Newtonsoft.Json.Linq;
using Serilog;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Mewdeko.Services.Impl;

public class GoogleApiService : IGoogleApiService
{
    private readonly IBotCredentials _creds;
    private readonly IHttpClientFactory _httpFactory;

    private readonly Dictionary<string, string> _languageDictionary = new()
    {
        {"afrikaans", "af"},
        {"albanian", "sq"},
        {"arabic", "ar"},
        {"armenian", "hy"},
        {"azerbaijani", "az"},
        {"basque", "eu"},
        {"belarusian", "be"},
        {"bengali", "bn"},
        {"bulgarian", "bg"},
        {"catalan", "ca"},
        {"chinese-traditional", "zh-TW"},
        {"chinese-simplified", "zh-CN"},
        {"chinese", "zh-CN"},
        {"croatian", "hr"},
        {"czech", "cs"},
        {"danish", "da"},
        {"dutch", "nl"},
        {"english", "en"},
        {"esperanto", "eo"},
        {"estonian", "et"},
        {"filipino", "tl"},
        {"finnish", "fi"},
        {"french", "fr"},
        {"galician", "gl"},
        {"german", "de"},
        {"georgian", "ka"},
        {"greek", "el"},
        {"haitian Creole", "ht"},
        {"hebrew", "iw"},
        {"hindi", "hi"},
        {"hungarian", "hu"},
        {"icelandic", "is"},
        {"indonesian", "id"},
        {"irish", "ga"},
        {"italian", "it"},
        {"japanese", "ja"},
        {"korean", "ko"},
        {"lao", "lo"},
        {"latin", "la"},
        {"latvian", "lv"},
        {"lithuanian", "lt"},
        {"macedonian", "mk"},
        {"malay", "ms"},
        {"maltese", "mt"},
        {"norwegian", "no"},
        {"persian", "fa"},
        {"polish", "pl"},
        {"portuguese", "pt"},
        {"romanian", "ro"},
        {"russian", "ru"},
        {"serbian", "sr"},
        {"slovak", "sk"},
        {"slovenian", "sl"},
        {"spanish", "es"},
        {"swahili", "sw"},
        {"swedish", "sv"},
        {"tamil", "ta"},
        {"telugu", "te"},
        {"thai", "th"},
        {"turkish", "tr"},
        {"ukrainian", "uk"},
        {"urdu", "ur"},
        {"vietnamese", "vi"},
        {"welsh", "cy"},
        {"yiddish", "yi"},

        {"af", "af"},
        {"sq", "sq"},
        {"ar", "ar"},
        {"hy", "hy"},
        {"az", "az"},
        {"eu", "eu"},
        {"be", "be"},
        {"bn", "bn"},
        {"bg", "bg"},
        {"ca", "ca"},
        {"zh-tw", "zh-TW"},
        {"zh-cn", "zh-CN"},
        {"hr", "hr"},
        {"cs", "cs"},
        {"da", "da"},
        {"nl", "nl"},
        {"en", "en"},
        {"eo", "eo"},
        {"et", "et"},
        {"tl", "tl"},
        {"fi", "fi"},
        {"fr", "fr"},
        {"gl", "gl"},
        {"de", "de"},
        {"ka", "ka"},
        {"el", "el"},
        {"ht", "ht"},
        {"iw", "iw"},
        {"hi", "hi"},
        {"hu", "hu"},
        {"is", "is"},
        {"id", "id"},
        {"ga", "ga"},
        {"it", "it"},
        {"ja", "ja"},
        {"ko", "ko"},
        {"lo", "lo"},
        {"la", "la"},
        {"lv", "lv"},
        {"lt", "lt"},
        {"mk", "mk"},
        {"ms", "ms"},
        {"mt", "mt"},
        {"no", "no"},
        {"fa", "fa"},
        {"pl", "pl"},
        {"pt", "pt"},
        {"ro", "ro"},
        {"ru", "ru"},
        {"sr", "sr"},
        {"sk", "sk"},
        {"sl", "sl"},
        {"es", "es"},
        {"sw", "sw"},
        {"sv", "sv"},
        {"ta", "ta"},
        {"te", "te"},
        {"th", "th"},
        {"tr", "tr"},
        {"uk", "uk"},
        {"ur", "ur"},
        {"vi", "vi"},
        {"cy", "cy"},
        {"yi", "yi"}
    };

    private readonly UrlshortenerService _sh;

    private readonly YouTubeService _yt;

    public GoogleApiService(IBotCredentials creds, IHttpClientFactory factory)
    {
        _creds = creds;
        _httpFactory = factory;

        var bcs = new BaseClientService.Initializer
        {
            ApplicationName = "Mewdeko Bot",
            ApiKey = _creds.GoogleApiKey
        };

        _yt = new YouTubeService(bcs);
        _sh = new UrlshortenerService(bcs);
    }

    public async Task<IEnumerable<string>> GetVideoLinksByKeywordAsync(string keywords, int count = 1)
    {
        await Task.Yield();
        if (string.IsNullOrWhiteSpace(keywords))
            throw new ArgumentNullException(nameof(keywords));

        if (count <= 0)
            throw new ArgumentOutOfRangeException(nameof(count));

        var query = _yt.Search.List("snippet");
        query.MaxResults = count;
        query.Q = keywords;
        query.Type = "video";
        query.SafeSearch = SearchResource.ListRequest.SafeSearchEnum.Strict;
        return (await query.ExecuteAsync().ConfigureAwait(false)).Items.Select(i =>
            $"https://www.youtube.com/watch?v={i.Id.VideoId}");
    }

    public Task<string> ShortenUrl(Uri url) => ShortenUrl(url.ToString());

    public async Task<string> ShortenUrl(string url)
    {
        await Task.Yield();
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentNullException(nameof(url));

        if (string.IsNullOrWhiteSpace(_creds.GoogleApiKey))
            return url;

        try
        {
            var response = await _sh.Url.Insert(new Url { LongUrl = url }).ExecuteAsync().ConfigureAwait(false);
            return response.Id;
        }
        catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.Forbidden)
        {
            return url;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error shortening URL");
            return url;
        }
    }

    public IEnumerable<string> Languages => _languageDictionary.Keys.OrderBy(x => x);

    public async Task<string> Translate(string sourceText, string sourceLanguage, string targetLanguage)
    {
        await Task.Yield();
        string text;

        if (!_languageDictionary.ContainsKey(sourceLanguage) ||
            !_languageDictionary.ContainsKey(targetLanguage))
        {
            throw new ArgumentException($"{nameof(sourceLanguage)}/{nameof(targetLanguage)}");
        }

        var url = new Uri(
            $"https://translate.googleapis.com/translate_a/single?client=gtx&sl={ConvertToLanguageCode(sourceLanguage)}&tl={ConvertToLanguageCode(targetLanguage)}&dt=t&q={WebUtility.UrlEncode(sourceText)}");
        using (var http = _httpFactory.CreateClient())
        {
            http.DefaultRequestHeaders.Add("user-agent",
                "Mozilla/5.0 (Windows NT 6.1) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/41.0.2228.0 Safari/537.36");
            text = await http.GetStringAsync(url).ConfigureAwait(false);
        }

        return string.Concat(JArray.Parse(text)[0].Select(x => x[0]));
    }

    private string ConvertToLanguageCode(string language)
    {
        _languageDictionary.TryGetValue(language, out var mode);
        return mode;
    }
}