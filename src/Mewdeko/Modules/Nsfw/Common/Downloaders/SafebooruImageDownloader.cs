using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;

namespace Mewdeko.Modules.Nsfw.Common.Downloaders;

/// <summary>
///     Downloader for images from Safebooru.
/// </summary>
public class SafebooruImageDownloader : ImageDownloader<SafebooruElement>
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="SafebooruImageDownloader" /> class.
    /// </summary>
    /// <param name="http">The HTTP client factory.</param>
    public SafebooruImageDownloader(IHttpClientFactory http) : base(Booru.Safebooru, http) { }

    /// <inheritdoc />
    public override async Task<List<SafebooruElement>> DownloadImagesAsync(
        string[] tags,
        int page,
        bool isExplicit = false,
        CancellationToken cancel = default)
    {
        var tagString = ImageDownloaderHelper.GetTagString(tags);
        var uri =
            $"https://safebooru.org/index.php?page=dapi&s=post&q=index&limit=200&tags={tagString}&json=1&pid={page}";

        using var http = Http.CreateClient();
        var images = await http.GetFromJsonAsync<List<SafebooruElement>>(uri, SerializerOptions, cancel);
        if (images is null)
            return [];

        return images;
    }
}