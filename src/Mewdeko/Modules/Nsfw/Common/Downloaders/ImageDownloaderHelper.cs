namespace Mewdeko.Modules.Nsfw.Common.Downloaders;

/// <summary>
///     Helper class for image downloader functionality.
/// </summary>
public static class ImageDownloaderHelper
{
    /// <summary>
    ///     Constructs a tag string from a collection of tags and optional explicit flag.
    /// </summary>
    /// <param name="tags">The collection of tags.</param>
    /// <param name="isExplicit">A flag indicating whether the content is explicit.</param>
    /// <returns>A string representing the constructed tag string.</returns>
    public static string GetTagString(IEnumerable<string> tags, bool isExplicit = false)
    {
        if (isExplicit)
            tags = tags.Append("rating:explicit");

        return string.Join('+', tags.Select(x => x.ToLowerInvariant()));
    }
}