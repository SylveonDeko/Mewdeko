namespace Mewdeko.Database.Models;

public class SuggestVotes : DbEntity
{
    public ulong UserId { get; set; }
    public ulong MessageId { get; set; }
    public int EmotePicked { get; set; }
}