using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;

namespace Mewdeko.Modules.Nsfw.Common.Downloaders;

public class RealbooruImageDownloader : ImageDownloader<RealBooruElement>
{
    public RealbooruImageDownloader(IHttpClientFactory http)
        : base(Booru.Realbooru, http)
    {
    }

    public override async Task<List<RealBooruElement>> DownloadImagesAsync(
        string[] tags,
        int page,
        bool isExplicit = false,
        CancellationToken cancel = default)
    {
        var tagString = ImageDownloaderHelper.GetTagString(tags);
        var uri =
            $"https://realbooru.com/index.php?page=dapi&s=post&q=index&limit=200&tags={tagString}&json=1&pid={page}";

        using var http = _http.CreateClient();
        var images = await http.GetFromJsonAsync<List<RealBooruElement>>(uri, _serializerOptions, cancel);
        return images ?? new List<RealBooruElement>();
    }
}