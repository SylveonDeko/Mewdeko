using System.Collections.Generic;
using Mewdeko.Core.Services.Database.Models;

namespace Mewdeko.Core.Services.Database.Repositories
{
    public interface ICurrencyTransactionsRepository : IRepository<CurrencyTransaction>
    {
        List<CurrencyTransaction> GetPageFor(ulong userId, int page);
        List<CurrencyTransaction> GetAllFor(ulong userId);
    }
}
