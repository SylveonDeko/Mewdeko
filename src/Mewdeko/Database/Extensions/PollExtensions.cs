using Mewdeko.Database.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;

namespace Mewdeko.Database.Extensions;

public static class PollExtensions
{
    public static IEnumerable<Poll> GetAllPolls(this DbSet<Poll> set) =>
        set.Include(x => x.Answers)
                      .Include(x => x.Votes)
                      .ToArray();

    public static void RemovePoll(this MewdekoContext ctx, int id)
    {
        var p = ctx
                .Poll
                              .Include(x => x.Answers)
                              .Include(x => x.Votes)
                              .FirstOrDefault(x => x.Id == id);
        if (p.Votes != null)
        {
            ctx.RemoveRange(p.Votes);
            p.Votes.Clear();
        }

        if (p.Answers != null)
        {
            ctx.RemoveRange(p.Answers);
            p.Answers.Clear();
        }

        ctx.Remove(p);
    }
}