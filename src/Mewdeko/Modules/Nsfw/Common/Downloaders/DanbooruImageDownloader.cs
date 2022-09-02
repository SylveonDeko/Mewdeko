using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Mewdeko.Modules.Nsfw.Common.Downloaders;

public sealed class DanbooruImageDownloader : DapiImageDownloader
{
    // using them as concurrent hashsets, value doesn't matter
    private static readonly ConcurrentDictionary<string, bool> _existentTags = new();
    private static readonly ConcurrentDictionary<string, bool> _nonexistentTags = new();

    public override async Task<bool> IsTagValid(string tag, CancellationToken cancel = default)
    {
        if (_existentTags.ContainsKey(tag))
            return true;

        if (_nonexistentTags.ContainsKey(tag))
            return false;

        var tags = await _http.GetFromJsonAsync<DapiTag[]>($"{_baseUrl}/tags.json?search[name_or_alias_matches]={tag}",
            options: _serializerOptions,
            cancellationToken: cancel).ConfigureAwait(false);
        if (tags is { Length: > 0 })
        {
            return _existentTags[tag] = true;
        }

        return _nonexistentTags[tag] = false;
    }

    public DanbooruImageDownloader(HttpClient http)
        : base(Booru.Danbooru, http, "http://danbooru.donmai.us")
    {
    }
}