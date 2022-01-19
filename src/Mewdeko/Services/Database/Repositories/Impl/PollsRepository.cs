using System.Collections.Generic;
using Mewdeko.Services.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Services.Database.Repositories.Impl;

public class PollsRepository : Repository<Poll>, IPollsRepository
{
    public PollsRepository(DbContext context) : base(context)
    {
    }

    public IEnumerable<Poll> GetAllPolls() =>
        Set.Include(x => x.Answers)
            .Include(x => x.Votes)
            .ToArray();

    public void RemovePoll(int id)
    {
        var p = Set
            .Include(x => x.Answers)
            .Include(x => x.Votes)
            .FirstOrDefault(x => x.Id == id);
        if (p.Votes != null)
        {
            Context.Set<PollVote>().RemoveRange(p.Votes);
            p.Votes.Clear();
        }

        if (p.Answers != null)
        {
            Context.Set<PollAnswer>().RemoveRange(p.Answers);
            p.Answers.Clear();
        }

        Set.Remove(p);
    }
}