using Discord;
using Discord.WebSocket;
using Mewdeko.Core.Services.Database;
using Mewdeko.Core.Services.Database.Models;
using Mewdeko.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Mewdeko.Core.Services
{
    public class CurrencyService : ICurrencyService
    {
        private readonly IBotConfigProvider _config;
        private readonly DbService _db;
        private readonly IUser _bot;

        public CurrencyService(IBotConfigProvider config, DbService db, DiscordSocketClient c)
        {
            _config = config;
            _db = db;
            _bot = c.CurrentUser;
        }

        private CurrencyTransaction GetCurrencyTransaction(ulong userId, string reason, long amount) =>
            new CurrencyTransaction
            {
                Amount = amount,
                UserId = userId,
                Reason = reason ?? "-",
            };

        private bool InternalChange(ulong userId, string userName, string discrim, string avatar,
            string reason, long amount, bool gamble, IUnitOfWork uow)
        {
            var result = uow.DiscordUsers.TryUpdateCurrencyState(userId, userName, discrim, avatar, amount);
            if (result)
            {
                var t = GetCurrencyTransaction(userId, reason, amount);
                uow._context.CurrencyTransactions.Add(t);

                if (gamble)
                {
                    var t2 = GetCurrencyTransaction(_bot.Id, reason, -amount);
                    uow._context.CurrencyTransactions.Add(t2);
                    uow.DiscordUsers.TryUpdateCurrencyState(_bot.Id, _bot.Username, _bot.Discriminator, _bot.AvatarId, -amount, true);
                }
            }
            return result;
        }

        private async Task InternalAddAsync(ulong userId, string userName, string discrim, string avatar, string reason, long amount, bool gamble)
        {
            if (amount < 0)
            {
                throw new ArgumentException("You can't add negative amounts. Use RemoveAsync method for that.", nameof(amount));
            }

            using (var uow = _db.GetDbContext())
            {
                InternalChange(userId, userName, discrim, avatar, reason, amount, gamble, uow);
                await uow.SaveChangesAsync();
            }
        }

        public Task AddAsync(ulong userId, string reason, long amount, bool gamble = false)
        {
            return InternalAddAsync(userId, null, null, null, reason, amount, gamble);
        }

        public async Task AddAsync(IUser user, string reason, long amount, bool sendMessage = false, bool gamble = false)
        {
            await InternalAddAsync(user.Id, user.Username, user.Discriminator, user.AvatarId, reason, amount, gamble);
            if (sendMessage)
            {
                try
                {
                    await (await user.GetOrCreateDMChannelAsync())
                        .EmbedAsync(new EmbedBuilder()
                            .WithOkColor()
                            .WithTitle($"Received {_config.BotConfig.CurrencySign}")
                            .AddField("Amount", amount)
                            .AddField("Reason", reason));
                }
                catch
                {
                    // ignored
                }
            }
        }

        public async Task AddBulkAsync(IEnumerable<ulong> userIds, IEnumerable<string> reasons, IEnumerable<long> amounts, bool gamble = false)
        {
            ulong[] idArray = userIds as ulong[] ?? userIds.ToArray();
            string[] reasonArray = reasons as string[] ?? reasons.ToArray();
            long[] amountArray = amounts as long[] ?? amounts.ToArray();

            if (idArray.Length != reasonArray.Length || reasonArray.Length != amountArray.Length)
                throw new ArgumentException("Cannot perform bulk operation. Arrays are not of equal length.");

            var userIdHashSet = new HashSet<ulong>(idArray.Length);
            using (var uow = _db.GetDbContext())
            {
                for (int i = 0; i < idArray.Length; i++)
                {
                    // i have to prevent same user changing more than once as it will cause db error
                    if (userIdHashSet.Add(idArray[i]))
                        InternalChange(idArray[i], null, null, null, reasonArray[i], amountArray[i], gamble, uow);
                }
                await uow.SaveChangesAsync();
            }
        }

        private async Task<bool> InternalRemoveAsync(ulong userId, string userName, string userDiscrim, string avatar, string reason, long amount, bool gamble = false)
        {
            if (amount < 0)
            {
                throw new ArgumentException("You can't remove negative amounts. Use AddAsync method for that.", nameof(amount));
            }

            bool result;
            using (var uow = _db.GetDbContext())
            {
                result = InternalChange(userId, userName, userDiscrim, avatar, reason, -amount, gamble, uow);
                await uow.SaveChangesAsync();
            }
            return result;
        }

        public Task<bool> RemoveAsync(ulong userId, string reason, long amount, bool gamble = false)
        {
            return InternalRemoveAsync(userId, null, null, null, reason, amount, gamble);
        }

        public Task<bool> RemoveAsync(IUser user, string reason, long amount, bool sendMessage = false, bool gamble = false)
        {
            return InternalRemoveAsync(user.Id, user.Username, user.Discriminator, user.AvatarId, reason, amount, gamble);
        }
    }
}
