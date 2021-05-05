using NadekoBot.Core.Services.Database.Models;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace NadekoBot.Core.Services.Database.Repositories.Impl
{
    public class PollsRepository : Repository<Poll>, IPollsRepository
    {
        public PollsRepository(DbContext context) : base(context)
        {
        }

        public IEnumerable<Poll> GetAllPolls()
        {
            return _set.Include(x => x.Answers)
                .Include(x => x.Votes)
                .ToArray();
        }

        public void RemovePoll(int id)
        {
            var p = _set
                .Include(x => x.Answers)
                .Include(x => x.Votes)
                .FirstOrDefault(x => x.Id == id);
            if (p.Votes != null)
            {
                _context.Set<PollVote>().RemoveRange(p.Votes);
                p.Votes.Clear();
            }
            if (p.Answers != null)
            {
                _context.Set<PollAnswer>().RemoveRange(p.Answers);
                p.Answers.Clear();
            }
            _set.Remove(p);
        }
    }
}
