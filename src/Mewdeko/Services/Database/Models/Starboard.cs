namespace Mewdeko.Services.Database.Models;

public class StarboardPosts : DbEntity
{
    public ulong MessageId { get; set; }
    public ulong PostId { get; set; }
}