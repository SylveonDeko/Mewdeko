using Discord;
using LinqToDB;
using LinqToDB.EntityFrameworkCore;
using Mewdeko.Database.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;

namespace Mewdeko.Database.Extensions;

public static class DiscordUserExtensions
{
    public static void EnsureUserCreated(this MewdekoContext ctx, ulong userId, string username, string discrim, string avatarId) =>
        ctx.DiscordUser
           .ToLinqToDBTable()
           .InsertOrUpdate(() => new DiscordUser
               {
                   UserId = userId,
                   Username = username,
                   Discriminator = discrim,
                   AvatarId = avatarId,
                   TotalXp = 0,
                   CurrencyAmount = 0
               },
               old => new DiscordUser
               {
                   Username = username,
                   Discriminator = discrim,
                   AvatarId = avatarId,
               }, () => new DiscordUser
               {
                   UserId = userId
               });

    //temp is only used in updatecurrencystate, so that i don't overwrite real usernames/discrims with Unknown
    public static DiscordUser GetOrCreateUser(this MewdekoContext ctx, ulong userId, string username, string discrim, string avatarId)
    {
        ctx.EnsureUserCreated(userId, username, discrim, avatarId);
        return ctx.DiscordUser
                  .Include(x => x.Club)
                  .First(u => u.UserId == userId);
    }

    public static DiscordUser GetOrCreateUser(this MewdekoContext ctx, IUser original)
        => ctx.GetOrCreateUser(original.Id, original.Username, original.Discriminator, original.AvatarId);

    public static int GetUserGlobalRank(this DbSet<DiscordUser> users, ulong id) =>
        users
            .AsQueryable()
            .Count(x => x.TotalXp > users
                                    .AsQueryable()
                                    .Where(y => y.UserId == id)
                                    .Select(y => y.TotalXp)
                                    .FirstOrDefault()) + 1;

    public static DiscordUser[] GetUsersXpLeaderboardFor(this DbSet<DiscordUser> users, int page) =>
        users.AsQueryable()
             .OrderByDescending(x => x.TotalXp)
             .Skip(page * 9)
             .Take(9)
             .AsEnumerable()
             .ToArray();

    public static List<DiscordUser> GetTopRichest(this DbSet<DiscordUser> users, ulong botId, int count, int page = 0) =>
        users.AsQueryable()
             .Where(c => c.CurrencyAmount > 0 && botId != c.UserId)
             .OrderByDescending(c => c.CurrencyAmount)
             .Skip(page * 9)
             .Take(count)
             .ToList();

    public static List<DiscordUser> GetTopRichest(this DbSet<DiscordUser> users, ulong botId, int count) =>
        users.AsQueryable()
             .Where(c => c.CurrencyAmount > 0 && botId != c.UserId)
             .OrderByDescending(c => c.CurrencyAmount)
             .Take(count)
             .ToList();

    public static long GetUserCurrency(this DbSet<DiscordUser> users, ulong userId) =>
        users.AsNoTracking()
             .FirstOrDefault(x => x.UserId == userId)
             ?.CurrencyAmount ?? 0;

    public static void RemoveFromMany(this DbSet<DiscordUser> users, IEnumerable<ulong> ids)
    {
        var items = users.AsQueryable().Where(x => ids.Contains(x.UserId));
        foreach (var item in items)
        {
            item.CurrencyAmount = 0;
        }
    }
    public static bool TryUpdateCurrencyState(this MewdekoContext ctx, ulong userId, string name, string discrim, string avatarId, long amount, bool allowNegative = false)
    {
        switch (amount)
        {
            case 0:
                return true;
            // if remove - try to remove if he has more or equal than the amount
            // and return number of rows > 0 (was there a change)
            case < 0 when !allowNegative:
                {
                    var rows = ctx.Database.ExecuteSqlInterpolated($@"
UPDATE DiscordUser
SET CurrencyAmount=CurrencyAmount+{amount}
WHERE UserId={userId} AND CurrencyAmount>={-amount};");
                    return rows > 0;
                }
            // if remove and negative is allowed, just remove without any condition
            case < 0 when allowNegative:
                {
                    var rows = ctx.Database.ExecuteSqlInterpolated($@"
UPDATE DiscordUser
SET CurrencyAmount=CurrencyAmount+{amount}
WHERE UserId={userId};");
                    return rows > 0;
                }
        }

        // if add - create a new user with default values if it doesn't exist
        // if it exists, sum current amount with the new one, if it doesn't
        // he just has the new amount
        var updatedUserData = !string.IsNullOrWhiteSpace(name);
        name = name ?? "Unknown";
        discrim = discrim ?? "????";
        avatarId = avatarId ?? "";

        // just update the amount, there is no new user data
        if (!updatedUserData)
        {
            var rows = ctx.Database.ExecuteSqlInterpolated($@"
UPDATE OR IGNORE DiscordUser
SET CurrencyAmount=CurrencyAmount+{amount}
WHERE UserId={userId};

INSERT OR IGNORE INTO DiscordUser (UserId, Username, Discriminator, AvatarId, CurrencyAmount, TotalXp)
VALUES ({userId}, {name}, {discrim}, {avatarId}, {amount}, 0);
");
                
        }
        else
        {
            ctx.Database.ExecuteSqlInterpolated($@"
UPDATE OR IGNORE DiscordUser
SET CurrencyAmount=CurrencyAmount+{amount},
    Username={name},
    Discriminator={discrim},
    AvatarId={avatarId}
WHERE UserId={userId};

INSERT OR IGNORE INTO DiscordUser (UserId, Username, Discriminator, AvatarId, CurrencyAmount, TotalXp)
VALUES ({userId}, {name}, {discrim}, {avatarId}, {amount}, 0);
");
        }
        return true;
    }

    public static decimal GetTotalCurrency(this DbSet<DiscordUser> users) =>
        users
            .Sum((Func<DiscordUser, decimal>)(x => x.CurrencyAmount));

    public static decimal GetTopOnePercentCurrency(this DbSet<DiscordUser> users, ulong botId) =>
        users.AsQueryable()
             .Where(x => x.UserId != botId)
             .OrderByDescending(x => x.CurrencyAmount)
             .Take(users.Count() / 100 == 0 ? 1 : users.Count() / 100)
             .Sum(x => x.CurrencyAmount);
}