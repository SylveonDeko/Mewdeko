namespace Mewdeko.Database.Models;

public class IgnoredVoicePresenceChannel : DbEntity
{
    public LogSetting LogSetting { get; set; }
    public ulong ChannelId { get; set; }
}