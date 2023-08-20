using System.ComponentModel.DataAnnotations.Schema;

namespace Mewdeko.Database.Models;

public class FilteredWord : DbEntity
{
    public string Word { get; set; }

    [ForeignKey("GuildConfigId")]
    public int GuildConfigId { get; set; }
}