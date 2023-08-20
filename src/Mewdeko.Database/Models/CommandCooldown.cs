using System.ComponentModel.DataAnnotations.Schema;

namespace Mewdeko.Database.Models;

public class CommandCooldown : DbEntity
{
    public int Seconds { get; set; }

    [ForeignKey("GuildConfigId")]
    public int GuildConfigId { get; set; }

    public string CommandName { get; set; }
}