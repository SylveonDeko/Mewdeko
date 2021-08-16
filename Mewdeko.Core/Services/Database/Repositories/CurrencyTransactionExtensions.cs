using System.Collections.Generic;
using System.Linq;
using Mewdeko.Core.Services.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Core.Services.Database
{
    public static class CurrencyTransactionExtensions
    {
        public static List<CurrencyTransaction> GetPageFor(this DbSet<CurrencyTransaction> set, ulong userId, int page)
        {
            return set.AsQueryable()
                .AsNoTracking()
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.DateAdded)
                .Skip(15 * page)
                .Take(15)
                .ToList();
        }
    }
}