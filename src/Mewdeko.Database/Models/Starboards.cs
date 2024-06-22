namespace Mewdeko.Database.Models;

public class Starboards : DbEntity
{
    public string Star { get; set; } = "⭐";
    public ulong GuildId { get; set; }
    public ulong StarboardChannel { get; set; }
    public int StarboardThreshold { get; set; } = 3;
    public int RepostThreshold { get; set; } = 5;
    public bool StarboardAllowBots { get; set; } = true;
    public bool StarboardRemoveOnDelete { get; set; } = false;
    public bool StarboardRemoveOnReactionsClear { get; set; } = false;
    public bool StarboardRemoveOnBelowThreshold { get; set; } = true;
    public bool UseStarboardBlacklist { get; set; } = true;
    public string StarboardCheckChannels { get; set; } = "0";
}