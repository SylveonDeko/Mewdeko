using System.Threading.Tasks;
using Mewdeko.Modules.Gambling.Services;

namespace Mewdeko.Services.Impl;

public class CurrencyService : ICurrencyService
{
    private readonly IUser bot;
    private readonly DbService db;
    private readonly GamblingConfigService gss;

    public CurrencyService(DbService db, DiscordSocketClient c,
        GamblingConfigService gss)
    {
        this.db = db;
        this.gss = gss;
        bot = c.CurrentUser;
    }

    public Task AddAsync(ulong userId, string reason, long amount, bool gamble = false) => InternalAddAsync(userId, null, null, null, reason, amount, gamble);

    public async Task AddAsync(IUser user, string reason, long amount, bool sendMessage = false,
        bool gamble = false)
    {
        await InternalAddAsync(user.Id, user.Username, user.Discriminator, user.AvatarId, reason, amount, gamble);
        if (sendMessage)
        {
            try
            {
                await (await user.CreateDMChannelAsync())
                    .EmbedAsync(new EmbedBuilder()
                        .WithOkColor()
                        .WithTitle("Received Currency")
                        .AddField("Amount", amount + gss.Data.Currency.Sign)
                        .AddField("Reason", reason));
            }
            catch
            {
                // ignored
            }
        }
    }

    public async Task AddBulkAsync(IEnumerable<ulong> userIds, IEnumerable<string> reasons,
        IEnumerable<long> amounts, bool gamble = false)
    {
        var idArray = userIds as ulong[] ?? userIds.ToArray();
        var reasonArray = reasons as string[] ?? reasons.ToArray();
        var amountArray = amounts as long[] ?? amounts.ToArray();

        if (idArray.Length != reasonArray.Length || reasonArray.Length != amountArray.Length)
            throw new ArgumentException("Cannot perform bulk operation. Arrays are not of equal length.");

        var userIdHashSet = new HashSet<ulong>(idArray.Length);
        await using var uow = db.GetDbContext();
        for (var i = 0; i < idArray.Length; i++)
        {
            // i have to prevent same user changing more than once as it will cause db error
            if (userIdHashSet.Add(idArray[i]))
                InternalChange(idArray[i], null, null, null, reasonArray[i], amountArray[i], gamble, uow);
        }

        await uow.SaveChangesAsync().ConfigureAwait(false);
    }

    public Task<bool> RemoveAsync(ulong userId, string reason, long amount, bool gamble = false) => InternalRemoveAsync(userId, null, null, null, reason, amount, gamble);

    public Task<bool> RemoveAsync(IUser user, string reason, long amount, bool sendMessage = false,
        bool gamble = false) =>
        InternalRemoveAsync(user.Id, user.Username, user.Discriminator, user.AvatarId, reason, amount,
            gamble);

    private static CurrencyTransaction GetCurrencyTransaction(ulong userId, string? reason, long amount) =>
        new()
        {
            Amount = amount, UserId = userId, Reason = reason ?? "-"
        };

    private bool InternalChange(ulong userId, string userName, string discrim, string avatar,
        string reason, long amount, bool gamble, MewdekoContext uow)
    {
        var result = uow.TryUpdateCurrencyState(userId, userName, discrim, avatar, amount);
        if (!result) return result;
        var t = GetCurrencyTransaction(userId, reason, amount);
        uow.CurrencyTransactions.Add(t);

        if (!gamble) return result;
        var t2 = GetCurrencyTransaction(bot.Id, reason, -amount);
        uow.CurrencyTransactions.Add(t2);
        uow.TryUpdateCurrencyState(bot.Id, bot.Username, bot.Discriminator, bot.AvatarId,
            -amount, true);

        return result;
    }

    private async Task InternalAddAsync(ulong userId, string userName, string discrim, string avatar, string reason,
        long amount, bool gamble)
    {
        if (amount < 0)
        {
            throw new ArgumentException("You can't add negative amounts. Use RemoveAsync method for that.",
                nameof(amount));
        }

        await using var uow = db.GetDbContext();
        InternalChange(userId, userName, discrim, avatar, reason, amount, gamble, uow);
        await uow.SaveChangesAsync().ConfigureAwait(false);
    }

    private async Task<bool> InternalRemoveAsync(ulong userId, string userName, string userDiscrim, string avatar,
        string reason, long amount, bool gamble = false)
    {
        if (amount < 0)
        {
            throw new ArgumentException("You can't remove negative amounts. Use AddAsync method for that.",
                nameof(amount));
        }

        await using var uow = db.GetDbContext();
        var result = InternalChange(userId, userName, userDiscrim, avatar, reason, -amount, gamble, uow);
        await uow.SaveChangesAsync().ConfigureAwait(false);
        return result;
    }
}