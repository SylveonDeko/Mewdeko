using Mewdeko.Database.Common;

namespace Mewdeko.Database.Models;

public class Poll : DbEntity
{
    public ulong GuildId { get; set; }
    public ulong ChannelId { get; set; }
    public string Question { get; set; }
    public IndexedCollection<PollAnswer> Answers { get; set; }
    public PollType PollType { get; set; }
    public List<PollVote> Votes { get; set; } = new();
}

public class PollAnswer : DbEntity, IIndexed
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