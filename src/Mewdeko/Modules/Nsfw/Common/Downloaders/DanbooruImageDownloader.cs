using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;

namespace Mewdeko.Modules.Nsfw.Common.Downloaders;

/// <summary>
///     Represents an image downloader for Danbooru.
/// </summary>
public sealed class DanbooruImageDownloader : DapiImageDownloader
{
    private static readonly ConcurrentDictionary<string, bool> ExistentTags = new();
    private static readonly ConcurrentDictionary<string, bool> NonexistentTags = new();

    /// <summary>
    ///     Initializes a new instance of the <see cref="DanbooruImageDownloader" /> class.
    /// </summary>
    /// <param name="http">The <see cref="IHttpClientFactory" /> instance for HTTP requests.</param>
    public DanbooruImageDownloader(IHttpClientFactory http) : base(Booru.Danbooru, http,
        "http://danbooru.donmai.us")
    {
    }

    /// <summary>
    ///     Checks if a given tag is valid.
    /// </summary>
    /// <param name="tag">The tag to check.</param>
    /// <param name="cancel">A cancellation token to cancel the operation.</param>
    /// <returns><c>true</c> if the tag is valid; otherwise, <c>false</c>.</returns>
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