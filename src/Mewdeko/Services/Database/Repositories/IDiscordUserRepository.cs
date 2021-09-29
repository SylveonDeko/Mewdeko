using System.Collections.Generic;
using Discord;
using Mewdeko.Services.Database.Models;

namespace Mewdeko.Services.Database.Repositories
{
    public interface IDiscordUserRepository : IRepository<DiscordUser>
    {
        void EnsureCreated(ulong userId, string username, string discrim, string avatarId);
        DiscordUser GetOrCreate(ulong userId, string username, string discrim, string avatarId);
        DiscordUser GetOrCreate(IUser original);
        int GetUserGlobalRank(ulong id);
        DiscordUser[] GetUsersXpLeaderboardFor(int page);

        long GetUserCurrency(ulong userId);

        bool TryUpdateCurrencyState(ulong userId, string name, string discrim, string avatar, long change,
            bool allowNegative = false);

        List<DiscordUser> GetTopRichest(ulong botId, int count, int page);
        List<DiscordUser> GetTopRichest(ulong botId, int count);
        void RemoveFromMany(IEnumerable<ulong> ids);
        decimal GetTotalCurrency();
        decimal GetTopOnePercentCurrency(ulong botId);
    }
}