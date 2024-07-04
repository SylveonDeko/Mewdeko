using System.ComponentModel.DataAnnotations.Schema;

namespace Mewdeko.Database.Models;

[Table("PublishUserBlacklist")]
public class PublishUserBlacklist : DbEntity
{
    public ulong ChannelId { get; set; }
    public ulong User { get; set; }
}