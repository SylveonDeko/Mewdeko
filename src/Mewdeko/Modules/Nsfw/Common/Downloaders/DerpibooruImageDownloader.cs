using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;

namespace Mewdeko.Modules.Nsfw.Common.Downloaders
{
    /// <summary>
    /// Represents an image downloader for Derpibooru.
    /// </summary>
    public class DerpibooruImageDownloader : ImageDownloader<DerpiImageObject>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DerpibooruImageDownloader"/> class.
        /// </summary>
        /// <param name="http">The <see cref="IHttpClientFactory"/> instance for HTTP requests.</param>
        public DerpibooruImageDownloader(IHttpClientFactory http) : base(Booru.Derpibooru, http)
        {
        }

        /// <inheritdoc/>
        public override async Task<List<DerpiImageObject>> DownloadImagesAsync(
            string[] tags,
            int page,
            bool isExplicit = false,
            CancellationToken cancel = default)
        {
            var tagString = ImageDownloaderHelper.GetTagString(tags, isExplicit);
            var uri =
                $"https://www.derpibooru.org/api/v1/json/search/images?q={tagString.Replace('+', ',')}&per_page=49&page={page}";

            using var req = new HttpRequestMessage(HttpMethod.Get, uri);
            req.Headers.AddFakeHeaders();

            using var http = Http.CreateClient();
            using var res = await http.SendAsync(req, cancel);
            res.EnsureSuccessStatusCode();

            var container = await res.Content.ReadFromJsonAsync<DerpiContainer>(SerializerOptions, cancel);
            return container?.Images is null
                ? new List<DerpiImageObject>()
                : container.Images.Where(x => !string.IsNullOrWhiteSpace(x.ViewUrl)).ToList();
        }
    }
}