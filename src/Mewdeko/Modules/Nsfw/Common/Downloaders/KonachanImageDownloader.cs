using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;

namespace Mewdeko.Modules.Nsfw.Common.Downloaders;

public sealed class KonachanImageDownloader(IHttpClientFactory http) : ImageDownloader<DapiImageObject>(Booru.Konachan,
    http)
{
    private const string BaseUrl = "https://konachan.com";

    public override async Task<List<DapiImageObject>> DownloadImagesAsync(
        string[] tags,
        int page,
        bool isExplicit = false,
        CancellationToken cancel = default)
    {
        var tagString = ImageDownloaderHelper.GetTagString(tags, isExplicit);
        var uri = $"{BaseUrl}/post.json?s=post&q=index&limit=200&tags={tagString}&page={page}";
        using var http = Http.CreateClient();
        var imageObjects = await http.GetFromJsonAsync<DapiImageObject[]>(uri, SerializerOptions, cancel);
        if (imageObjects is null)
            return new();
        return imageObjects.Where(x => x.FileUrl is not null).ToList();
    }
}