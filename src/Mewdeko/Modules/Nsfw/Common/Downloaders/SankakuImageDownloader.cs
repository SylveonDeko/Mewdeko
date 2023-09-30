using System.Net.Http;
using System.Text.Json;
using System.Threading;

namespace Mewdeko.Modules.Nsfw.Common.Downloaders;

public sealed class SankakuImageDownloader(IHttpClientFactory http) : ImageDownloader<SankakuImageObject>(Booru.Sankaku,
    http)
{
    private readonly string baseUrl = "https://capi-v2.sankakucomplex.com";

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