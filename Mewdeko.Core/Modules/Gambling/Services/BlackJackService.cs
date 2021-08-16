using System.Collections.Concurrent;
using Mewdeko.Core.Modules.Gambling.Common.Blackjack;
using Mewdeko.Core.Services;

namespace Mewdeko.Core.Modules.Gambling.Services
{
    public class BlackJackService : INService
    {
        public ConcurrentDictionary<ulong, Blackjack> Games { get; } = new();
    }
}