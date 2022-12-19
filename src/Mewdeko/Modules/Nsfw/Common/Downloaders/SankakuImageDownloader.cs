using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Mewdeko.Modules.Nsfw.Common.Downloaders;

public sealed class SankakuImageDownloader : ImageDownloader<SankakuImageObject>
{
    private readonly string baseUrl;

    public SankakuImageDownloader(HttpClient http)
        : base(Booru.Sankaku, http)
    {
        baseUrl = "https://capi-v2.sankakucomplex.com";
        Http.AddFakeHeaders();
    }

    public override async Task<List<SankakuImageObject>> DownloadImagesAsync(string[] tags, int page, bool isExplicit = false, CancellationToken cancel = default)
    {
        // explicit probably not supported
        var tagString = ImageDownloaderHelper.GetTagString(tags);

        var uri = $"{baseUrl}/posts?tags={tagString}&limit=50";
        var data = await Http.GetStringAsync(uri, cancel).ConfigureAwait(false);
        return JsonSerializer.Deserialize<SankakuImageObject[]>(data, SerializerOptions)
            .Where(x => !string.IsNullOrWhiteSpace(x.FileUrl) && x.FileType.StartsWith("image"))
            .ToList();
    }
}