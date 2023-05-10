namespace Mewdeko.Database.Models;

public class RoleStateSettings : DbEntity
{
    public ulong GuildId { get; set; }
    public bool Enabled { get; set; }
    public bool ClearOnBan { get; set; }
    public bool IgnoreBots { get; set; } = true;
    public string DeniedRoles { get; set; } = "";
    public string DeniedUsers { get; set; } = "";
}