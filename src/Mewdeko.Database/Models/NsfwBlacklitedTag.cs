using System.ComponentModel.DataAnnotations.Schema;

namespace Mewdeko.Database.Models;

public class NsfwBlacklitedTag : DbEntity
{
    [ForeignKey("GuildConfigId")]
    public int GuildConfigId { get; set; }

    public string Tag { get; set; }

    public override int GetHashCode() => Tag.GetHashCode(StringComparison.InvariantCulture);

    public override bool Equals(object obj) =>
        obj is NsfwBlacklitedTag x
        && x.Tag == Tag;
}