using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;

namespace Mewdeko.Modules.Nsfw.Common.Downloaders;

public class RealbooruImageDownloader(IHttpClientFactory http) : ImageDownloader<RealBooruElement>(Booru.Realbooru,
    http)
{
    public override async Task<List<RealBooruElement>> DownloadImagesAsync(
        string[] tags,
        int page,
        bool isExplicit = false,
        CancellationToken cancel = default)
    {
        var tagString = ImageDownloaderHelper.GetTagString(tags);
        var uri =
            $"https://realbooru.com/index.php?page=dapi&s=post&q=index&limit=200&tags={tagString}&json=1&pid={page}";

        using var http = Http.CreateClient();
        var images = await http.GetFromJsonAsync<List<RealBooruElement>>(uri, SerializerOptions, cancel);
        return images ?? new List<RealBooruElement>();
    }
}