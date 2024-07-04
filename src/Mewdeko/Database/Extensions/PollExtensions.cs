using LinqToDB.EntityFrameworkCore;
using Mewdeko.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Database.Extensions;

/// <summary>
/// Provides extension methods for querying and manipulating Poll entities.
/// </summary>
public static class PollExtensions
{
    /// <summary>
    /// Retrieves all Poll entities including their associated Answers and Votes.
    /// </summary>
    /// <param name="set">The DbSet of Poll entities to query.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains an IEnumerable of Poll entities.</returns>
    public async static Task<IEnumerable<Polls>> GetAllPolls(this DbSet<Polls> set) =>
        await set.Include(x => x.Answers)
            .Include(x => x.Votes)
            .ToArrayAsyncEF();

    /// <summary>
    /// Removes a Poll entity and its associated Answers and Votes from the database.
    /// </summary>
    /// <param name="ctx">The MewdekoContext to perform the removal operation.</param>
    /// <param name="id">The ID of the Poll to remove.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public static async Task RemovePoll(this MewdekoContext ctx, int id)
    {
        var p = await ctx
            .Poll
            .Include(x => x.Answers)
            .Include(x => x.Votes)
            .FirstOrDefaultAsyncEF(x => x.Id == id);
        if (p?.Votes != null)
        {
            ctx.RemoveRange(p.Votes);
            p.Votes.Clear();
        }

        if (p?.Answers != null)
        {
            ctx.RemoveRange(p.Answers);
            p.Answers.Clear();
        }

        ctx.Remove(p!);
    }
}