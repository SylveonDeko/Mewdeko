namespace Mewdeko.Database.Models;

public class ReactionRoleMessage : DbEntity, IIndexed
{
    public int GuildConfigId { get; set; }
    public GuildConfig GuildConfig { get; set; }

    public ulong ChannelId { get; set; }
    public ulong MessageId { get; set; }

    public List<ReactionRole> ReactionRoles { get; set; }
    public bool Exclusive { get; set; }
    public int Index { get; set; }
}

public class ReactionRole : DbEntity
{
    public string EmoteName { get; set; }
    public ulong RoleId { get; set; }
}