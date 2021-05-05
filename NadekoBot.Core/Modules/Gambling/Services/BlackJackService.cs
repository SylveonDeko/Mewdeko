using NadekoBot.Core.Modules.Gambling.Common.Blackjack;
using NadekoBot.Core.Services;
using System.Collections.Concurrent;

namespace NadekoBot.Core.Modules.Gambling.Services
{
    public class BlackJackService : INService
    {
        public ConcurrentDictionary<ulong, Blackjack> Games { get; } = new ConcurrentDictionary<ulong, Blackjack>();
    }
}
