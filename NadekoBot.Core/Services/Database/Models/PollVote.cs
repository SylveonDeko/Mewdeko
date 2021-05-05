namespace NadekoBot.Core.Services.Database.Models
{
    public class PollVote : DbEntity
    {
        public ulong UserId { get; set; }
        public int VoteIndex { get; set; }

        public override int GetHashCode()
        {
            return UserId.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return obj is PollVote p
                ? p.UserId == UserId
                : false;
        }
    }
}
