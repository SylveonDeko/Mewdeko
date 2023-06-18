using System.Net.Http;
using System.Text.Json;
using System.Threading;

namespace Mewdeko.Modules.Nsfw.Common.Downloaders;

public sealed class SankakuImageDownloader : ImageDownloader<SankakuImageObject>
{
    private readonly string baseUrl;

    public SankakuImageDownloader(IHttpClientFactory http)
        : base(Booru.Sankaku, http)
    {
        baseUrl = "https://capi-v2.sankakucomplex.com";
    }

    public override async Task<List<SankakuImageObject>> DownloadImagesAsync(
        string[] tags,
        int page,
        bool isExplicit = false,
        CancellationToken cancel = default)
    {
        // explicit probably not supported
        var tagString = ImageDownloaderHelper.GetTagString(tags);

        var uri = $"{baseUrl}/posts?tags={tagString}&limit=50";

        using var http = _http.CreateClient();
        http.AddFakeHeaders();
        var data = await http.GetStringAsync(uri, cancel);
        return JsonSerializer.Deserialize<SankakuImageObject[]>(data, _serializerOptions)
            ?.Where(x => !string.IsNullOrWhiteSpace(x.FileUrl) && x.FileType.StartsWith("image"))
            .ToList();
    }
}