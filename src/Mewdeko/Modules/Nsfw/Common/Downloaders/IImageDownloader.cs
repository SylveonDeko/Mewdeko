using System.Threading;

namespace Mewdeko.Modules.Nsfw.Common.Downloaders
{
    /// <summary>
    /// Represents an interface for downloading image data.
    /// </summary>
    public interface IImageDownloader
    {
        /// <summary>
        /// Downloads image data asynchronously.
        /// </summary>
        /// <param name="tags">An array of tags for filtering images.</param>
        /// <param name="page">The page number of the results.</param>
        /// <param name="isExplicit">Indicates whether explicit content is allowed.</param>
        /// <param name="cancel">A cancellation token that can be used to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation, containing a list of <see cref="ImageData"/>.</returns>
        Task<List<ImageData>> DownloadImageDataAsync(
            string[] tags,
            int page = 0,
            bool isExplicit = false,
            CancellationToken cancel = default);
    }
}