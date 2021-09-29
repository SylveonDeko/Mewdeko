using Mewdeko.Services.Database.Models;

namespace Mewdeko.Services.Database.Repositories
{
    public interface IReputationRepository : IRepository<Reputation>
    {
        Reputation[] ForUserId(ulong msgid);
        Reputation[] ForGuildId(ulong guildid);
        Reputation[] ForReviewerId(ulong guildid);
    }
}