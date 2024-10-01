using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;

namespace Mewdeko.Modules.Nsfw.Common.Downloaders;

/// <summary>
///     Represents a base class for downloading images.
/// </summary>
/// <typeparam name="T">The type of image data.</typeparam>
public abstract class ImageDownloader<T> : IImageDownloader
    where T : IImageData
{
    /// <summary>
    ///     The <see cref="IHttpClientFactory" /> instance for making HTTP requests.
    /// </summary>
    protected readonly IHttpClientFactory Http;

    /// <summary>
    ///     Options for controlling the behavior during JSON serialization and deserialization.
    /// </summary>
    protected readonly JsonSerializerOptions SerializerOptions;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ImageDownloader{T}" /> class.
    /// </summary>
    /// <param name="booru">The Booru associated with the image downloader.</param>
    /// <param name="http">The <see cref="IHttpClientFactory" /> instance for making HTTP requests.</param>
    protected ImageDownloader(Booru booru, IHttpClientFactory http)
    {
        Booru = booru;
        Http = http;
        SerializerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString
        };
    }

    /// <summary>
    ///     Gets the Booru associated with the image downloader.
    /// </summary>
    public Booru Booru { get; }

    /// <inheritdoc />
    public async Task<List<ImageData>> DownloadImageDataAsync(
        string[] tags,
        int page,
        bool isExplicit = false,
        CancellationToken cancel = default)
    {
        var images = await DownloadImagesAsync(tags, page, isExplicit, cancel);
        return images.Select(x => x.ToCachedImageData(Booru)).ToList();
    }

    /// <summary>
    ///     Downloads images asynchronously.
    /// </summary>
    /// <param name="tags">An array of tags for filtering images.</param>
    /// <param name="page">The page number of the results.</param>
    /// <param name="isExplicit">Indicates whether explicit content is allowed.</param>
    /// <param name="cancel">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation, containing a list of images of type <typeparamref name="T" />.</returns>
    public abstract Task<List<T>> DownloadImagesAsync(
        string[] tags,
        int page,
        bool isExplicit = false,
        CancellationToken cancel = default);
}