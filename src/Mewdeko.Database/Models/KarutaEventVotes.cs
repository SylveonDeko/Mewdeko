namespace Mewdeko.Database.Models;

public class KarutaEventVotes : DbEntity
{
    public ulong GuildId { get; set; }
    public ulong MessageId { get; set; }
    public ulong UserId { get; set; }
    public int VotedNum { get; set; }
}