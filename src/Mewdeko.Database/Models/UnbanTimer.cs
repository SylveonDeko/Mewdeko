using System.ComponentModel.DataAnnotations.Schema;

namespace Mewdeko.Database.Models;

public class UnbanTimer : DbEntity
{
    [ForeignKey("GuildConfigId")]
    public int GuildConfigId { get; set; }

    public ulong UserId { get; set; }
    public DateTime UnbanAt { get; set; }

    public override int GetHashCode() => UserId.GetHashCode();

    public override bool Equals(object obj) =>
        obj is UnbanTimer ut
        && ut.UserId == UserId;
}