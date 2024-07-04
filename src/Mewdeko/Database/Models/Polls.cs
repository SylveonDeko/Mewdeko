using System.ComponentModel.DataAnnotations.Schema;
using Mewdeko.Database.Common;

namespace Mewdeko.Database.Models;

[Table("Poll")]
public class Polls : DbEntity
{
    public ulong GuildId { get; set; }
    public ulong ChannelId { get; set; }
    public string Question { get; set; }
    public IndexedCollection<PollAnswers> Answers { get; set; }
    public PollType PollType { get; set; }
    public List<PollVote> Votes { get; set; } = new();
}

[Table("PollAnswer")]
public class PollAnswers : DbEntity, IIndexed
{
    public string Text { get; set; }
    public int Index { get; set; }
}

public enum PollType
{
    SingleAnswer,
    AllowChange,
    MultiAnswer,
    PollEnded
}