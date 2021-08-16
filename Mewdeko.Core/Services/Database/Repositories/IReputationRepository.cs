using Mewdeko.Core.Services.Database.Models;

namespace Mewdeko.Core.Services.Database.Repositories
{
    public interface IReputationRepository : IRepository<Reputation>
    {
        Reputation[] ForUserId(ulong msgid);
        Reputation[] ForGuildId(ulong guildid);
        Reputation[] ForReviewerId(ulong guildid);
    }
}