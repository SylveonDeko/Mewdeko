using System.Collections.Generic;
using System.Linq;
using Mewdeko.Core.Services.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Core.Services.Database.Repositories.Impl
{
    public class CurrencyTransactionsRepository : Repository<CurrencyTransaction>, ICurrencyTransactionsRepository
    {
        public CurrencyTransactionsRepository(DbContext context) : base(context)
        {
        }

        public List<CurrencyTransaction> GetPageFor(ulong userId, int page)
        {
            return _set.AsQueryable()
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.DateAdded)
                .Skip(15 * page)
                .Take(15)
                .ToList();
        }

        public List<CurrencyTransaction> GetAllFor(ulong userId)
        {
            return _set.AsQueryable()
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.DateAdded)
                .ToList();
        }
    }
}