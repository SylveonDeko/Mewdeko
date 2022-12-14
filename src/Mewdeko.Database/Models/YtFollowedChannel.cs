namespace Mewdeko.Database.Models;

public class YtFollowedChannel : DbEntity
{
    public ulong ChannelId { get; set; }
    public string YtChannelId { get; set; }
    public string UploadMessage { get; set; }
}