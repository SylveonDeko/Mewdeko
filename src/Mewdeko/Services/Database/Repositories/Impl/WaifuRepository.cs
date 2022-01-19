using System.Collections.Generic;
using Mewdeko.Services.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Services.Database.Repositories.Impl;

public class WaifuRepository : Repository<WaifuInfo>, IWaifuRepository
{
    public WaifuRepository(DbContext context) : base(context)
    {
    }

    public WaifuInfo ByWaifuUserId(ulong userId, Func<DbSet<WaifuInfo>, IQueryable<WaifuInfo>> includes = null)
    {
        if (includes == null)
            return Set.Include(wi => wi.Waifu)
                .Include(wi => wi.Affinity)
                .Include(wi => wi.Claimer)
                .Include(wi => wi.Items)
                .FirstOrDefault(wi => wi.WaifuId == Context.Set<DiscordUser>()
                    .AsQueryable()
                    .Where(x => x.UserId == userId)
                    .Select(x => x.Id)
                    .FirstOrDefault());

        return includes(Set)
            .AsQueryable()
            .FirstOrDefault(wi => wi.WaifuId == Context.Set<DiscordUser>()
                .AsQueryable()
                .Where(x => x.UserId == userId)
                .Select(x => x.Id)
                .FirstOrDefault());
    }

    public IEnumerable<string> GetWaifuNames(ulong userId)
    {
        var waifus = Set.AsQueryable().Where(x => x.ClaimerId != null &&
                                                   x.ClaimerId == Context.Set<DiscordUser>()
                                                       .AsQueryable()
                                                       .Where(y => y.UserId == userId)
                                                       .Select(y => y.Id)
                                                       .FirstOrDefault())
            .Select(x => x.WaifuId);

        return Context.Set<DiscordUser>()
            .AsQueryable()
            .Where(x => waifus.Contains(x.Id))
            .Select(x => x.Username + "#" + x.Discriminator)
            .ToList();
    }

    public IEnumerable<WaifuLbResult> GetTop(int count, int skip = 0)
    {
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count));
        if (count == 0)
            return new List<WaifuLbResult>();

        return Set.Include(wi => wi.Waifu)
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
            .ToList();
    }

    public decimal GetTotalValue() =>
        Set
            .AsQueryable()
            .Where(x => x.ClaimerId != null)
            .Sum(x => x.Price);

    public int AffinityCount(ulong userId) =>
        //return _context.Set<WaifuUpdate>()
        //           .Count(w => w.User.UserId == userId &&
        //               w.UpdateType == WaifuUpdateType.AffinityChanged &&
        //               w.New != null));
        Context.Set<WaifuUpdate>()
                .FromSqlInterpolated($@"SELECT 1 
FROM WaifuUpdates
WHERE UserId = (SELECT Id from DiscordUser WHERE UserId={userId}) AND 
    UpdateType = 0 AND 
    NewId IS NOT NULL")
                .Count();

    public WaifuInfoStats GetWaifuInfo(ulong userId)
    {
        Context.Database.ExecuteSqlInterpolated($@"
INSERT OR IGNORE INTO WaifuInfo (AffinityId, ClaimerId, Price, WaifuId)
VALUES ({null}, {null}, {1}, (SELECT Id FROM DiscordUser WHERE UserId={userId}));");

        var toReturn = Set.AsQueryable()
            .Where(w => w.WaifuId == Context.Set<DiscordUser>()
                .AsQueryable()
                .Where(u => u.UserId == userId)
                .Select(u => u.Id).FirstOrDefault())
            .Select(w => new WaifuInfoStats
            {
                FullName = Context.Set<DiscordUser>()
                    .AsQueryable()
                    .Where(u => u.UserId == userId)
                    .Select(u => u.Username + "#" + u.Discriminator)
                    .FirstOrDefault(),

                AffinityCount = Context
                    .Set<WaifuUpdate>()
                    .AsQueryable()
                    .Count(x => x.UserId == w.WaifuId &&
                                x.UpdateType == WaifuUpdateType.AffinityChanged &&
                                x.NewId != null),

                AffinityName = Context.Set<DiscordUser>()
                    .AsQueryable()
                    .Where(u => u.Id == w.AffinityId)
                    .Select(u => u.Username + "#" + u.Discriminator)
                    .FirstOrDefault(),

                ClaimCount = Set
                    .AsQueryable()
                    .Count(x => x.ClaimerId == w.WaifuId),

                ClaimerName = Context.Set<DiscordUser>()
                    .AsQueryable()
                    .Where(u => u.Id == w.ClaimerId)
                    .Select(u => u.Username + "#" + u.Discriminator)
                    .FirstOrDefault(),

                DivorceCount = Context
                    .Set<WaifuUpdate>()
                    .AsQueryable()
                    .Count(x => x.OldId == w.WaifuId &&
                                x.NewId == null &&
                                x.UpdateType == WaifuUpdateType.Claimed),

                Price = w.Price,

                Claims30 = Set
                    .AsQueryable()
                    .Include(x => x.Waifu)
                    .Where(x => x.ClaimerId == w.WaifuId)
                    .Select(x => x.Waifu.Username + "#" + x.Waifu.Discriminator)
                    .ToList(),

                Items = w.Items
            })
            .FirstOrDefault();

        if (toReturn is null)
            return null;

        toReturn.Claims30 = toReturn.Claims30 is null
            ? new List<string>()
            : toReturn.Claims30.OrderBy(_ => Guid.NewGuid()).Take(30).ToList();

        return toReturn;
    }
}