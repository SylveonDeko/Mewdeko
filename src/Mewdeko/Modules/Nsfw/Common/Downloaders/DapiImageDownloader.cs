using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Mewdeko.Modules.Nsfw.Common.Downloaders;

public abstract class DapiImageDownloader : ImageDownloader<DapiImageObject>
{
    protected readonly string BaseUrl;

    protected DapiImageDownloader(Booru booru, HttpClient http, string baseUrl) : base(booru, http) => BaseUrl = baseUrl;

    public abstract Task<bool> IsTagValid(string tag, CancellationToken cancel = default);

    protected async Task<bool> AllTagsValid(IEnumerable<string> tags, CancellationToken cancel = default)
    {
        var results = await Task.WhenAll(tags.Select(tag => IsTagValid(tag, cancel))).ConfigureAwait(false);

        // if any of the tags is not valid, the query is not valid
        return results.All(result => result);
    }

    public override async Task<List<DapiImageObject>> DownloadImagesAsync(string[] tags, int page,
        bool isExplicit = false, CancellationToken cancel = default)
    {
        // up to 2 tags allowed on danbooru
        if (tags.Length > 2)
            return new List<DapiImageObject>();

        if (!await AllTagsValid(tags, cancel).ConfigureAwait(false))
            return new List<DapiImageObject>();

        var tagString = ImageDownloaderHelper.GetTagString(tags, isExplicit);

        var uri = $"{BaseUrl}/posts.json?limit=200&tags={tagString}&page={page}";
        var imageObjects = await Http.GetFromJsonAsync<DapiImageObject[]>(uri, SerializerOptions, cancel)
            .ConfigureAwait(false);
        if (imageObjects is null)
            return new List<DapiImageObject>();
        return imageObjects
            .Where(x => x.FileUrl is not null)
            .ToList();
    }
}