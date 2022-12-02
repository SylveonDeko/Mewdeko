using Discord;

namespace Mewdeko.Database.Models;

public class DiscordPermOverride : DbEntity
{
    public GuildPermission Perm { get; set; }

    public ulong? GuildId { get; set; }
    public string Command { get; set; }
}