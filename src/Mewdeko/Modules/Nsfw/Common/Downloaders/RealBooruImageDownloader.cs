using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;

namespace Mewdeko.Modules.Nsfw.Common.Downloaders;

/// <summary>
///     Downloader for images from Realbooru.
/// </summary>
public class RealbooruImageDownloader : ImageDownloader<RealBooruElement>
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="RealbooruImageDownloader" /> class.
    /// </summary>
    /// <param name="http">The HTTP client factory.</param>
    public RealbooruImageDownloader(IHttpClientFactory http) : base(Booru.Realbooru, http) { }

    /// <inheritdoc />
    public override async Task<List<RealBooruElement>> DownloadImagesAsync(
        string[] tags,
        int page,
        bool isExplicit = false,
        CancellationToken cancel = default)
    {
        var tagString = ImageDownloaderHelper.GetTagString(tags);
        var uri =
            $"https://realbooru.com/index.php?page=dapi&s=post&q=index&limit=200&tags={tagString}&json=1&pid={page}";

        using var http = Http.CreateClient();
        var images = await http.GetFromJsonAsync<List<RealBooruElement>>(uri, SerializerOptions, cancel);
        return images ?? [];
    }
}