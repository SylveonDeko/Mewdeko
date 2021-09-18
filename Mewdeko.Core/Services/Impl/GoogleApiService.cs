using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using Google;
using Google.Apis.Customsearch.v1;
using Google.Apis.Services;
using Google.Apis.Urlshortener.v1;
using Google.Apis.Urlshortener.v1.Data;
using Google.Apis.YouTube.v3;
using Mewdeko.Common;
using Mewdeko.Extensions;
using Newtonsoft.Json.Linq;
using Serilog;

namespace Mewdeko.Core.Services.Impl
{
    public class GoogleApiService : IGoogleApiService
    {
        private const string SearchEngineId = "018084019232060951019:hs5piey28-e";

        private static readonly Regex plRegex =
            new("(?:youtu\\.be\\/|list=)(?<id>[\\da-zA-Z\\-_]*)", RegexOptions.Compiled);

        //private readonly Regex YtVideoIdRegex = new Regex(@"(?:youtube\.com\/\S*(?:(?:\/e(?:mbed))?\/|watch\?(?:\S*?&?v\=))|youtu\.be\/)(?<id>[a-zA-Z0-9_-]{6,11})", RegexOptions.Compiled);
        private readonly IBotCredentials _creds;
        private readonly IHttpClientFactory _httpFactory;

        private readonly Dictionary<string, string> _languageDictionary = new()
        {
            { "afrikaans", "af" },
            { "albanian", "sq" },
            { "arabic", "ar" },
            { "armenian", "hy" },
            { "azerbaijani", "az" },
            { "basque", "eu" },
            { "belarusian", "be" },
            { "bengali", "bn" },
            { "bulgarian", "bg" },
            { "catalan", "ca" },
            { "chinese-traditional", "zh-TW" },
            { "chinese-simplified", "zh-CN" },
            { "chinese", "zh-CN" },
            { "croatian", "hr" },
            { "czech", "cs" },
            { "danish", "da" },
            { "dutch", "nl" },
            { "english", "en" },
            { "esperanto", "eo" },
            { "estonian", "et" },
            { "filipino", "tl" },
            { "finnish", "fi" },
            { "french", "fr" },
            { "galician", "gl" },
            { "german", "de" },
            { "georgian", "ka" },
            { "greek", "el" },
            { "haitian Creole", "ht" },
            { "hebrew", "iw" },
            { "hindi", "hi" },
            { "hungarian", "hu" },
            { "icelandic", "is" },
            { "indonesian", "id" },
            { "irish", "ga" },
            { "italian", "it" },
            { "japanese", "ja" },
            { "korean", "ko" },
            { "lao", "lo" },
            { "latin", "la" },
            { "latvian", "lv" },
            { "lithuanian", "lt" },
            { "macedonian", "mk" },
            { "malay", "ms" },
            { "maltese", "mt" },
            { "norwegian", "no" },
            { "persian", "fa" },
            { "polish", "pl" },
            { "portuguese", "pt" },
            { "romanian", "ro" },
            { "russian", "ru" },
            { "serbian", "sr" },
            { "slovak", "sk" },
            { "slovenian", "sl" },
            { "spanish", "es" },
            { "swahili", "sw" },
            { "swedish", "sv" },
            { "tamil", "ta" },
            { "telugu", "te" },
            { "thai", "th" },
            { "turkish", "tr" },
            { "ukrainian", "uk" },
            { "urdu", "ur" },
            { "vietnamese", "vi" },
            { "welsh", "cy" },
            { "yiddish", "yi" },

            { "af", "af" },
            { "sq", "sq" },
            { "ar", "ar" },
            { "hy", "hy" },
            { "az", "az" },
            { "eu", "eu" },
            { "be", "be" },
            { "bn", "bn" },
            { "bg", "bg" },
            { "ca", "ca" },
            { "zh-tw", "zh-TW" },
            { "zh-cn", "zh-CN" },
            { "hr", "hr" },
            { "cs", "cs" },
            { "da", "da" },
            { "nl", "nl" },
            { "en", "en" },
            { "eo", "eo" },
            { "et", "et" },
            { "tl", "tl" },
            { "fi", "fi" },
            { "fr", "fr" },
            { "gl", "gl" },
            { "de", "de" },
            { "ka", "ka" },
            { "el", "el" },
            { "ht", "ht" },
            { "iw", "iw" },
            { "hi", "hi" },
            { "hu", "hu" },
            { "is", "is" },
            { "id", "id" },
            { "ga", "ga" },
            { "it", "it" },
            { "ja", "ja" },
            { "ko", "ko" },
            { "lo", "lo" },
            { "la", "la" },
            { "lv", "lv" },
            { "lt", "lt" },
            { "mk", "mk" },
            { "ms", "ms" },
            { "mt", "mt" },
            { "no", "no" },
            { "fa", "fa" },
            { "pl", "pl" },
            { "pt", "pt" },
            { "ro", "ro" },
            { "ru", "ru" },
            { "sr", "sr" },
            { "sk", "sk" },
            { "sl", "sl" },
            { "es", "es" },
            { "sw", "sw" },
            { "sv", "sv" },
            { "ta", "ta" },
            { "te", "te" },
            { "th", "th" },
            { "tr", "tr" },
            { "uk", "uk" },
            { "ur", "ur" },
            { "vi", "vi" },
            { "cy", "cy" },
            { "yi", "yi" }
        };

        private readonly CustomsearchService cs;
        private readonly UrlshortenerService sh;

        private readonly YouTubeService yt;

        public GoogleApiService(IBotCredentials creds, IHttpClientFactory factory)
        {
            _creds = creds;
            _httpFactory = factory;

            var bcs = new BaseClientService.Initializer
            {
                ApplicationName = "Mewdeko Bot",
                ApiKey = _creds.GoogleApiKey
            };

            yt = new YouTubeService(bcs);
            sh = new UrlshortenerService(bcs);
            cs = new CustomsearchService(bcs);
        }

        public async Task<IEnumerable<string>> GetPlaylistIdsByKeywordsAsync(string keywords, int count = 1)
        {
            await Task.Yield();
            if (string.IsNullOrWhiteSpace(keywords))
                throw new ArgumentNullException(nameof(keywords));

            if (count <= 0)
                throw new ArgumentOutOfRangeException(nameof(count));

            var match = plRegex.Match(keywords);
            if (match.Length > 1) return new[] { match.Groups["id"].Value };
            var query = yt.Search.List("snippet");
            query.MaxResults = count;
            query.Type = "playlist";
            query.Q = keywords;

            return (await query.ExecuteAsync().ConfigureAwait(false)).Items.Select(i => i.Id.PlaylistId);
        }

        // todo future add quota users
        public async Task<IEnumerable<string>> GetRelatedVideosAsync(string id, int count = 1)
        {
            await Task.Yield();
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentNullException(nameof(id));

            if (count <= 0)
                throw new ArgumentOutOfRangeException(nameof(count));
            var query = yt.Search.List("snippet");
            query.MaxResults = count;
            query.RelatedToVideoId = id;
            query.Type = "video";
            return (await query.ExecuteAsync().ConfigureAwait(false)).Items.Select(i =>
                "http://www.youtube.com/watch?v=" + i.Id.VideoId);
        }

        public async Task<IEnumerable<string>> GetVideoLinksByKeywordAsync(string keywords, int count = 1)
        {
            await Task.Yield();
            if (string.IsNullOrWhiteSpace(keywords))
                throw new ArgumentNullException(nameof(keywords));

            if (count <= 0)
                throw new ArgumentOutOfRangeException(nameof(count));

            var query = yt.Search.List("snippet");
            query.MaxResults = count;
            query.Q = keywords;
            query.Type = "video";
            query.SafeSearch = SearchResource.ListRequest.SafeSearchEnum.Strict;
            return (await query.ExecuteAsync().ConfigureAwait(false)).Items.Select(i =>
                "http://www.youtube.com/watch?v=" + i.Id.VideoId);
        }

        public async Task<IEnumerable<(string Name, string Id, string Url)>> GetVideoInfosByKeywordAsync(
            string keywords, int count = 1)
        {
            await Task.Yield();
            if (string.IsNullOrWhiteSpace(keywords))
                throw new ArgumentNullException(nameof(keywords));

            if (count <= 0)
                throw new ArgumentOutOfRangeException(nameof(count));

            var query = yt.Search.List("snippet");
            query.MaxResults = count;
            query.Q = keywords;
            query.Type = "video";
            return (await query.ExecuteAsync().ConfigureAwait(false)).Items.Select(i =>
                (i.Snippet.Title.TrimTo(50), i.Id.VideoId, "http://www.youtube.com/watch?v=" + i.Id.VideoId));
        }

        public Task<string> ShortenUrl(Uri url)
        {
            return ShortenUrl(url.ToString());
        }

        public async Task<string> ShortenUrl(string url)
        {
            await Task.Yield();
            if (string.IsNullOrWhiteSpace(url))
                throw new ArgumentNullException(nameof(url));

            if (string.IsNullOrWhiteSpace(_creds.GoogleApiKey))
                return url;

            try
            {
                var response = await sh.Url.Insert(new Url { LongUrl = url }).ExecuteAsync().ConfigureAwait(false);
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

        public async Task<IEnumerable<string>> GetPlaylistTracksAsync(string playlistId, int count = 50)
        {
            await Task.Yield();
            if (string.IsNullOrWhiteSpace(playlistId))
                throw new ArgumentNullException(nameof(playlistId));

            if (count <= 0)
                throw new ArgumentOutOfRangeException(nameof(count));

            string nextPageToken = null;

            var toReturn = new List<string>(count);

            do
            {
                var toGet = count > 50 ? 50 : count;
                count -= toGet;

                var query = yt.PlaylistItems.List("contentDetails");
                query.MaxResults = toGet;
                query.PlaylistId = playlistId;
                query.PageToken = nextPageToken;

                var data = await query.ExecuteAsync().ConfigureAwait(false);

                toReturn.AddRange(data.Items.Select(i => i.ContentDetails.VideoId));
                nextPageToken = data.NextPageToken;
            } while (count > 0 && !string.IsNullOrWhiteSpace(nextPageToken));

            return toReturn;
        }

        public async Task<IReadOnlyDictionary<string, TimeSpan>> GetVideoDurationsAsync(IEnumerable<string> videoIds)
        {
            await Task.Yield();
            var videoIdsList = videoIds as List<string> ?? videoIds.ToList();

            var toReturn = new Dictionary<string, TimeSpan>();

            if (!videoIdsList.Any())
                return toReturn;
            var remaining = videoIdsList.Count;

            do
            {
                var toGet = remaining > 50 ? 50 : remaining;
                remaining -= toGet;

                var q = yt.Videos.List("contentDetails");
                q.Id = string.Join(",", videoIdsList.Take(toGet));
                videoIdsList = videoIdsList.Skip(toGet).ToList();
                var items = (await q.ExecuteAsync().ConfigureAwait(false)).Items;
                foreach (var i in items) toReturn.Add(i.Id, XmlConvert.ToTimeSpan(i.ContentDetails.Duration));
            } while (remaining > 0);

            return toReturn;
        }

        public async Task<ImageResult> GetImageAsync(string query)
        {
            await Task.Yield();
            if (string.IsNullOrWhiteSpace(query))
                throw new ArgumentNullException(nameof(query));

            var req = cs.Cse.List();
            req.Q = query;
            req.Cx = SearchEngineId;
            req.Num = 1;
            req.Fields = "items(image(contextLink,thumbnailLink),link)";
            req.SearchType = CseResource.ListRequest.SearchTypeEnum.Image;
            req.Start = new MewdekoRandom().Next(0, 20);

            var search = await req.ExecuteAsync().ConfigureAwait(false);

            return new ImageResult(search.Items[0].Image, search.Items[0].Link);
        }

        public IEnumerable<string> Languages => _languageDictionary.Keys.OrderBy(x => x);

        public async Task<string> Translate(string sourceText, string sourceLanguage, string targetLanguage)
        {
            await Task.Yield();
            string text;

            if (!_languageDictionary.ContainsKey(sourceLanguage) ||
                !_languageDictionary.ContainsKey(targetLanguage))
                throw new ArgumentException(nameof(sourceLanguage) + "/" + nameof(targetLanguage));


            var url = new Uri(string.Format(
                "https://translate.googleapis.com/translate_a/single?client=gtx&sl={0}&tl={1}&dt=t&q={2}",
                ConvertToLanguageCode(sourceLanguage),
                ConvertToLanguageCode(targetLanguage),
                WebUtility.UrlEncode(sourceText)));
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
}