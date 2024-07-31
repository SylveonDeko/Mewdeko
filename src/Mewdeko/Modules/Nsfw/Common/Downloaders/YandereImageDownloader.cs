using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;

namespace Mewdeko.Modules.Nsfw.Common.Downloaders
{
    /// <summary>
    /// Downloader for images from Yande.re.
    /// </summary>
    public sealed class YandereImageDownloader : ImageDownloader<DapiImageObject>
    {
        private const string BaseUrl = "https://yande.re";

        /// <summary>
        /// Initializes a new instance of the <see cref="YandereImageDownloader"/> class.
        /// </summary>
        /// <param name="http">The HTTP client factory.</param>
        public YandereImageDownloader(IHttpClientFactory http) : base(Booru.Yandere, http) { }

        /// <inheritdoc/>
        public override async Task<List<DapiImageObject>> DownloadImagesAsync(
            string[] tags,
            int page,
            bool isExplicit = false,
            CancellationToken cancel = default)
        {
            var tagString = ImageDownloaderHelper.GetTagString(tags, isExplicit);

            var uri = $"{BaseUrl}/post.json?limit=200&tags={tagString}&page={page}";

            using var http = Http.CreateClient();
            var imageObjects = await http.GetFromJsonAsync<DapiImageObject[]>(uri, SerializerOptions, cancel);
            if (imageObjects is null)
                return [];
            return imageObjects.Where(x => x.FileUrl is not null).ToList();
        }
    }
}