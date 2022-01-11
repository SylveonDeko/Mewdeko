using Mewdeko.Services.Database.Models;

namespace Mewdeko.Services.Database.Repositories;

public interface IMultiGreetRepository : IRepository<MultiGreet>
{
    public MultiGreet[] GetAllGreets(ulong guildId);
    public MultiGreet[] GetForChannel(ulong channelId);
}