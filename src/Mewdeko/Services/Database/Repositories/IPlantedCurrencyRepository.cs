using Mewdeko.Services.Database.Models;

namespace Mewdeko.Services.Database.Repositories;

public interface IPlantedCurrencyRepository : IRepository<PlantedCurrency>
{
    (long Sum, ulong[] MessageIds) RemoveSumAndGetMessageIdsFor(ulong cid, string pass);
    decimal GetTotalPlanted();
}