using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;

namespace Mewdeko.Modules.Nsfw.Common.Downloaders;

public class GelbooruImageDownloader(IHttpClientFactory http) : ImageDownloader<DapiImageObject>(Booru.Gelbooru, http)
{
    public override async Task<List<DapiImageObject>> DownloadImagesAsync(
        string[] tags,
        int page,
        bool isExplicit = false,
        CancellationToken cancel = default)
    {
        var tagString = ImageDownloaderHelper.GetTagString(tags, isExplicit);
        var uri = $"https://gelbooru.com/index.php?page=dapi"
                  + $"&s=post"
                  + $"&json=1"
                  + $"&q=index"
                  + $"&limit=100"
                  + $"&tags={tagString}"
                  + $"&pid={page}";
        using var req = new HttpRequestMessage(HttpMethod.Get, uri);
        using var http = Http.CreateClient();
        using var res = await http.SendAsync(req, cancel);
        res.EnsureSuccessStatusCode();
        var resString = await res.Content.ReadAsStringAsync(cancel);
        if (string.IsNullOrWhiteSpace(resString))
            return new();

        var images = JsonSerializer.Deserialize<GelbooruResponse>(resString, SerializerOptions);
        if (images is null or { Post: null })
            return new();

        return images.Post.Where(x => x.FileUrl is not null).ToList();
    }
}

public class GelbooruResponse
{
    [JsonPropertyName("post")]
    public List<DapiImageObject> Post { get; set; }
}