namespace Mewdeko.Database.Models;

public class RoleStateSettings : DbEntity
{
    public ulong GuildId { get; set; }
    public bool Enabled { get; set; } = false;
    public bool ClearOnBan { get; set; } = false;
    public bool IgnoreBots { get; set; } = true;
    public string DeniedRoles { get; set; } = "";
    public string DeniedUsers { get; set; } = "";
}