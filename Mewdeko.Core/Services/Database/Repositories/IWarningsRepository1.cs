using System.Threading.Tasks;
using Mewdeko.Core.Services.Database.Models;

namespace Mewdeko.Core.Services.Database.Repositories
{
    public interface IWarningsRepository : IRepository<Warning>
    {
        Warning[] ForId(ulong guildId, ulong userId);
        Task ForgiveAll(ulong guildId, ulong userId, string moderator);
        bool Forgive(ulong guildId, ulong userId, string moderator, int index);
        Warning[] GetForGuild(ulong id);
    }
}