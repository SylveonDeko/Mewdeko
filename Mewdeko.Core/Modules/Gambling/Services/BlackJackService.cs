using Mewdeko.Core.Modules.Gambling.Common.Blackjack;
using Mewdeko.Core.Services;
using System.Collections.Concurrent;

namespace Mewdeko.Core.Modules.Gambling.Services
{
    public class BlackJackService : INService
    {
        public ConcurrentDictionary<ulong, Blackjack> Games { get; } = new ConcurrentDictionary<ulong, Blackjack>();
    }
}
