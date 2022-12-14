using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Mewdeko.Modules.Nsfw.Common.Downloaders;

public class SafebooruImageDownloader : ImageDownloader<SafebooruElement>
{
    public SafebooruImageDownloader(HttpClient http) : base(Booru.Safebooru, http)
    {
    }

    public override async Task<List<SafebooruElement>> DownloadImagesAsync(string[] tags, int page, bool isExplicit = false, CancellationToken cancel = default)
    {
        var tagString = ImageDownloaderHelper.GetTagString(tags, isExplicit: false);
        var uri = $"https://safebooru.org/index.php?page=dapi&s=post&q=index&limit=200&tags={tagString}&json=1&pid={page}";
        var images = await Http.GetFromJsonAsync<List<SafebooruElement>>(uri, SerializerOptions, cancellationToken: cancel).ConfigureAwait(false);
        return images ?? new List<SafebooruElement>();
    }
}