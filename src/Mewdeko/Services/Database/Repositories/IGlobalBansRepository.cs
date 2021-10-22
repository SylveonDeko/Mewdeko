using Mewdeko.Services.Database.Models;

namespace Mewdeko.Services.Database.Repositories
{
    public interface IGlobalBansRepository : IRepository<GlobalBans>
    {
        GlobalBans[] AllGlobalBans();
        GlobalBans[] GlobalBansByType(string type);
        GlobalBans[] GetGlobalBansAddedBy(ulong userid);
        GlobalBans[] GetGlobalBanById(int id);
        GlobalBans[] GetGlobalBanByUserId(ulong id);
    }
}