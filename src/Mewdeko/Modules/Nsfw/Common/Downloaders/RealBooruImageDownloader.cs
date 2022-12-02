using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Mewdeko.Modules.Nsfw.Common.Downloaders;

public class RealBooruImageDownloader : ImageDownloader<RealBooruElement>
{
    public RealBooruImageDownloader(HttpClient http) : base(Booru.Realbooru, http)
    {
    }

    public override async Task<List<RealBooruElement>> DownloadImagesAsync(string[] tags, int page, bool isExplicit = false, CancellationToken cancel = default)
    {
        var tagString = ImageDownloaderHelper.GetTagString(tags, isExplicit: false);
        var uri = $"https://realbooru.com/index.php?page=dapi&s=post&q=index&limit=200&tags={tagString}&json=1&pid={page}";
        var images = await Http.GetFromJsonAsync<List<RealBooruElement>>(uri, SerializerOptions, cancellationToken: cancel).ConfigureAwait(false);
        return images ?? new List<RealBooruElement>();
    }
}