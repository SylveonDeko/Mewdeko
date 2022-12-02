namespace Mewdeko.Database.Models;

public class WarningPunishment : DbEntity
{
    public int Count { get; set; }
    public PunishmentAction Punishment { get; set; }
    public int Time { get; set; }
    public ulong? RoleId { get; set; }
}