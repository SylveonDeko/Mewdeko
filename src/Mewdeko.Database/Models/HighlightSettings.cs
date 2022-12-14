namespace Mewdeko.Database.Models;

public class HighlightSettings : DbEntity
{
    public ulong GuildId { get; set; }
    public ulong UserId { get; set; }
    public string IgnoredChannels { get; set; }
    public string IgnoredUsers { get; set; }
    public bool HighlightsOn { get; set; }
}