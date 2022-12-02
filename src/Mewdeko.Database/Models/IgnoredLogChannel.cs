namespace Mewdeko.Database.Models;

public class IgnoredLogChannel : DbEntity
{
    public LogSetting LogSetting { get; set; }
    public ulong ChannelId { get; set; }
}