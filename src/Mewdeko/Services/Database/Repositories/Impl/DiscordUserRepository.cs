using System.Collections.Generic;
using Discord;
using Mewdeko.Services.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Services.Database.Repositories.Impl;

public class DiscordUserRepository : Repository<DiscordUser>, IDiscordUserRepository
{
    public DiscordUserRepository(DbContext context) : base(context)
    {
    }

    public void EnsureCreated(ulong userId, string username, string discrim, string avatarId) =>
        Context.Database.ExecuteSqlInterpolated($@"
UPDATE OR IGNORE DiscordUser
SET Username={username},
    Discriminator={discrim},
    AvatarId={avatarId}
WHERE UserId={userId};

INSERT OR IGNORE INTO DiscordUser (UserId, Username, Discriminator, AvatarId)
VALUES ({userId}, {username}, {discrim}, {avatarId});
");

    //temp is only used in updatecurrencystate, so that i don't overwrite real usernames/discrims with Unknown
    public DiscordUser GetOrCreate(ulong userId, string username, string discrim, string avatarId)
    {
        EnsureCreated(userId, username, discrim, avatarId);
        return Set
            .Include(x => x.Club)
            .First(u => u.UserId == userId);
    }


    public DiscordUser GetOrCreate(IUser original) => GetOrCreate(original.Id, original.Username, original.Discriminator, original.AvatarId);

    public int GetUserGlobalRank(ulong id) =>
        //            @"SELECT COUNT(*) + 1
        //FROM DiscordUser
        //WHERE TotalXp > COALESCE((SELECT TotalXp
        //    FROM DiscordUser
        //    WHERE UserId = @p1
        //    LIMIT 1), 0);"
        Set
            .AsQueryable()
            .Count(x => x.TotalXp > Set
                                    .AsQueryable()
                                    .Where(y => y.UserId == id)
                                    .Select(y => y.TotalXp)
                                    .FirstOrDefault()) + 1;

    public DiscordUser[] GetUsersXpLeaderboardFor(int page) =>
        Set.AsQueryable()
            .OrderByDescending(x => x.TotalXp)
            .Skip(page * 9)
            .Take(9)
            .AsEnumerable()
            .ToArray();

    public List<DiscordUser> GetTopRichest(ulong botId, int count, int page = 0) =>
        Set.AsQueryable()
            .Where(c => c.CurrencyAmount > 0 && botId != c.UserId)
            .OrderByDescending(c => c.CurrencyAmount)
            .Skip(page * 9)
            .Take(count)
            .ToList();

    public List<DiscordUser> GetTopRichest(ulong botId, int count) =>
        Set.AsQueryable()
            .Where(c => c.CurrencyAmount > 0 && botId != c.UserId)
            .OrderByDescending(c => c.CurrencyAmount)
            .Take(count)
            .ToList();

    public long GetUserCurrency(ulong userId) =>
        Set.AsNoTracking()
            .FirstOrDefault(x => x.UserId == userId)
            ?.CurrencyAmount ?? 0;

    public void RemoveFromMany(IEnumerable<ulong> ids)
    {
        var items = Set.AsQueryable().Where(x => ids.Contains(x.UserId));
        foreach (var item in items) item.CurrencyAmount = 0;
    }

    public bool TryUpdateCurrencyState(ulong userId, string name, string discrim, string avatarId, long amount,
        bool allowNegative = false)
    {
        if (amount == 0)
            return true;

        // if remove - try to remove if he has more or equal than the amount
        // and return number of rows > 0 (was there a change)
        if (amount < 0 && !allowNegative)
        {
            var rows = Context.Database.ExecuteSqlInterpolated($@"
UPDATE DiscordUser
SET CurrencyAmount=CurrencyAmount+{amount}
WHERE UserId={userId} AND CurrencyAmount>={-amount};");
            return rows > 0;
        }

        // if remove and negative is allowed, just remove without any condition
        if (amount < 0 && allowNegative)
        {
            var rows = Context.Database.ExecuteSqlInterpolated($@"
UPDATE DiscordUser
SET CurrencyAmount=CurrencyAmount+{amount}
WHERE UserId={userId};");
            return rows > 0;
        }

        // if add - create a new user with default values if it doesn't exist
        // if it exists, sum current amount with the new one, if it doesn't
        // he just has the new amount
        var updatedUserData = !string.IsNullOrWhiteSpace(name);
        name ??= "Unknown";
        discrim ??= "????";
        avatarId ??= "";

        // just update the amount, there is no new user data
        if (!updatedUserData)
            Context.Database.ExecuteSqlInterpolated($@"
UPDATE OR IGNORE DiscordUser
SET CurrencyAmount=CurrencyAmount+{amount}
WHERE UserId={userId};

INSERT OR IGNORE INTO DiscordUser (UserId, Username, Discriminator, AvatarId, CurrencyAmount)
VALUES ({userId}, {name}, {discrim}, {avatarId}, {amount});
");
        else
            Context.Database.ExecuteSqlInterpolated($@"
UPDATE OR IGNORE DiscordUser
SET CurrencyAmount=CurrencyAmount+{amount},
    Username={name},
    Discriminator={discrim},
    AvatarId={avatarId}
WHERE UserId={userId};

INSERT OR IGNORE INTO DiscordUser (UserId, Username, Discriminator, AvatarId, CurrencyAmount)
VALUES ({userId}, {name}, {discrim}, {avatarId}, {amount});
");
        return true;
    }

    public decimal GetTotalCurrency() =>
        Set
            .Sum(x => x.CurrencyAmount);

    public decimal GetTopOnePercentCurrency(ulong botId) =>
        Set.AsQueryable()
            .Where(x => x.UserId != botId)
            .OrderByDescending(x => x.CurrencyAmount)
            .Take(Set.Count() / 100 == 0 ? 1 : Set.Count() / 100)
            .Sum(x => x.CurrencyAmount);
}