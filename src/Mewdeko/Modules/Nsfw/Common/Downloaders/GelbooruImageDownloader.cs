using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;

namespace Mewdeko.Modules.Nsfw.Common.Downloaders
{
    /// <summary>
    /// Represents an image downloader for Gelbooru.
    /// </summary>
    public class GelbooruImageDownloader : ImageDownloader<DapiImageObject>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GelbooruImageDownloader"/> class.
        /// </summary>
        /// <param name="http">The <see cref="IHttpClientFactory"/> instance for HTTP requests.</param>
        public GelbooruImageDownloader(IHttpClientFactory http) : base(Booru.Gelbooru, http)
        {
        }

        /// <inheritdoc/>
        public override async Task<List<DapiImageObject>> DownloadImagesAsync(
            string[] tags,
            int page,
            bool isExplicit = false,
            CancellationToken cancel = default)
        {
            var tagString = ImageDownloaderHelper.GetTagString(tags, isExplicit);
            var uri = $"https://gelbooru.com/index.php?page=dapi"
                      + $"&s=post"
                      + $"&json=1"
                      + $"&q=index"
                      + $"&limit=100"
                      + $"&tags={tagString}"
                      + $"&pid={page}";

            using var req = new HttpRequestMessage(HttpMethod.Get, uri);
            using var http = Http.CreateClient();
            using var res = await http.SendAsync(req, cancel);
            res.EnsureSuccessStatusCode();

            var resString = await res.Content.ReadAsStringAsync(cancel);
            if (string.IsNullOrWhiteSpace(resString))
                return [];

            var images = JsonSerializer.Deserialize<GelbooruResponse>(resString, SerializerOptions);
            if (images is null || images.Post is null)
                return [];

            return images.Post.Where(x => x.FileUrl is not null).ToList();
        }
    }

    /// <summary>
    /// Represents the response object from the Gelbooru API.
    /// </summary>
    public class GelbooruResponse
    {
        /// <summary>
        /// Gets or sets the list of Gelbooru posts.
        /// </summary>
        [JsonPropertyName("post")]
        public List<DapiImageObject> Post { get; set; }
    }
}