using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Mewdeko.Modules.Nsfw.Common.Downloaders;

public abstract class ImageDownloader<T> : IImageDownloader
    where T : IImageData
{
    protected readonly HttpClient Http;

    protected readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true, NumberHandling = JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString
    };

    public Booru Booru { get; }

    public ImageDownloader(Booru booru, HttpClient http)
    {
        Http = http;
        Booru = booru;
    }

    public abstract Task<List<T>> DownloadImagesAsync(string[] tags, int page, bool isExplicit = false, CancellationToken cancel = default);

    public async Task<List<ImageData>> DownloadImageDataAsync(string[] tags, int page, bool isExplicit = false,
        CancellationToken cancel = default)
    {
        var images = await DownloadImagesAsync(tags, page, isExplicit, cancel).ConfigureAwait(false);
        return images.Select(x => x.ToCachedImageData(Booru)).ToList();
    }
}