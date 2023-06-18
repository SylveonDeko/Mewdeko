using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;

namespace Mewdeko.Modules.Nsfw.Common.Downloaders;

public sealed class KonachanImageDownloader : ImageDownloader<DapiImageObject>
{
    private readonly string _baseUrl;

    public KonachanImageDownloader(IHttpClientFactory http)
        : base(Booru.Konachan, http)
        => _baseUrl = "https://konachan.com";

    public override async Task<List<DapiImageObject>> DownloadImagesAsync(
        string[] tags,
        int page,
        bool isExplicit = false,
        CancellationToken cancel = default)
    {
        var tagString = ImageDownloaderHelper.GetTagString(tags, isExplicit);
        var uri = $"{_baseUrl}/post.json?s=post&q=index&limit=200&tags={tagString}&page={page}";
        using var http = _http.CreateClient();
        var imageObjects = await http.GetFromJsonAsync<DapiImageObject[]>(uri, _serializerOptions, cancel);
        if (imageObjects is null)
            return new();
        return imageObjects.Where(x => x.FileUrl is not null).ToList();
    }
}