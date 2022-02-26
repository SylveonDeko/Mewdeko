using System.Threading;

namespace Mewdeko.Modules.Nsfw.Common.Downloaders;

public interface IImageDownloader
{
    Task<List<ImageData>> DownloadImageDataAsync(string[] tags, int page = 0,
        bool isExplicit = false, CancellationToken cancel = default);
}