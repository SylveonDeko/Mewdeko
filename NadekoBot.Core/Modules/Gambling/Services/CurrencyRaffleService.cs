using System.Threading.Tasks;
using NadekoBot.Core.Services;
using NadekoBot.Core.Modules.Gambling.Common;
using System.Threading;
using System.Linq;
using System.Collections.Generic;
using Discord;
using System;

namespace NadekoBot.Core.Modules.Gambling.Services
{
    public class CurrencyRaffleService : INService
    {
        public enum JoinErrorType
        {
            NotEnoughCurrency,
            AlreadyJoinedOrInvalidAmount
        }
        private readonly SemaphoreSlim _locker = new SemaphoreSlim(1, 1);
        private readonly DbService _db;
        private readonly ICurrencyService _cs;

        public Dictionary<ulong, CurrencyRaffleGame> Games { get; } = new Dictionary<ulong, CurrencyRaffleGame>();

        public CurrencyRaffleService(DbService db, ICurrencyService cs)
        {
            _db = db;
            _cs = cs;
        }

        public async Task<(CurrencyRaffleGame, JoinErrorType?)> JoinOrCreateGame(ulong channelId, IUser user, long amount, bool mixed, Func<IUser, long, Task> onEnded)
        {
            await _locker.WaitAsync().ConfigureAwait(false);
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
                if (!await _cs.RemoveAsync(user.Id, "Currency Raffle Join", amount).ConfigureAwait(false))
                {
                    if (newGame)
                        Games.Remove(channelId);
                    return (null, JoinErrorType.NotEnoughCurrency);
                }

                if (!crg.AddUser(user, amount))
                {
                    await _cs.AddAsync(user.Id, "Curency Raffle Refund", amount).ConfigureAwait(false);
                    return (null, JoinErrorType.AlreadyJoinedOrInvalidAmount);
                }
                if (newGame)
                {
                    var _t = Task.Run(async () =>
                    {
                        await Task.Delay(60000).ConfigureAwait(false);
                        await _locker.WaitAsync().ConfigureAwait(false);
                        try
                        {
                            var winner = crg.GetWinner();
                            var won = crg.Users.Sum(x => x.Amount);

                            await _cs.AddAsync(winner.DiscordUser.Id, "Currency Raffle Win",
                                won).ConfigureAwait(false);
                            Games.Remove(channelId, out _);
                            var oe = onEnded(winner.DiscordUser, won);
                        }
                        catch { }
                        finally { _locker.Release(); }
                    });
                }
                return (crg, null);
            }
            finally
            {
                _locker.Release();
            }
        }
    }
}