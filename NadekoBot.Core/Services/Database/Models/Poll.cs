using NadekoBot.Common.Collections;
using System.Collections.Generic;

namespace NadekoBot.Core.Services.Database.Models
{
    public class Poll : DbEntity
    {
        public ulong GuildId { get; set; }
        public ulong ChannelId { get; set; }
        public string Question { get; set; }
        public IndexedCollection<PollAnswer> Answers { get; set; }
        public HashSet<PollVote> Votes { get; set; } = new HashSet<PollVote>();
    }

    public class PollAnswer : DbEntity, IIndexed
    {
        public int Index { get; set; }
        public string Text { get; set; }
    }
}
