namespace Mewdeko.Database.Models;

public class KarutaEventEntry : DbEntity
{
    public ulong GuildId { get; set; }
    public ulong ChannelId { get; set; }
    public ulong MessageId { get; set; }
    public int EntryNumber { get; set; }
    public int Button1Count { get; set; }
    public int Button2Count { get; set; }
    public int Button3Count { get; set; }
    public int Button4Count { get; set; }
    public int Button5Count { get; set; }
    public int Button6Count { get; set; }
}