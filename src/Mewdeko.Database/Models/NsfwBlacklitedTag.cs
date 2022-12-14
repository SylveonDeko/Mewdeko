namespace Mewdeko.Database.Models;

public class NsfwBlacklitedTag : DbEntity
{
    public string Tag { get; set; }

    public override int GetHashCode() => Tag.GetHashCode(StringComparison.InvariantCulture);

    public override bool Equals(object obj) =>
        obj is NsfwBlacklitedTag x
        && x.Tag == Tag;
}