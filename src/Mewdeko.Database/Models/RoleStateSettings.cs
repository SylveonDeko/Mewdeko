namespace Mewdeko.Database.Models;

public class RoleStateSettings : DbEntity
{
    public ulong GuildId { get; set; }
    public long Enabled { get; set; }
    public long ClearOnBan { get; set; }
    public long IgnoreBots { get; set; } = 1;
    public string DeniedRoles { get; set; } = "";
    public string DeniedUsers { get; set; } = "";
}