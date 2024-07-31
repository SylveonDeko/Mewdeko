using System.Net.Http;
using System.Threading;
using System.Xml;
using Newtonsoft.Json;
using Serilog;

namespace Mewdeko.Modules.Searches.Common
{
    /// <summary>
    /// Represents a service for caching and retrieving images.
    /// </summary>
    public class SearchImageCacher
    {
        private static readonly List<string> DefaultTagBlacklist = ["loli", "lolicon", "shota"];

        private readonly SortedSet<ImageCacherObject> cache;
        private readonly IHttpClientFactory httpFactory;
        private readonly SemaphoreSlim @lock = new SemaphoreSlim(1, 1);
        private readonly Random rng;

        /// <summary>
        /// Initializes a new instance of the <see cref="SearchImageCacher"/> class.
        /// </summary>
        /// <param name="http">The <see cref="IHttpClientFactory"/> for creating HTTP clients.</param>
        public SearchImageCacher(IHttpClientFactory http)
        {
            httpFactory = http ?? throw new ArgumentNullException(nameof(http));
            rng = new Random();
            cache = [];
        }

        /// <summary>
        /// Retrieves an image based on the specified parameters.
        /// </summary>
        /// <param name="tags">The tags used to search for the image.</param>
        /// <param name="forceExplicit">A value indicating whether to force explicit content.</param>
        /// <param name="type">The type of image search.</param>
        /// <param name="blacklistedTags">The tags that are blacklisted.</param>
        /// <returns>An <see cref="ImageCacherObject"/> representing the retrieved image, or <c>null</c> if no image is found.</returns>
        public async Task<ImageCacherObject>? GetImage(string[] tags, bool forceExplicit, DapiSearchType type,
            HashSet<string>? blacklistedTags = null)
        {
            // Normalize tags to lowercase
            tags = tags.Select(tag => tag.ToLowerInvariant()).ToArray();

            blacklistedTags ??= [];

            // Add default tag blacklist to the provided blacklisted tags
            foreach (var item in DefaultTagBlacklist)
            {
                blacklistedTags.Add(item);
            }

            blacklistedTags = blacklistedTags.Select(t => t.ToLowerInvariant()).ToHashSet();

            // Check if any of the specified tags is blacklisted
            if (tags.Any(x => blacklistedTags.Contains(x)))
                throw new ArgumentException("One of the specified tags is blacklisted");

            // Handle specific tag replacement for E621 type
            if (type == DapiSearchType.E621)
            {
                tags = tags.Select(tag => tag.Replace("yuri", "female/female", StringComparison.InvariantCulture))
                    .ToArray();
            }

            await @lock.WaitAsync().ConfigureAwait(false);
            try
            {
                ImageCacherObject[] imgs;
                if (tags.Length > 0)
                {
                    imgs = cache.Where(x =>
                            x.Tags.IsSupersetOf(tags) && x.SearchType == type && (!forceExplicit || x.Rating == "e"))
                        .ToArray();
                }
                else
                {
                    imgs = cache.Where(x => x.SearchType == type).ToArray();
                }

                imgs = imgs.Where(x => x.Tags.All(t => !blacklistedTags.Contains(t.ToLowerInvariant()))).ToArray();
                ImageCacherObject img;
                if (imgs.Length == 0)
                    img = null;
                else
                    img = imgs[rng.Next(imgs.Length)];

                if (img != null)
                {
                    cache.Remove(img);
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
                    var toReturn = images[rng.Next(images.Length)];
                    foreach (var dledImg in images)
                    {
                        if (dledImg != toReturn)
                            cache.Add(dledImg);
                    }

                    return toReturn;
                }
            }
            finally
            {
                @lock.Release();
            }
        }

        /// <summary>
        /// Downloads images based on the specified parameters.
        /// </summary>
        /// <param name="tags">The tags used to search for the image.</param>
        /// <param name="isExplicit">A value indicating whether to allow explicit content.</param>
        /// <param name="type">The type of image search.</param>
        /// <returns>An array of <see cref="ImageCacherObject"/> representing the downloaded images.</returns>
        public async Task<ImageCacherObject[]> DownloadImagesAsync(string[] tags, bool isExplicit, DapiSearchType type)
        {
            // Determine if explicit content should be allowed based on search type
            isExplicit = type != DapiSearchType.Safebooru && isExplicit;
            var tag = "";
            tag += string.Join('+',
                tags.Select(x => x.Replace(" ", "_", StringComparison.InvariantCulture).ToLowerInvariant()));
            if (isExplicit)
                tag = $"rating%3Aexplicit+{tag}";
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
                    website = $"https://gelbooru.com/index.php?page=dapi&s=post&json=1&q=index&limit=100&tags={tag}";
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
                    website =
                        $"https://www.derpibooru.org/api/v1/json/search/images?q={tag.Replace('+', ',')}&per_page=49";
                    break;
            }

            try
            {
                using var http = httpFactory.CreateClient();
                http.AddFakeHeaders();
                switch (type)
                {
                    case DapiSearchType.Konachan or DapiSearchType.Yandere or DapiSearchType.Danbooru:
                    {
                        var data = await http.GetStringAsync(website).ConfigureAwait(false);


                        return (JsonConvert.DeserializeObject<DapiImageObject[]>(data) ??
                                [])
                            .Where(x => x.FileUrl != null)
                            .Select(x => new ImageCacherObject(x, type))
                            .ToArray();
                    }
                    case DapiSearchType.E621:
                    {
                        var data = await http.GetStringAsync(website).ConfigureAwait(false);
                        return JsonConvert.DeserializeAnonymousType(data, new
                            {
                                posts = new List<E621Object>()
                            })
                            .posts
                            .Where(x => !string.IsNullOrWhiteSpace(x.File.Url))
                            .Select(x =>
                                new ImageCacherObject(x.File.Url, type, string.Join(' ', x.Tags.General),
                                    x.Score.Total))
                            .ToArray();
                    }
                    case DapiSearchType.Derpibooru:
                    {
                        var data = await http.GetStringAsync(website).ConfigureAwait(false);
                        return JsonConvert.DeserializeObject<DerpiContainer>(data)
                            .Images
                            .Where(x => !string.IsNullOrWhiteSpace(x.ViewUrl))
                            .Select(x => new ImageCacherObject(x.ViewUrl, type, string.Join("\n", x.Tags), x.Score))
                            .ToArray();
                    }
                    case DapiSearchType.Safebooru:
                    {
                        var data = await http.GetStringAsync(website).ConfigureAwait(false);
                        return JsonConvert.DeserializeObject<SafebooruElement[]>(data)
                            .Select(x => new ImageCacherObject(x.FileUrl, type, x.Tags, x.Rating))
                            .ToArray();
                    }
                    default:
                        return (await LoadXmlAsync(website, type).ConfigureAwait(false)).ToArray();
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error downloading an image: {Message}", ex.Message);
                return [];
            }
        }

        private async Task<ImageCacherObject[]> LoadXmlAsync(string website, DapiSearchType type)
        {
            var list = new List<ImageCacherObject>();
            using (var http = httpFactory.CreateClient())
            {
                var stream = await http.GetStreamAsync(website).ConfigureAwait(false);
                await using (stream.ConfigureAwait(false))
                using (var reader = XmlReader.Create(stream, new XmlReaderSettings
                       {
                           Async = true
                       }))
                {
                    while (await reader.ReadAsync().ConfigureAwait(false))
                    {
                        if (reader.NodeType == XmlNodeType.Element && reader.Name == "post")
                            list.Add(new ImageCacherObject(new DapiImageObject
                            {
                                FileUrl = reader["file_url"], Tags = reader["tags"], Rating = reader["rating"] ?? "e"
                            }, type));
                    }
                }
            }

            return list.ToArray();
        }

        /// <summary>
        /// Clears the image cache.
        /// </summary>
        public void Clear() => cache.Clear();
    }

    /// <summary>
    /// Represents an object returned from the Dapi image search.
    /// </summary>
    public class DapiImageObject
    {
        /// <summary>
        /// Gets or sets the URL of the image file.
        /// </summary>
        [JsonProperty("File_Url")]
        public string? FileUrl { get; set; }

        /// <summary>
        /// Gets or sets the tags associated with the image.
        /// </summary>
        public string? Tags { get; set; }

        /// <summary>
        /// Gets or sets the tag string associated with the image.
        /// </summary>
        [JsonProperty("Tag_String")]
        public string? TagString { get; set; }

        /// <summary>
        /// Gets or sets the rating of the image.
        /// </summary>
        public string? Rating { get; set; }
    }

    /// <summary>
    /// Represents a container for Derpibooru images.
    /// </summary>
    public class DerpiContainer
    {
        /// <summary>
        /// Gets or sets the array of Derpibooru images.
        /// </summary>
        public DerpiImageObject[] Images { get; set; }
    }

    /// <summary>
    /// Represents an image object from Derpibooru.
    /// </summary>
    public class DerpiImageObject
    {
        /// <summary>
        /// Gets or sets the URL of the image view.
        /// </summary>
        [JsonProperty("view_url")]
        public string ViewUrl { get; set; }

        /// <summary>
        /// Gets or sets the tags associated with the image.
        /// </summary>
        public string[] Tags { get; set; }

        /// <summary>
        /// Gets or sets the score of the image.
        /// </summary>
        public string Score { get; set; }
    }

    /// <summary>
    /// Represents the type of Dapi image search.
    /// </summary>
    public enum DapiSearchType
    {
        /// <summary>
        /// Represents the Safebooru image search.
        /// </summary>
        Safebooru,

        /// <summary>
        /// Represents the E621 image search.
        /// </summary>
        E621,

        /// <summary>
        /// Represents the Danbooru image search.
        /// </summary>
        Derpibooru,

        /// <summary>
        /// Represents the Gelbooru image search.
        /// </summary>
        Gelbooru,

        /// <summary>
        /// Represents the Konachan image search.
        /// </summary>
        Konachan,

        /// <summary>
        /// Represents the Rule34 image search.
        /// </summary>
        Rule34,

        /// <summary>
        /// Represents the Yandere image search.
        /// </summary>
        Yandere,

        /// <summary>
        /// Represents the Danbooru image search.
        /// </summary>
        Danbooru
    }

    /// <summary>
    /// Represents an element from Safebooru.
    /// </summary>
    public class SafebooruElement
    {
        /// <summary>
        /// Gets or sets the directory of the image.
        /// </summary>
        public string Directory { get; set; }

        /// <summary>
        /// Gets or sets the name of the image file.
        /// </summary>
        public string Image { get; set; }

        /// <summary>
        /// Gets the URL of the image file.
        /// </summary>
        public string FileUrl => $"https://safebooru.org/images/{Directory}/{Image}";

        /// <summary>
        /// Gets or sets the rating of the image.
        /// </summary>
        public string Rating { get; set; }

        /// <summary>
        /// Gets or sets the tags associated with the image.
        /// </summary>
        public string Tags { get; set; }
    }
}