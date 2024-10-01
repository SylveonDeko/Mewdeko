using System.Net.Http;
using System.Text.Json;
using System.Threading;

namespace Mewdeko.Modules.Nsfw.Common.Downloaders;

/// <summary>
///     Downloader for images from Sankakucomplex.
/// </summary>
public sealed class SankakuImageDownloader : ImageDownloader<SankakuImageObject>
{
    private readonly string baseUrl = "https://capi-v2.sankakucomplex.com";

    /// <summary>
    ///     Initializes a new instance of the <see cref="SankakuImageDownloader" /> class.
    /// </summary>
    /// <param name="http">The HTTP client factory.</param>
    public SankakuImageDownloader(IHttpClientFactory http) : base(Booru.Sankaku, http) { }

    /// <inheritdoc />
    public override async Task<List<SankakuImageObject>> DownloadImagesAsync(
        string[] tags,
        int page,
        bool isExplicit = false,
        CancellationToken cancel = default)
    {
        // explicit probably not supported
        var tagString = ImageDownloaderHelper.GetTagString(tags);

        var uri = $"{baseUrl}/posts?tags={tagString}&limit=50";

        using var http = Http.CreateClient();
        http.AddFakeHeaders();
        var data = await http.GetStringAsync(uri, cancel);
        return JsonSerializer.Deserialize<SankakuImageObject[]>(data, SerializerOptions)
            ?.Where(x => !string.IsNullOrWhiteSpace(x.FileUrl) && x.FileType.StartsWith("image"))
            .ToList();
    }
}