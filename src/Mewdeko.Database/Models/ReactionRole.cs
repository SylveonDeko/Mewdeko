using System.ComponentModel.DataAnnotations.Schema;

namespace Mewdeko.Database.Models;

public class ReactionRoleMessage : DbEntity, IIndexed
{
    [ForeignKey("GuildConfigId")]
    public int GuildConfigId { get; set; }

    public GuildConfig GuildConfig { get; set; }

    public ulong ChannelId { get; set; }
    public ulong MessageId { get; set; }

    public List<ReactionRole> ReactionRoles { get; set; }
    public long Exclusive { get; set; }
    public int Index { get; set; }
}

public class ReactionRole : DbEntity
{
    public string EmoteName { get; set; }
    public ulong RoleId { get; set; }

    [ForeignKey("ReactionRoleMessageId")]
    public int ReactionRoleMessageId { get; set; }
}