using System.Threading;
using Mewdeko.Database.DbContextStuff;
using Poll = Mewdeko.Database.Models.Poll;

namespace Mewdeko.Modules.Games.Common
{
    /// <summary>
    /// Represents a runner for managing and handling votes in a poll.
    /// </summary>
    public class PollRunner
    {
        private readonly DbContextProvider dbProvider;
        private readonly SemaphoreSlim locker = new(1, 1);

        /// <summary>
        /// Initializes a new instance of the <see cref="PollRunner"/> class with the specified database service and poll.
        /// </summary>
        /// <param name="db">The database service.</param>
        /// <param name="poll">The poll to manage.</param>
        public PollRunner(DbContextProvider dbProvider, Poll poll)
        {
            this.dbProvider = dbProvider;
            Poll = poll;
        }

        /// <summary>
        /// Gets the poll managed by this poll runner.
        /// </summary>
        public Poll Poll { get; }

        /// <summary>
        /// Tries to vote in the poll with the specified vote number and user.
        /// </summary>
        /// <param name="num">The vote number.</param>
        /// <param name="user">The user who is voting.</param>
        /// <returns>
        /// A tuple containing a boolean indicating whether the vote is allowed,
        /// and the type of poll.
        /// </returns>
        public async Task<(bool allowed, PollType type)> TryVote(int num, IUser user)
        {
            PollVote voteObj;
            await locker.WaitAsync().ConfigureAwait(false);
            try
            {
                await using var dbContext = await dbProvider.GetContextAsync();

                voteObj = new PollVote
                {
                    UserId = user.Id, VoteIndex = num
                };
                var voteCheck = Poll.Votes.Find(x => x.UserId == user.Id && x.VoteIndex == num) == null;
                switch (Poll.PollType)
                {
                    case PollType.SingleAnswer when !Poll.Votes.Contains(voteObj):
                    {

                        var trackedPoll = await dbContext.Poll.GetById(Poll.Id);
                        trackedPoll.Votes.Add(voteObj);
                        await dbContext.SaveChangesAsync().ConfigureAwait(false);
                        Poll.Votes.Add(voteObj);
                        return (true, PollType.SingleAnswer);
                    }

                    case PollType.SingleAnswer:
                        return (false, PollType.SingleAnswer);

                    case PollType.AllowChange when voteCheck:
                    {

                        var trackedPoll = await dbContext.Poll.GetById(Poll.Id);
                        trackedPoll.Votes.Remove(trackedPoll.Votes.Find(x => x.UserId == user.Id));
                        trackedPoll.Votes.Add(voteObj);
                        Poll.Votes.Remove(Poll.Votes.Find(x => x.UserId == user.Id));
                        Poll.Votes.Add(voteObj);
                        await dbContext.SaveChangesAsync().ConfigureAwait(false);
                        return (true, PollType.AllowChange);
                    }

                    case PollType.AllowChange when !voteCheck:
                        return (false, PollType.AllowChange);

                    case PollType.MultiAnswer when !voteCheck:
                    {

                        var trackedPoll = await dbContext.Poll.GetById(Poll.Id);
                        trackedPoll.Votes.Remove(voteObj);
                        Poll.Votes.Remove(voteObj);
                        await dbContext.SaveChangesAsync().ConfigureAwait(false);
                        return (false, PollType.MultiAnswer);
                    }

                    case PollType.MultiAnswer when voteCheck:
                    {

                        var trackedPoll = await dbContext.Poll.GetById(Poll.Id);
                        trackedPoll.Votes.Add(voteObj);
                        Poll.Votes.Add(voteObj);
                        await dbContext.SaveChangesAsync().ConfigureAwait(false);
                        return (true, PollType.MultiAnswer);
                    }
                }
            }
            finally
            {
                locker.Release();
            }

            return (true, Poll.PollType);
        }
    }
}