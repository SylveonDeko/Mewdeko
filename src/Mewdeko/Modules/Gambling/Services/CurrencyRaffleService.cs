using System.Threading;
using System.Threading.Tasks;
using Mewdeko.Modules.Gambling.Common;

namespace Mewdeko.Modules.Gambling.Services;

public class CurrencyRaffleService : INService
{
    public enum JoinErrorType
    {
        NotEnoughCurrency,
        AlreadyJoinedOrInvalidAmount
    }

    private readonly ICurrencyService cs;
    private readonly SemaphoreSlim locker = new(1, 1);

    public CurrencyRaffleService(ICurrencyService cs) => this.cs = cs;

    public Dictionary<ulong, CurrencyRaffleGame> Games { get; } = new();

    public async Task<(CurrencyRaffleGame?, JoinErrorType?)> JoinOrCreateGame(ulong channelId, IUser user,
        long amount, bool mixed, Func<IUser, long, Task> onEnded)
    {
        await locker.WaitAsync().ConfigureAwait(false);
        try
        {
            var newGame = false;
            if (!Games.TryGetValue(channelId, out var crg))
            {
                newGame = true;
                crg = new CurrencyRaffleGame(mixed
                    ? CurrencyRaffleGame.Type.Mixed
                    : CurrencyRaffleGame.Type.Normal);
                Games.Add(channelId, crg);
            }

            //remove money, and stop the game if this
            // user created it and doesn't have the money
            if (!await cs.RemoveAsync(user.Id, "Currency Raffle Join", amount).ConfigureAwait(false))
            {
                if (newGame)
                    Games.Remove(channelId);
                return (null, JoinErrorType.NotEnoughCurrency);
            }

            if (!crg.AddUser(user, amount))
            {
                await cs.AddAsync(user.Id, "Curency Raffle Refund", amount).ConfigureAwait(false);
                return (null, JoinErrorType.AlreadyJoinedOrInvalidAmount);
            }

            if (newGame)
            {
                await Task.Run(async () =>
                {
                    await Task.Delay(60000).ConfigureAwait(false);
                    await locker.WaitAsync().ConfigureAwait(false);
                    try
                    {
                        var winner = crg.GetWinner();
                        var won = crg.Users.Sum(x => x.Amount);

                        await cs.AddAsync(winner.DiscordUser.Id, "Currency Raffle Win",
                            won).ConfigureAwait(false);
                        Games.Remove(channelId, out _);
                        await onEnded(winner.DiscordUser, won);
                    }
                    catch
                    {
                        // ignored
                    }
                    finally
                    {
                        locker.Release();
                    }
                });
            }

            return (crg, null);
        }
        finally
        {
            locker.Release();
        }
    }
}