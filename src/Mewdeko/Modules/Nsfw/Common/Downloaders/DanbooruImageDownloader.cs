using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Mewdeko.Modules.Nsfw.Common.Downloaders;

public sealed class DanbooruImageDownloader : DapiImageDownloader
{
    // using them as concurrent hashsets, value doesn't matter
    private static readonly ConcurrentDictionary<string, bool> ExistentTags = new();
    private static readonly ConcurrentDictionary<string, bool> NonexistentTags = new();

    public override async Task<bool> IsTagValid(string tag, CancellationToken cancel = default)
    {
        if (ExistentTags.ContainsKey(tag))
            return true;

        if (NonexistentTags.ContainsKey(tag))
            return false;

        var tags = await Http.GetFromJsonAsync<DapiTag[]>($"{BaseUrl}/tags.json?search[name_or_alias_matches]={tag}",
            options: SerializerOptions,
            cancellationToken: cancel).ConfigureAwait(false);
        if (tags is { Length: > 0 })
        {
            return ExistentTags[tag] = true;
        }

        return NonexistentTags[tag] = false;
    }

    public DanbooruImageDownloader(HttpClient http)
        : base(Booru.Danbooru, http, "https://danbooru.donmai.us")
    {
    }
}