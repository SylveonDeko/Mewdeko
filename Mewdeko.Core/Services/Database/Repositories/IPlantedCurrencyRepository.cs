using Mewdeko.Core.Services.Database.Models;

namespace Mewdeko.Core.Services.Database.Repositories
{
    public interface IPlantedCurrencyRepository : IRepository<PlantedCurrency>
    {
        (long Sum, ulong[] MessageIds) RemoveSumAndGetMessageIdsFor(ulong cid, string pass);
        decimal GetTotalPlanted();
    }
}