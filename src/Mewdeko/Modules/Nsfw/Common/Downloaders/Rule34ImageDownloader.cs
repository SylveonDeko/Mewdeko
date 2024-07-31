using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;

namespace Mewdeko.Modules.Nsfw.Common.Downloaders
{
    /// <summary>
    /// Downloader for images from Rule34.
    /// </summary>
    public class Rule34ImageDownloader : ImageDownloader<Rule34Object>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Rule34ImageDownloader"/> class.
        /// </summary>
        /// <param name="http">The HTTP client factory.</param>
        public Rule34ImageDownloader(IHttpClientFactory http) : base(Booru.Rule34, http) { }

        /// <inheritdoc/>
        public override async Task<List<Rule34Object>> DownloadImagesAsync(
            string[] tags,
            int page,
            bool isExplicit = false,
            CancellationToken cancel = default)
        {
            var tagString = ImageDownloaderHelper.GetTagString(tags);
            var uri = $"https://api.rule34.xxx//index.php?page=dapi&s=post"
                      + $"&q=index"
                      + $"&json=1"
                      + $"&limit=100"
                      + $"&tags={tagString}"
                      + $"&pid={page}";

            using var http = Http.CreateClient();
            // Adding custom headers to handle CF clearance and user agent
            http.DefaultRequestHeaders.TryAddWithoutValidation("cookie",
                "cf_clearance=Gg3bVffg9fOL_.9fIdKmu5PJS86eTI.yTrhbR8z2tPc-1652310659-0-250");
            http.DefaultRequestHeaders.TryAddWithoutValidation("user-agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/100.0.4896.60 Safari/537.36");

            var images = await http.GetFromJsonAsync<List<Rule34Object>>(uri, SerializerOptions, cancel);

            return images is null
                ? []
                : images.Where(img => !string.IsNullOrWhiteSpace(img.Image)).ToList();
        }
    }
}