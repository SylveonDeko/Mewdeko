namespace Mewdeko.Database.Models;

public class AutoBanRoles : DbEntity
{
    public ulong GuildId { get; set; }
    public ulong RoleId { get; set; }
}