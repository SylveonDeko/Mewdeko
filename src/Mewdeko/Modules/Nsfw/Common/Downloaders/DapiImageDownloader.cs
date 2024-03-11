using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;

namespace Mewdeko.Modules.Nsfw.Common.Downloaders;

public abstract class DapiImageDownloader(Booru booru, IHttpClientFactory http, string baseUrl)
    : ImageDownloader<DapiImageObject>(booru, http)
{
    protected readonly string BaseUrl = baseUrl;

    protected abstract Task<bool> IsTagValid(string tag, CancellationToken cancel = default);

    protected async Task<bool> AllTagsValid(IEnumerable<string> tags, CancellationToken cancel = default)
    {
        var results = await tags.Select(tag => IsTagValid(tag, cancel)).WhenAll();

        return results.All(result => result);
    }

    public override async Task<List<DapiImageObject>> DownloadImagesAsync(
        string[] tags,
        int page,
        bool isExplicit = false,
        CancellationToken cancel = default)
    {
        if (tags.Length > 2)
            return new List<DapiImageObject>();

        if (!await AllTagsValid(tags, cancel))
            return new List<DapiImageObject>();

        var tagString = ImageDownloaderHelper.GetTagString(tags, isExplicit);

        var uri = $"{BaseUrl}/posts.json?limit=200&tags={tagString}&page={page}";
        using var http = Http.CreateClient();
        var imageObjects = await http.GetFromJsonAsync<DapiImageObject[]>(uri, SerializerOptions, cancel);
        return imageObjects is null
            ? new List<DapiImageObject>()
            : imageObjects.Where(x => x.FileUrl is not null).ToList();
    }
}