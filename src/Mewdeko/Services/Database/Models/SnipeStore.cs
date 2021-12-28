namespace Mewdeko.Services.Database.Models;

public class SnipeStore : DbEntity
{
    public ulong GuildId { get; set; }
    public ulong UserId { get; set; }
    public ulong ChannelId { get; set; }
    public string Message { get; set; }
    public ulong Edited { get; set; }
}