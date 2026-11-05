#nullable enable

namespace Mewdeko.Modules.Searches.Common.StreamNotifications.Models;

/// <summary>
///     One video, post or episode read from a feed.
/// </summary>
/// <param name="Id">The item's stable identifier.</param>
/// <param name="Url">The link to the item.</param>
/// <param name="Title">The item's title or caption.</param>
/// <param name="Description">The item's text or show notes.</param>
/// <param name="Thumbnail">The item's image.</param>
/// <param name="Media">The item's media URL, such as a podcast audio file.</param>
/// <param name="Published">When the item was published.</param>
public record StreamItem(
    string Id,
    string? Url,
    string? Title,
    string? Description,
    string? Thumbnail,
    string? Media,
    DateTimeOffset? Published)
{
    /// <summary>
    ///     Copies this item onto a stream's latest item fields.
    /// </summary>
    /// <param name="stream">The stream data to copy from.</param>
    /// <returns>A copy of the stream data describing this item.</returns>
    public StreamData ApplyTo(StreamData stream)
    {
        return stream with
        {
            LatestVideoId = Id,
            LatestVideoUrl = Url,
            LatestVideoTitle = Title,
            LatestVideoDescription = Description,
            LatestVideoThumbnail = Thumbnail,
            LatestVideoMedia = Media,
            LatestVideoPublished = Published,
            NewItems = null,
            SkippedItems = 0
        };
    }
}
