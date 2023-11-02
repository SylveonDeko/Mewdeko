namespace Mewdeko.Database.Models;

public class RoleWatch : DbEntity
{
    public ulong GuildId { get; set; }
    public ulong RoleId { get; set; }
    public string AllowedUsers { get; set; }
    public string AllowedRoles { get; set; }
    public string DeniedUsers { get; set; }
    public string DeniedRoles { get; set; }
    public ulong AlertChannel { get; set; }
    public PunishmentAction Action { get; set; }
    public int PunishDuration { get; set; }
    public int AllowedPermissions { get; set; }
}