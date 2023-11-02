namespace Mewdeko.Database.Models;

public class ModLogs : DbEntity
{
    public ulong GuildId { get; set; }
    public ulong ChannelId { get; set; }
    public ulong UserId { get; set; }
    public string Action { get; set; }
    public string Reason { get; set; }
}