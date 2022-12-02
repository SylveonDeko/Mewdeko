using System.Threading;
using System.Threading.Tasks;

namespace Mewdeko.Modules.Games.Common;

public class PollRunner
{
    private readonly DbService db;

    private readonly SemaphoreSlim locker = new(1, 1);

    public PollRunner(DbService db, Poll poll)
    {
        this.db = db;
        Poll = poll;
    }

    public Poll Poll { get; }

    public async Task<(bool allowed, PollType type)> TryVote(int num, IUser user)
    {
        PollVote voteObj;
        await locker.WaitAsync().ConfigureAwait(false);
        try
        {
            voteObj = new PollVote
            {
                UserId = user.Id, VoteIndex = num
            };
            var voteCheck = Poll.Votes.Find(x => x.UserId == user.Id && x.VoteIndex == num) == null;
            switch (Poll.PollType)
            {
                case PollType.SingleAnswer when !Poll.Votes.Contains(voteObj):
                {
                    await using var uow = db.GetDbContext();
                    var trackedPoll = await uow.Poll.GetById(Poll.Id);
                    trackedPoll.Votes.Add(voteObj);
                    await uow.SaveChangesAsync().ConfigureAwait(false);
                    Poll.Votes.Add(voteObj);
                    return (true, PollType.SingleAnswer);
                }

                case PollType.SingleAnswer:
                    return (false, PollType.SingleAnswer);

                case PollType.AllowChange when voteCheck:
                {
                    await using var uow = db.GetDbContext();
                    var trackedPoll = await uow.Poll.GetById(Poll.Id);
                    trackedPoll.Votes.Remove(trackedPoll.Votes.Find(x => x.UserId == user.Id));
                    trackedPoll.Votes.Add(voteObj);
                    Poll.Votes.Remove(Poll.Votes.Find(x => x.UserId == user.Id));
                    Poll.Votes.Add(voteObj);
                    await uow.SaveChangesAsync().ConfigureAwait(false);
                    return (true, PollType.AllowChange);
                }

                case PollType.AllowChange when !voteCheck:
                    return (false, PollType.AllowChange);

                case PollType.MultiAnswer when !voteCheck:
                {
                    await using var uow = db.GetDbContext();
                    var trackedPoll = await uow.Poll.GetById(Poll.Id);
                    trackedPoll.Votes.Remove(voteObj);
                    Poll.Votes.Remove(voteObj);
                    await uow.SaveChangesAsync().ConfigureAwait(false);
                    return (false, PollType.MultiAnswer);
                }

                case PollType.MultiAnswer when voteCheck:
                {
                    await using var uow = db.GetDbContext();
                    var trackedPoll = await uow.Poll.GetById(Poll.Id);
                    trackedPoll.Votes.Add(voteObj);
                    Poll.Votes.Add(voteObj);
                    await uow.SaveChangesAsync().ConfigureAwait(false);
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