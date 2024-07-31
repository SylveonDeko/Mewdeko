using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;

namespace Mewdeko.Modules.Nsfw.Common.Downloaders
{
    /// <summary>
    /// Represents an abstract base class for downloading images using the Danbooru API.
    /// </summary>
    public abstract class DapiImageDownloader : ImageDownloader<DapiImageObject>
    {
        /// <summary>
        /// The base URL of the image downloader.
        /// </summary>
        protected readonly string BaseUrl;

        /// <summary>
        /// Initializes a new instance of the <see cref="DapiImageDownloader"/> class.
        /// </summary>
        /// <param name="booru">The booru associated with the image downloader.</param>
        /// <param name="http">The <see cref="IHttpClientFactory"/> instance for HTTP requests.</param>
        /// <param name="baseUrl">The base URL of the image downloader.</param>
        protected DapiImageDownloader(Booru booru, IHttpClientFactory http, string baseUrl)
            : base(booru, http)
        {
            BaseUrl = baseUrl;
        }

        /// <summary>
        /// Checks if a given tag is valid.
        /// </summary>
        /// <param name="tag">The tag to check.</param>
        /// <param name="cancel">A cancellation token to cancel the operation.</param>
        /// <returns><c>true</c> if the tag is valid; otherwise, <c>false</c>.</returns>
        protected abstract Task<bool> IsTagValid(string tag, CancellationToken cancel = default);

        /// <summary>
        /// Checks if all provided tags are valid.
        /// </summary>
        /// <param name="tags">The tags to check.</param>
        /// <param name="cancel">A cancellation token to cancel the operation.</param>
        /// <returns><c>true</c> if all tags are valid; otherwise, <c>false</c>.</returns>
        protected async Task<bool> AllTagsValid(IEnumerable<string> tags, CancellationToken cancel = default)
        {
            var results = await tags.Select(tag => IsTagValid(tag, cancel)).WhenAll();
            return results.All(result => result);
        }

        /// <inheritdoc/>
        public override async Task<List<DapiImageObject>> DownloadImagesAsync(
            string[] tags,
            int page,
            bool isExplicit = false,
            CancellationToken cancel = default)
        {
            if (tags.Length > 2)
                return [];

            if (!await AllTagsValid(tags, cancel))
                return [];

            var tagString = ImageDownloaderHelper.GetTagString(tags, isExplicit);
            var uri = $"{BaseUrl}/posts.json?limit=200&tags={tagString}&page={page}";

            using var http = Http.CreateClient();
            var imageObjects = await http.GetFromJsonAsync<DapiImageObject[]>(uri, SerializerOptions, cancel);
            return imageObjects is null
                ? []
                : imageObjects.Where(x => x.FileUrl is not null).ToList();
        }
    }
}