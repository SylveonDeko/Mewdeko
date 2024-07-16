using System.ComponentModel.DataAnnotations.Schema;

namespace Mewdeko.Database.Models
{
    /// <summary>
    /// Represents a vote in a poll.
    /// </summary>
    [Table("PollVote")]
    public class PollVote : DbEntity
    {
        /// <summary>
        /// Gets or sets the user ID of the voter.
        /// </summary>
        public ulong UserId { get; set; }

        /// <summary>
        /// Gets or sets the index of the vote.
        /// </summary>
        public int VoteIndex { get; set; }

        /// <summary>
        /// No idea why honestly.
        /// </summary>
        public int PollId { get; set; }

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        /// <returns>A hash code for the current object.</returns>
        public override int GetHashCode() => UserId.GetHashCode();

        /// <summary>
        /// Determines whether the specified object is equal to the current object.
        /// </summary>
        /// <param name="obj">The object to compare with the current object.</param>
        /// <returns>true if the specified object is equal to the current object; otherwise, false.</returns>
        public override bool Equals(object obj) =>
            obj is PollVote p && p.UserId == UserId;
    }
}