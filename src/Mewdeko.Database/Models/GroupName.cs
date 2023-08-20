using System.ComponentModel.DataAnnotations.Schema;

namespace Mewdeko.Database.Models;

public class GroupName : DbEntity
{
    [ForeignKey("GuildConfigId")]
    public int GuildConfigId { get; set; }

    public GuildConfig GuildConfig { get; set; }

    public int Number { get; set; }
    public string Name { get; set; }
}