namespace Mewdeko.Database.Models;

public class CurrencyTransaction : DbEntity
{
    public long Amount { get; set; }
    public string Reason { get; set; }
    public ulong UserId { get; set; }

    public CurrencyTransaction Clone() =>
        new()
        {
            Amount = Amount, Reason = Reason, UserId = UserId
        };
}