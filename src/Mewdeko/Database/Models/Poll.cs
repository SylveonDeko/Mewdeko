using Mewdeko.Common.Collections;
using System.Collections.Generic;

namespace Mewdeko.Database.Models;

public class Poll : DbEntity
{
    public ulong GuildId { get; set; }
    public ulong ChannelId { get; set; }
    public string Question { get; set; }
    public IndexedCollection<PollAnswer> Answers { get; set; }
    public HashSet<PollVote> Votes { get; set; } = new();
}

public class PollAnswer : DbEntity, IIndexed
{
    public string Text { get; set; }
    public int Index { get; set; }
}