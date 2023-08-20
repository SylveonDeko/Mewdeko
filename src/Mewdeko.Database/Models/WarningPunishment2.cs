using System.ComponentModel.DataAnnotations.Schema;

namespace Mewdeko.Database.Models;

public class WarningPunishment2 : DbEntity
{
    [ForeignKey("GuildConfigId")]
    public int GuildConfigId { get; set; }

    public int Count { get; set; }
    public PunishmentAction Punishment { get; set; }
    public int Time { get; set; }
    public ulong? RoleId { get; set; }
}