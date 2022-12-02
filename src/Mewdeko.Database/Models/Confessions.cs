namespace Mewdeko.Database.Models;

public class Confessions : DbEntity
{
    public ulong GuildId { get; set; }
    public ulong UserId { get; set; }
    public ulong MessageId { get; set; }
    public ulong ChannelId { get; set; }
    public ulong ConfessNumber { get; set; }
    public string Confession { get; set; }
}