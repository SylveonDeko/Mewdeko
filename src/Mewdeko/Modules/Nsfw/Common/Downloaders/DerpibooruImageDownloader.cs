using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Mewdeko.Modules.Nsfw.Common.Downloaders;

public class DerpibooruImageDownloader : ImageDownloader<DerpiImageObject>
{
    public DerpibooruImageDownloader(HttpClient http) : base(Booru.Derpibooru, http)
    {
    }

    public override async Task<List<DerpiImageObject>> DownloadImagesAsync(string[] tags, int page, bool isExplicit = false, CancellationToken cancel = default)
    {
        var tagString = ImageDownloaderHelper.GetTagString(tags, isExplicit);
        var uri = $"https://www.derpibooru.org/api/v1/json/search/images?q={tagString.Replace('+', ',')}&per_page=49&page={page}";
        using var req = new HttpRequestMessage(HttpMethod.Get, uri);
        req.Headers.AddFakeHeaders();
        using var res = await Http.SendAsync(req, cancel).ConfigureAwait(false);
        res.EnsureSuccessStatusCode();

        var container = await res.Content.ReadFromJsonAsync<DerpiContainer>(SerializerOptions, cancel).ConfigureAwait(false);
        if (container?.Images is null)
            return new List<DerpiImageObject>();

        return container.Images
            .Where(x => !string.IsNullOrWhiteSpace(x.ViewUrl))
            .ToList();
    }
}