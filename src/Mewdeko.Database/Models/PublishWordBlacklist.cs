using System.ComponentModel.DataAnnotations.Schema;

namespace Mewdeko.Database.Models;

[Table("PublishWordBlacklist")]
public class PublishWordBlacklist : DbEntity
{
    public ulong ChannelId { get; set; }
    public string Word { get; set; }
}