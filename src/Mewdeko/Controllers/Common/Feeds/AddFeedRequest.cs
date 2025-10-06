namespace Mewdeko.Controllers.Common.Feeds;

/// <summary>
///     Request model for adding a feed subscription
/// </summary>
public class AddFeedRequest
{
    /// <summary>
    ///     The channel ID where feed updates should be posted
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     The URL of the RSS feed
    /// </summary>
    public string Url { get; set; }
}