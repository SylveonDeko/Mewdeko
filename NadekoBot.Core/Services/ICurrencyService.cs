using Discord;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NadekoBot.Core.Services
{
    public interface ICurrencyService : INService
    {
        Task AddAsync(ulong userId, string reason, long amount, bool gamble = false);
        Task AddAsync(IUser user, string reason, long amount, bool sendMessage = false, bool gamble = false);
        Task AddBulkAsync(IEnumerable<ulong> userIds, IEnumerable<string> reasons, IEnumerable<long> amounts,  bool gamble = false);
        Task<bool> RemoveAsync(ulong userId, string reason, long amount, bool gamble = false);
        Task<bool> RemoveAsync(IUser userId, string reason, long amount, bool sendMessage = false, bool gamble = false);
    }
}
