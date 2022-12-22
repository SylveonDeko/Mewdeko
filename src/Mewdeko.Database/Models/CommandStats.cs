#nullable enable

namespace Mewdeko.Database.Models;

public class CommandStats : DbEntity
{
    public string NameOrId { get; set; }
    public string? Module { get; set; } = null;
    public bool IsSlash { get; set; } = false;
    public bool Trigger { get; set; } = false;
    public ulong GuildId { get; set; }
    public ulong ChannelId { get; set; }
    public ulong UserId { get; set; }
}