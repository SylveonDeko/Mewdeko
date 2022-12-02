namespace Mewdeko.Database.Models;

public class VoteRoles : DbEntity
{
    public ulong RoleId { get; set; }
    public ulong GuildId { get; set; }
    public int Timer { get; set; }
}