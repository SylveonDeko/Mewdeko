using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;

namespace Mewdeko.Modules.Nsfw.Common.Downloaders;

public sealed class YandereImageDownloader : ImageDownloader<DapiImageObject>
{
    private readonly string _baseUrl;

    public YandereImageDownloader(IHttpClientFactory http)
        : base(Booru.Yandere, http)
        => _baseUrl = "https://yande.re";

    public override async Task<List<DapiImageObject>> DownloadImagesAsync(
        string[] tags,
        int page,
        bool isExplicit = false,
        CancellationToken cancel = default)
    {
        var tagString = ImageDownloaderHelper.GetTagString(tags, isExplicit);

        var uri = $"{_baseUrl}/post.json?limit=200&tags={tagString}&page={page}";

        using var http = _http.CreateClient();
        var imageObjects = await http.GetFromJsonAsync<DapiImageObject[]>(uri, _serializerOptions, cancel);
        if (imageObjects is null)
            return new();
        return imageObjects.Where(x => x.FileUrl is not null).ToList();
    }
}