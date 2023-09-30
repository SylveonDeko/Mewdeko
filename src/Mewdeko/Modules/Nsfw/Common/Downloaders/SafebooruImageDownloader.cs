using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;

namespace Mewdeko.Modules.Nsfw.Common.Downloaders;

public class SafebooruImageDownloader(IHttpClientFactory http) : ImageDownloader<SafebooruElement>(Booru.Safebooru,
    http)
{
    public override async Task<List<SafebooruElement>> DownloadImagesAsync(
        string[] tags,
        int page,
        bool isExplicit = false,
        CancellationToken cancel = default)
    {
        var tagString = ImageDownloaderHelper.GetTagString(tags);
        var uri =
            $"https://safebooru.org/index.php?page=dapi&s=post&q=index&limit=200&tags={tagString}&json=1&pid={page}";

        using var http = Http.CreateClient();
        var images = await http.GetFromJsonAsync<List<SafebooruElement>>(uri, SerializerOptions, cancel);
        if (images is null)
            return new();

        return images;
    }
}