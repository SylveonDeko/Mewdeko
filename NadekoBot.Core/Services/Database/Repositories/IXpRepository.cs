using NadekoBot.Core.Services.Database.Models;
using System.Collections.Generic;

namespace NadekoBot.Core.Services.Database.Repositories
{
    public interface IXpRepository : IRepository<UserXpStats>
    {
        UserXpStats GetOrCreateUser(ulong guildId, ulong userId);
        int GetUserGuildRanking(ulong userId, ulong guildId);
        List<UserXpStats> GetUsersFor(ulong guildId, int page);
        void ResetGuildUserXp(ulong userId, ulong guildId);
        void ResetGuildXp(ulong guildId);
        List<UserXpStats> GetTopUserXps(ulong guildId, int count);
    }
}
