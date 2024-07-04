namespace Mewdeko.Database.Models;

public class OwnerOnly : DbEntity
{
    public string Owners { get; set; } = "";
    public int GptTokensUsed { get; set; }
    public string CurrencyEmote { get; set; } = "💰";
    public int RewardAmount { get; set; } = 200;
    public int RewardTimeoutSeconds { get; set; } = 86400;
}