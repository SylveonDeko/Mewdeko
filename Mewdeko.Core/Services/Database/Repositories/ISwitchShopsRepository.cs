using Mewdeko.Core.Services.Database.Models;

namespace Mewdeko.Core.Services.Database.Repositories
{
    public interface ISwitchShopsRepository : IRepository<SwitchShops>
    {
        SwitchShops[] ForGuild(ulong guildid);
        SwitchShops[] GetAll(ulong e = 0);
    }
}