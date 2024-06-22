namespace Mewdeko.Database.Models;

public class JoinLeaveLogs : DbEntity
{
    public ulong GuildId { get; set; }
    public ulong UserId { get; set; }
    public bool IsJoin { get; set; }
}