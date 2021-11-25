using System.Linq;
using Mewdeko.Services.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Services.Database.Repositories.Impl
{
    public class GlobalBansRepository : Repository<GlobalBans>, IGlobalBansRepository
    {
        public GlobalBansRepository(DbContext context) : base(context)
        {
        }

        public GlobalBans[] AllGlobalBans()
        {
            return _set.AsQueryable().ToArray();
        }
        public GlobalBans[] GlobalBansByType(string type)
        {
            return _set.AsQueryable().Where(x => x.Type == type).ToArray();
        }
        public GlobalBans[] GetGlobalBansAddedBy(ulong uid)
        {
            return _set.AsQueryable().Where(x => x.AddedBy == uid).ToArray();
        }
        public GlobalBans[] GetGlobalBanById(int id)
        {
            return _set.AsQueryable().Where(x => x.Id == id).ToArray();
        }
        public GlobalBans[] GetGlobalBanByUserId(ulong uid)
        {
            return _set.AsQueryable().Where(x => x.UserId == uid).ToArray();
        }
    }
}