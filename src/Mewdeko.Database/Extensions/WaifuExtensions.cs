using LinqToDB.EntityFrameworkCore;
using Mewdeko.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Database.Extensions;

public class WaifuInfoStats
{
    public string FullName { get; set; }
    public int Price { get; set; }
    public string ClaimerName { get; set; }
    public string AffinityName { get; set; }
    public int AffinityCount { get; set; }
    public int DivorceCount { get; set; }
    public int ClaimCount { get; set; }
    public List<WaifuItem> Items { get; set; }
    public List<string> Claims30 { get; set; }
}

public static class WaifuExtensions
{
    public static async Task<WaifuInfo> ByWaifuUserId(this DbSet<WaifuInfo> waifus, ulong userId, Func<DbSet<WaifuInfo>, IQueryable<WaifuInfo>> includes = null)
    {
        if (includes is null)
        {
            return await waifus.Include(wi => wi.Waifu)
                .Include(wi => wi.Affinity)
                .Include(wi => wi.Claimer)
                .Include(wi => wi.Items)
                .FirstOrDefaultAsyncEF(wi => wi.Waifu.UserId == userId);
        }

        return await includes(waifus)
            .AsQueryable()
            .FirstOrDefaultAsyncEF(wi => wi.Waifu.UserId == userId);
    }

    public static async Task<IEnumerable<WaifuLbResult>> GetTop(this DbSet<WaifuInfo> waifus, int count, int skip = 0)
    {
        return count switch
        {
            < 0 => throw new ArgumentOutOfRangeException(nameof(count)),
            0 => new List<WaifuLbResult>(),
            _ => await waifus.Include(wi => wi.Waifu)
                .Include(wi => wi.Affinity)
                .Include(wi => wi.Claimer)
                .OrderByDescending(wi => wi.Price)
                .Skip(skip)
                .Take(count)
                .Select(x => new WaifuLbResult
                {
                    Affinity = x.Affinity == null ? null : x.Affinity.Username,
                    AffinityDiscrim = x.Affinity == null ? null : x.Affinity.Discriminator,
                    Claimer = x.Claimer == null ? null : x.Claimer.Username,
                    ClaimerDiscrim = x.Claimer == null ? null : x.Claimer.Discriminator,
                    Username = x.Waifu.Username,
                    Discrim = x.Waifu.Discriminator,
                    Price = x.Price
                })
                .ToListAsyncLinqToDB()
        };
    }

    public static async Task<decimal> GetTotalValue(this DbSet<WaifuInfo> waifus) =>
        await waifus
            .AsQueryable()
            .Where(x => x.ClaimerId != null)
            .SumAsyncLinqToDB(x => x.Price);

    public static async Task<ulong> GetWaifuUserId(this DbSet<WaifuInfo> waifus, ulong ownerId, string name) =>
        await waifus
            .AsQueryable()
            .AsNoTracking()
            .Where(x => x.Claimer.UserId == ownerId
                        && $"{x.Waifu.Username}#{x.Waifu.Discriminator}" == name)
            .Select(x => x.Waifu.UserId)
            .FirstOrDefaultAsyncLinqToDB();

    public static async Task<WaifuInfoStats> GetWaifuInfo(this MewdekoContext ctx, ulong userId)
    {
        await ctx.Database.ExecuteSqlInterpolatedAsync($@"
INSERT OR IGNORE INTO WaifuInfo (AffinityId, ClaimerId, Price, WaifuId)
VALUES ({null}, {null}, {1}, (SELECT Id FROM DiscordUser WHERE UserId={userId}));");

        var toReturn = await ctx.WaifuInfo.AsQueryable()
            .Where(w => w.WaifuId == ctx.DiscordUser
                .AsQueryable()
                .Where(u => u.UserId == userId)
                .Select(u => u.Id).FirstOrDefault())
            .Select(w => new WaifuInfoStats
            {
                FullName = ctx.DiscordUser
                    .AsQueryable()
                    .Where(u => u.UserId == userId)
                    .Select(u => $"{u.Username}#{u.Discriminator}")
                    .FirstOrDefault(),
                AffinityCount = ctx.WaifuUpdates
                    .AsQueryable()
                    .Count(x => x.UserId == w.WaifuId &&
                                x.UpdateType == WaifuUpdateType.AffinityChanged &&
                                x.NewId != null),
                AffinityName = ctx.DiscordUser
                    .AsQueryable()
                    .Where(u => u.Id == w.AffinityId)
                    .Select(u => $"{u.Username}#{u.Discriminator}")
                    .FirstOrDefault(),
                ClaimCount = ctx.WaifuInfo
                    .AsQueryable()
                    .Count(x => x.ClaimerId == w.WaifuId),
                ClaimerName = ctx.DiscordUser
                    .AsQueryable()
                    .Where(u => u.Id == w.ClaimerId)
                    .Select(u => $"{u.Username}#{u.Discriminator}")
                    .FirstOrDefault(),
                DivorceCount = ctx.WaifuUpdates
                    .AsQueryable()
                    .Count(x => x.OldId == w.WaifuId &&
                                x.NewId == null &&
                                x.UpdateType == WaifuUpdateType.Claimed),
                Price = w.Price,
                Claims30 = ctx.WaifuInfo
                    .AsQueryable()
                    .Include(x => x.Waifu)
                    .Where(x => x.ClaimerId == w.WaifuId)
                    .Select(x => $"{x.Waifu.Username}#{x.Waifu.Discriminator}")
                    .ToList(),
                Items = w.Items
            })
            .FirstOrDefaultAsyncLinqToDB();

        if (toReturn is null)
            return null;

        toReturn.Claims30 = toReturn.Claims30 is null
            ? new List<string>()
            : toReturn.Claims30.OrderBy(_ => Guid.NewGuid()).Take(30).ToList();

        return toReturn;
    }
}