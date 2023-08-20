using System.ComponentModel.DataAnnotations.Schema;

namespace Mewdeko.Database.Models;

public class UnmuteTimer : DbEntity
{
    [ForeignKey("GuildConfigId")]
    public int GuildConfigId { get; set; }

    public ulong UserId { get; set; }
    public DateTime UnmuteAt { get; set; }

    public override int GetHashCode() => UserId.GetHashCode();

    public override bool Equals(object obj) =>
        obj is UnmuteTimer ut
        && ut.UserId == UserId;
}