using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;

namespace Mewdeko.Modules.Nsfw.Common.Downloaders;

public sealed class DanbooruImageDownloader(IHttpClientFactory http) : DapiImageDownloader(Booru.Danbooru, http,
    "http://danbooru.donmai.us")
{
    // using them as concurrent hashsets, value doesn't matter
    private static readonly ConcurrentDictionary<string, bool> ExistentTags = new();
    private static readonly ConcurrentDictionary<string, bool> NonexistentTags = new();

    protected override async Task<bool> IsTagValid(string tag, CancellationToken cancel = default)
    {
        if (ExistentTags.ContainsKey(tag))
            return true;

        if (NonexistentTags.ContainsKey(tag))
            return false;

        using var http = Http.CreateClient();
        var tags = await http.GetFromJsonAsync<DapiTag[]>(
            BaseUrl + "/tags.json" + $"?search[name_or_alias_matches]={tag}",
            SerializerOptions,
            cancel);
        if (tags is { Length: > 0 })
            return ExistentTags[tag] = true;

        return NonexistentTags[tag] = false;
    }
}