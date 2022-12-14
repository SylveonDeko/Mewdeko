using Mewdeko.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Database.Extensions;

public static class CurrencyTransactionExtensions
{
    public static List<CurrencyTransaction> GetPageFor(this DbSet<CurrencyTransaction> set, ulong userId, int page) =>
        set.AsQueryable()
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.DateAdded)
            .Skip(15 * page)
            .Take(15)
            .ToList();
}