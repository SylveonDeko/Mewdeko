using System.Threading.Tasks;
using Mewdeko.Modules.Gambling.Common;
using Mewdeko.Modules.Gambling.Common.Waifu;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Modules.Gambling.Services;

public class WaifuService : INService
{
    private readonly IDataCache cache;
    private readonly ICurrencyService cs;

    private readonly DbService db;
    private readonly GamblingConfigService gss;

    public WaifuService(DbService db, ICurrencyService cs, IDataCache cache,
        GamblingConfigService gss)
    {
        this.db = db;
        this.cs = cs;
        this.cache = cache;
        this.gss = gss;
    }

    public async Task<bool> WaifuTransfer(IUser owner, ulong waifuId, IUser newOwner)
    {
        if (owner.Id == newOwner.Id || waifuId == newOwner.Id)
            return false;

        var settings = gss.Data;

        await using var uow = db.GetDbContext();
        var waifu = await uow.WaifuInfo.ByWaifuUserId(waifuId);
        var ownerUser = await uow.GetOrCreateUser(owner);

        // owner has to be the owner of the waifu
        if (waifu == null || waifu.ClaimerId != ownerUser.Id)
            return false;

        // if waifu likes the person, gotta pay the penalty
        if (waifu.AffinityId == ownerUser.Id)
        {
            if (!await cs.RemoveAsync(owner.Id,
                    "Waifu Transfer - affinity penalty",
                    (int)(waifu.Price * 0.6),
                    true))
            {
                // unable to pay 60% penalty
                return false;
            }

            waifu.Price = (int)(waifu.Price * 0.7); // half of 60% = 30% price reduction
            if (waifu.Price < settings.Waifu.MinPrice)
                waifu.Price = settings.Waifu.MinPrice;
        }
        else // if not, pay 10% fee
        {
            if (!await cs.RemoveAsync(owner.Id, "Waifu Transfer", waifu.Price / 10, true)) return false;

            waifu.Price = (int)(waifu.Price * 0.95); // half of 10% = 5% price reduction
            if (waifu.Price < settings.Waifu.MinPrice)
                waifu.Price = settings.Waifu.MinPrice;
        }

        //new claimerId is the id of the new owner
        var newOwnerUser = uow.GetOrCreateUser(newOwner);
        waifu.ClaimerId = newOwnerUser.Id;

        await uow.SaveChangesAsync().ConfigureAwait(false);

        return true;
    }

    public async Task<int> GetResetPrice(IUser user)
    {
        var settings = gss.Data;
        await using var uow = db.GetDbContext();
        var waifu = await uow.WaifuInfo.ByWaifuUserId(user.Id);

        if (waifu == null)
            return settings.Waifu.MinPrice;

        var divorces = uow.WaifuUpdates.Count(x => x.Old != null &&
                                                   x.Old.UserId == user.Id &&
                                                   x.UpdateType == WaifuUpdateType.Claimed &&
                                                   x.New == null);
        var affs = uow.WaifuUpdates
            .AsQueryable()
            .Where(w => w.User.UserId == user.Id && w.UpdateType == WaifuUpdateType.AffinityChanged &&
                        w.New != null)
            .ToList()
            .GroupBy(x => x.New)
            .Count();

        return (int)Math.Ceiling(waifu.Price * 1.25f) +
               ((divorces + affs + 2) * settings.Waifu.Multipliers.WaifuReset);
    }

    public async Task<bool> TryReset(IUser user)
    {
        await using var uow = db.GetDbContext();
        var price = await GetResetPrice(user);
        if (!await cs.RemoveAsync(user.Id, "Waifu Reset", price, true))
            return false;

        var affs = uow.WaifuUpdates
            .AsQueryable()
            .Where(w => w.User.UserId == user.Id
                        && w.UpdateType == WaifuUpdateType.AffinityChanged
                        && w.New != null);

        var divorces = uow.WaifuUpdates
            .AsQueryable()
            .Where(x => x.Old != null &&
                        x.Old.UserId == user.Id &&
                        x.UpdateType == WaifuUpdateType.Claimed &&
                        x.New == null);

        //reset changes of heart to 0
        uow.WaifuUpdates.RemoveRange(affs);
        //reset divorces to 0
        uow.WaifuUpdates.RemoveRange(divorces);
        var waifu = await uow.WaifuInfo.ByWaifuUserId(user.Id);
        //reset price, remove items
        //remove owner, remove affinity
        waifu.Price = 50;
        waifu.Items.Clear();
        waifu.ClaimerId = null;
        waifu.AffinityId = null;

        //wives stay though

        await uow.SaveChangesAsync().ConfigureAwait(false);

        return true;
    }

    public async Task<(WaifuInfo, bool, WaifuClaimResult)> ClaimWaifuAsync(IUser user, IUser? target, int amount)
    {
        var settings = gss.Data;
        WaifuClaimResult result;
        WaifuInfo w;
        bool isAffinity;
        await using (var uow = db.GetDbContext())
        {
            w = await uow.WaifuInfo.ByWaifuUserId(target.Id);
            isAffinity = w?.Affinity?.UserId == user.Id;
            if (w == null)
            {
                var claimer = await uow.GetOrCreateUser(user);
                var waifu = await uow.GetOrCreateUser(target);
                if (!await cs.RemoveAsync(user.Id, "Claimed Waifu", amount, true))
                {
                    result = WaifuClaimResult.NotEnoughFunds;
                }
                else
                {
                    uow.WaifuInfo.Add(w = new WaifuInfo
                    {
                        Waifu = waifu, Claimer = claimer, Affinity = null, Price = amount
                    });
                    uow.WaifuUpdates.Add(new WaifuUpdate
                    {
                        User = waifu, Old = null, New = claimer, UpdateType = WaifuUpdateType.Claimed
                    });
                    result = WaifuClaimResult.Success;
                }
            }
            else if (isAffinity && amount > w.Price * settings.Waifu.Multipliers.CrushClaim)
            {
                if (!await cs.RemoveAsync(user.Id, "Claimed Waifu", amount, true))
                {
                    result = WaifuClaimResult.NotEnoughFunds;
                }
                else
                {
                    var oldClaimer = w.Claimer;
                    w.Claimer = await uow.GetOrCreateUser(user);
                    w.Price = amount + (amount / 4);
                    result = WaifuClaimResult.Success;

                    uow.WaifuUpdates.Add(new WaifuUpdate
                    {
                        User = w.Waifu, Old = oldClaimer, New = w.Claimer, UpdateType = WaifuUpdateType.Claimed
                    });
                }
            }
            else if (amount >= w.Price * settings.Waifu.Multipliers.NormalClaim) // if no affinity
            {
                if (!await cs.RemoveAsync(user.Id, "Claimed Waifu", amount, true))
                {
                    result = WaifuClaimResult.NotEnoughFunds;
                }
                else
                {
                    var oldClaimer = w.Claimer;
                    w.Claimer = await uow.GetOrCreateUser(user);
                    w.Price = amount;
                    result = WaifuClaimResult.Success;

                    uow.WaifuUpdates.Add(new WaifuUpdate
                    {
                        User = w.Waifu, Old = oldClaimer, New = w.Claimer, UpdateType = WaifuUpdateType.Claimed
                    });
                }
            }
            else
            {
                result = WaifuClaimResult.InsufficientAmount;
            }

            await uow.SaveChangesAsync().ConfigureAwait(false);
        }

        return (w, isAffinity, result);
    }

    public async Task<(DiscordUser?, bool, TimeSpan?)> ChangeAffinityAsync(IUser user, IGuildUser? target)
    {
        DiscordUser? oldAff = null;
        var success = false;
        TimeSpan? remaining = null;
        await using (var uow = db.GetDbContext())
        {
            var w = await uow.WaifuInfo.ByWaifuUserId(user.Id);
            var newAff = target == null ? null : await uow.GetOrCreateUser(target);
            if (w?.Affinity?.UserId == target?.Id)
            {
            }
            else if (!cache.TryAddAffinityCooldown(user.Id, out remaining))
            {
            }
            else if (w == null)
            {
                var thisUser = await uow.GetOrCreateUser(user);
                uow.WaifuInfo.Add(new WaifuInfo
                {
                    Affinity = newAff, Waifu = thisUser, Price = 1, Claimer = null
                });
                success = true;

                uow.WaifuUpdates.Add(new WaifuUpdate
                {
                    User = thisUser, Old = null, New = newAff, UpdateType = WaifuUpdateType.AffinityChanged
                });
            }
            else
            {
                if (w.Affinity != null)
                    oldAff = w.Affinity;
                w.Affinity = newAff;
                success = true;

                uow.WaifuUpdates.Add(new WaifuUpdate
                {
                    User = w.Waifu, Old = oldAff, New = newAff, UpdateType = WaifuUpdateType.AffinityChanged
                });
            }

            await uow.SaveChangesAsync().ConfigureAwait(false);
        }

        return (oldAff, success, remaining);
    }

    public async Task<IEnumerable<WaifuLbResult>> GetTopWaifuInfoAtPage(int page)
    {
        await using var uow = db.GetDbContext();
        return await uow.WaifuInfo.GetTop(9, page * 9);
    }

    public async Task<(WaifuInfo, DivorceResult, long, TimeSpan?)> DivorceWaifuAsync(IUser user, ulong targetId)
    {
        DivorceResult result;
        TimeSpan? remaining = null;
        long amount = 0;
        WaifuInfo w;
        await using (var uow = db.GetDbContext())
        {
            w = await uow.WaifuInfo.ByWaifuUserId(targetId);
            if (w?.Claimer == null || w.Claimer.UserId != user.Id)
            {
                result = DivorceResult.NotYourWife;
            }
            else if (!cache.TryAddDivorceCooldown(user.Id, out remaining))
            {
                result = DivorceResult.Cooldown;
            }
            else
            {
                amount = w.Price / 2;

                if (w.Affinity?.UserId == user.Id)
                {
                    await cs.AddAsync(w.Waifu.UserId, "Waifu Compensation", amount, true);
                    w.Price = (int)Math.Floor(w.Price * gss.Data.Waifu.Multipliers.DivorceNewValue);
                    result = DivorceResult.SucessWithPenalty;
                }
                else
                {
                    await cs.AddAsync(user.Id, "Waifu Refund", amount, true);

                    result = DivorceResult.Success;
                }

                var oldClaimer = w.Claimer;
                w.Claimer = null;

                uow.WaifuUpdates.Add(new WaifuUpdate
                {
                    User = w.Waifu, Old = oldClaimer, New = null, UpdateType = WaifuUpdateType.Claimed
                });
            }

            await uow.SaveChangesAsync().ConfigureAwait(false);
        }

        return (w, result, amount, remaining);
    }

    public async Task<bool> GiftWaifuAsync(IUser from, IUser giftedWaifu, WaifuItemModel itemObj)
    {
        if (!await cs.RemoveAsync(from, "Bought waifu item", itemObj.Price, gamble: true)) return false;

        await using var uow = db.GetDbContext();
        var w = await uow.WaifuInfo.ByWaifuUserId(giftedWaifu.Id,
            set => set.Include(x => x.Items)
                .Include(x => x.Claimer));
        if (w == null)
        {
            uow.WaifuInfo.Add(w = new WaifuInfo
            {
                Affinity = null, Claimer = null, Price = 1, Waifu = await uow.GetOrCreateUser(giftedWaifu)
            });
        }

        w.Items.Add(new WaifuItem
        {
            Name = itemObj.Name.ToLowerInvariant(), ItemEmoji = itemObj.ItemEmoji
        });

        if (w.Claimer?.UserId == from.Id)
            w.Price += (int)(itemObj.Price * gss.Data.Waifu.Multipliers.GiftEffect);
        else
            w.Price += itemObj.Price / 2;

        await uow.SaveChangesAsync().ConfigureAwait(false);

        return true;
    }

    public async Task<WaifuInfoStats> GetFullWaifuInfoAsync(ulong targetId)
    {
        await using var uow = db.GetDbContext();
        var wi = await uow.GetWaifuInfo(targetId) ?? new WaifuInfoStats
        {
            AffinityCount = 0,
            AffinityName = null,
            ClaimCount = 0,
            ClaimerName = null,
            Claims30 = new List<string>(),
            DivorceCount = 0,
            FullName = null,
            Items = new List<WaifuItem>(),
            Price = 1
        };

        return wi;
    }

    public static string GetClaimTitle(int count)
    {
        var title = count switch
        {
            0 => ClaimTitle.Lonely,
            1 => ClaimTitle.Devoted,
            < 3 => ClaimTitle.Rookie,
            < 6 => ClaimTitle.Schemer,
            < 10 => ClaimTitle.Dilettante,
            < 17 => ClaimTitle.Intermediate,
            < 25 => ClaimTitle.Seducer,
            < 35 => ClaimTitle.Expert,
            < 50 => ClaimTitle.Veteran,
            < 75 => ClaimTitle.Incubis,
            < 100 => ClaimTitle.HaremKing,
            _ => ClaimTitle.HaremGod
        };

        return title.ToString().Replace('_', ' ');
    }

    public static string GetAffinityTitle(int count)
    {
        var title = count switch
        {
            < 1 => AffinityTitle.Pure,
            < 2 => AffinityTitle.Faithful,
            < 4 => AffinityTitle.Playful,
            < 8 => AffinityTitle.Cheater,
            < 11 => AffinityTitle.Tainted,
            < 15 => AffinityTitle.Corrupted,
            < 20 => AffinityTitle.Lewd,
            < 25 => AffinityTitle.Sloot,
            < 35 => AffinityTitle.Depraved,
            _ => AffinityTitle.Harlot
        };

        return title.ToString().Replace('_', ' ');
    }

    public IReadOnlyList<WaifuItemModel> GetWaifuItems()
    {
        var conf = gss.Data;
        return gss.Data.Waifu.Items
            .Select(x =>
                new WaifuItemModel(x.ItemEmoji, (int)(x.Price * conf.Waifu.Multipliers.AllGiftPrices), x.Name))
            .ToList();
    }

    public class FullWaifuInfo
    {
        public WaifuInfo Waifu { get; set; }
        public IEnumerable<string> Claims { get; set; }
        public int Divorces { get; set; }
    }
}