using System.Collections.Concurrent;
using Mewdeko.Modules.Gambling.Common.Blackjack;
using Mewdeko.Services;

namespace Mewdeko.Modules.Gambling.Services
{
    public class BlackJackService : INService
    {
        public ConcurrentDictionary<ulong, Blackjack> Games { get; } = new();
    }
}