using System.Threading;
using Discord;
using Mewdeko.Database.Extensions;
using Mewdeko.Database.Models;

namespace Mewdeko.Modules.Games.Common;

public class PollRunner
{
    private readonly DbService _db;

    private readonly SemaphoreSlim _locker = new(1, 1);

    public PollRunner(DbService db, Poll poll)
    {
        _db = db;
        Poll = poll;
    }

    public Poll Poll { get; }

    public event Func<IUserMessage, IGuildUser, Task> OnVoted;

    public async Task<bool> TryVote(IUserMessage msg)
    {
        PollVote voteObj;
        await _locker.WaitAsync().ConfigureAwait(false);
        try
        {
            // has to be a user message
            // channel must be the same the poll started in
            if (msg == null || msg.Author.IsBot || msg.Channel.Id != Poll.ChannelId)
                return false;

            // has to be an integer
            if (!int.TryParse(msg.Content, out var vote))
                return false;
            --vote;
            if (vote < 0 || vote >= Poll.Answers.Count)
                return false;

            var usr = msg.Author as IGuildUser;
            if (usr == null)
                return false;

            voteObj = new PollVote
            {
                UserId = msg.Author.Id,
                VoteIndex = vote
            };
            if (!Poll.Votes.Add(voteObj))
                return false;

            var _ = OnVoted?.Invoke(msg, usr);
        }
        finally
        {
            _locker.Release();
        }

        await using var uow = _db.GetDbContext();
        var trackedPoll = uow.Poll.GetById(Poll.Id);
        trackedPoll.Votes.Add(voteObj);
        await uow.SaveChangesAsync();

        return true;
    }

    public void End() => OnVoted = null;
}