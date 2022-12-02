using Mewdeko.Modules.Gambling.Common.Blackjack;

namespace Mewdeko.Modules.Gambling.Services;

public class BlackJackService : INService
{
    public ConcurrentDictionary<ulong, Blackjack> Games { get; } = new();
}