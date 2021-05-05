using NadekoBot.Extensions;
using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace NadekoBot.Modules.Searches.Common
{
    public class SearchImageCacher
    {
        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);
        private readonly IHttpClientFactory _httpFactory;
        private readonly Random _rng;
        private readonly SortedSet<ImageCacherObject> _cache;
        private readonly Logger _log;
        private static readonly List<string> defaultTagBlacklist = new List<string>() {
            "loli",
            "lolicon",
            "shota"
        };

        public SearchImageCacher(IHttpClientFactory http)
        {
            _httpFactory = http;
            _rng = new Random();
            _cache = new SortedSet<ImageCacherObject>();
            _log = LogManager.GetCurrentClassLogger();
        }

        public async Task<ImageCacherObject> GetImage(string[] tags, bool forceExplicit, DapiSearchType type,
            HashSet<string> blacklistedTags = null)
        {
            tags = tags.Select(tag => tag?.ToLowerInvariant()).ToArray();

            blacklistedTags = blacklistedTags ?? new HashSet<string>();

            foreach (var item in defaultTagBlacklist)
            {
                blacklistedTags.Add(item);
            }

            blacklistedTags = blacklistedTags.Select(t => t.ToLowerInvariant()).ToHashSet();

            if (tags.Any(x => blacklistedTags.Contains(x)))
            {
                throw new Exception("One of the specified tags is blacklisted");
            }

            if (type == DapiSearchType.E621)
                tags = tags.Select(tag => tag?.Replace("yuri", "female/female", StringComparison.InvariantCulture))
                    .ToArray();

            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                ImageCacherObject[] imgs;
                if (tags.Any())
                {
                    imgs = _cache.Where(x => x.Tags.IsSupersetOf(tags) && x.SearchType == type && (!forceExplicit || x.Rating == "e")).ToArray();
                }
                else
                {
                    imgs = _cache.Where(x => x.SearchType == type).ToArray();
                }
                imgs = imgs.Where(x => x.Tags.All(t => !blacklistedTags.Contains(t.ToLowerInvariant()))).ToArray();
                ImageCacherObject img;
                if (imgs.Length == 0)
                    img = null;
                else
                    img = imgs[_rng.Next(imgs.Length)];

                if (img != null)
                {
                    _cache.Remove(img);
                    return img;
                }
                else
                {
                    var images = await DownloadImagesAsync(tags, forceExplicit, type).ConfigureAwait(false);
                    images = images
                        .Where(x => x.Tags.All(t => !blacklistedTags.Contains(t.ToLowerInvariant())))
                        .ToArray();
                    if (images.Length == 0)
                        return null;
                    var toReturn = images[_rng.Next(images.Length)];
                    foreach (var dledImg in images)
                    {
                        if (dledImg != toReturn)
                            _cache.Add(dledImg);
                    }
                    return toReturn;
                }
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<ImageCacherObject[]> DownloadImagesAsync(string[] tags, bool isExplicit, DapiSearchType type)
        {
            isExplicit = type == DapiSearchType.Safebooru
                ? false
                : isExplicit;
            var tag = "";
            tag += string.Join('+', tags.Select(x => x.Replace(" ", "_", StringComparison.InvariantCulture).ToLowerInvariant()));
            if (isExplicit)
                tag = "rating%3Aexplicit+" + tag;
            var website = "";
            switch (type)
            {
                case DapiSearchType.Safebooru:
                    website = $"https://safebooru.org/index.php?page=dapi&s=post&q=index&limit=1000&tags={tag}&json=1";
                    break;
                case DapiSearchType.E621:
                    website = $"https://e621.net/posts.json?limit=200&tags={tag}";
                    break;
                case DapiSearchType.Danbooru:
                    website = $"http://danbooru.donmai.us/posts.json?limit=100&tags={tag}";
                    break;
                case DapiSearchType.Gelbooru:
                    website = $"http://gelbooru.com/index.php?page=dapi&s=post&q=index&limit=100&tags={tag}";
                    break;
                case DapiSearchType.Rule34:
                    website = $"https://rule34.xxx/index.php?page=dapi&s=post&q=index&limit=100&tags={tag}";
                    break;
                case DapiSearchType.Konachan:
                    website = $"https://konachan.com/post.json?s=post&q=index&limit=100&tags={tag}";
                    break;
                case DapiSearchType.Yandere:
                    website = $"https://yande.re/post.json?limit=100&tags={tag}";
                    break;
                case DapiSearchType.Derpibooru:
                    tag = string.IsNullOrWhiteSpace(tag) ? "safe" : tag;
                    website = $"https://www.derpibooru.org/api/v1/json/search/images?q={tag?.Replace('+', ',')}&per_page=49";
                    break;
            }

            try
            {
                using (var _http = _httpFactory.CreateClient())
                {
                    _http.AddFakeHeaders();
                    if (type == DapiSearchType.Konachan || type == DapiSearchType.Yandere || type == DapiSearchType.Danbooru)
                    {
                        var data = await _http.GetStringAsync(website).ConfigureAwait(false);
                        return JsonConvert.DeserializeObject<DapiImageObject[]>(data)
                            .Where(x => x.FileUrl != null)
                            .Select(x => new ImageCacherObject(x, type))
                            .ToArray();
                    }

                    if (type == DapiSearchType.E621)
                    {
                        var data = await _http.GetStringAsync(website).ConfigureAwait(false);
                        return JsonConvert.DeserializeAnonymousType(data, new { posts = new List<E621Object>() })
                            .posts
                            .Where(x => !string.IsNullOrWhiteSpace(x.File?.Url))
                            .Select(x => new ImageCacherObject(x.File.Url,
                                type, string.Join(' ', x.Tags.General), x.Score.Total))
                            .ToArray();
                    }

                    if (type == DapiSearchType.Derpibooru)
                    {
                        var data = await _http.GetStringAsync(website).ConfigureAwait(false);
                        return JsonConvert.DeserializeObject<DerpiContainer>(data)
                            .Images
                            .Where(x => !string.IsNullOrWhiteSpace(x.ViewUrl))
                            .Select(x => new ImageCacherObject(x.ViewUrl,
                                type, string.Join("\n", x.Tags), x.Score))
                            .ToArray();
                    }

                    if (type == DapiSearchType.Safebooru)
                    {
                        var data = await _http.GetStringAsync(website).ConfigureAwait(false);
                        return JsonConvert.DeserializeObject<SafebooruElement[]>(data)
                            .Select(x => new ImageCacherObject(x.FileUrl, type, x.Tags, x.Rating))
                            .ToArray();
                    }

                    return (await LoadXmlAsync(website, type).ConfigureAwait(false)).ToArray();
                }
            }
            catch (Exception ex)
            {
                _log.Warn("Error downloading an image: {Message}", ex.Message);
                _log.Warn(ex);
                return Array.Empty<ImageCacherObject>();
            }
        }

        private async Task<ImageCacherObject[]> LoadXmlAsync(string website, DapiSearchType type)
        {
            var list = new List<ImageCacherObject>();
            using (var http = _httpFactory.CreateClient())
            using (var stream = await http.GetStreamAsync(website).ConfigureAwait(false))
            using (var reader = XmlReader.Create(stream, new XmlReaderSettings()
            {
                Async = true,
            }))
            {
                while (await reader.ReadAsync().ConfigureAwait(false))
                {
                    if (reader.NodeType == XmlNodeType.Element &&
                        reader.Name == "post")
                    {
                        list.Add(new ImageCacherObject(new DapiImageObject()
                        {
                            FileUrl = reader["file_url"],
                            Tags = reader["tags"],
                            Rating = reader["rating"] ?? "e"

                        }, type));
                    }
                }
            }
            return list.ToArray();
        }

        public void Clear()
        {
            _cache.Clear();
        }
    }


    public class DapiImageObject
    {
        [JsonProperty("File_Url")]
        public string FileUrl { get; set; }
        public string Tags { get; set; }
        [JsonProperty("Tag_String")]
        public string TagString { get; set; }
        public string Rating { get; set; }
    }

    public class DerpiContainer
    {
        public DerpiImageObject[] Images { get; set; }
    }

    public class DerpiImageObject
    {
        [JsonProperty("view_url")]
        public string ViewUrl { get; set; }
        public string[] Tags { get; set; }
        public string Score { get; set; }
    }

    public enum DapiSearchType
    {
        Safebooru,
        E621,
        Derpibooru,
        Gelbooru,
        Konachan,
        Rule34,
        Yandere,
        Danbooru,
    }
    public class SafebooruElement
    {
        public string Directory { get; set; }
        public string Image { get; set; }


        public string FileUrl => $"https://safebooru.org/images/{Directory}/{Image}";
        public string Rating { get; set; }
        public string Tags { get; set; }
    }
}
