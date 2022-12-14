using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Mewdeko.Modules.Nsfw.Common.Downloaders;

public class GelbooruImageDownloader : ImageDownloader<DapiImageObject>
{
    public GelbooruImageDownloader(HttpClient http) : base(Booru.Gelbooru, http)
    {
    }

    public override async Task<List<DapiImageObject>> DownloadImagesAsync(string[] tags, int page,
        bool isExplicit = false, CancellationToken cancel = default)
    {
        var tagString = ImageDownloaderHelper.GetTagString(tags, isExplicit);
        var uri =
            $"https://gelbooru.com/index.php?page=dapi&s=post&json=1&q=index&limit=100&tags={tagString}&pid={page}";
        using var req = new HttpRequestMessage(HttpMethod.Get, uri);
        using var res = await Http.SendAsync(req, cancel).ConfigureAwait(false);
        res.EnsureSuccessStatusCode();
        var resString = await res.Content.ReadAsStringAsync(cancel).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(resString))
            return new List<DapiImageObject>();

        var images = JsonSerializer.Deserialize<GelbooruResponse>(resString, SerializerOptions);
        if (images is null or { Post: null })
            return new List<DapiImageObject>();

        return images.Post.Where(x => x.FileUrl is not null).ToList();
    }
}

public class GelbooruResponse
{
    [JsonPropertyName("post")]
    public List<DapiImageObject> Post { get; set; }
}