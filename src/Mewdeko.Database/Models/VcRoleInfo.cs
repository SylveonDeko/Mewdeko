using System.ComponentModel.DataAnnotations.Schema;

namespace Mewdeko.Database.Models;

[Table("VcRoleInfo")]
public class VcRoleInfo : DbEntity
{
    [ForeignKey("GuildConfigId")]
    public int GuildConfigId { get; set; }

    public ulong VoiceChannelId { get; set; }
    public ulong RoleId { get; set; }
}