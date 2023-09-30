using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;

namespace Mewdeko.Modules.Nsfw.Common.Downloaders;

public abstract class ImageDownloader<T>(Booru booru, IHttpClientFactory http) : IImageDownloader
    where T : IImageData
{
    public Booru Booru { get; } = booru;
    protected readonly IHttpClientFactory Http = http;

    protected readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString
    };

    public abstract Task<List<T>> DownloadImagesAsync(
        string[] tags,
        int page,
        bool isExplicit = false,
        CancellationToken cancel = default);

    public async Task<List<ImageData>> DownloadImageDataAsync(
        string[] tags,
        int page,
        bool isExplicit = false,
        CancellationToken cancel = default)
    {
        var images = await DownloadImagesAsync(tags, page, isExplicit, cancel);
        return images.Select(x => x.ToCachedImageData(Booru)).ToList();
    }
}