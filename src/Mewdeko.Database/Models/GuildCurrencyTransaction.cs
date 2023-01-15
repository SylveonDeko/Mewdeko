namespace Mewdeko.Database.Models;

public class GuildCurrencyTransaction : DbEntity
{
    public long Amount { get; set; }
    public string Reason { get; set; }
    public ulong UserId { get; set; }
    public ulong GuildId { get; set; }

    public GuildCurrencyTransaction Clone() =>
        new()
        {
            Amount = Amount, Reason = Reason, UserId = UserId, GuildId = GuildId
        };
}