using LinqToDB;
using LinqToDB.EntityFrameworkCore;
using Mewdeko.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Database.Extensions;

public static class XpExtensions
{
    public static async Task<UserXpStats> GetOrCreateUser(this DbSet<UserXpStats> set, ulong guildId, ulong userId)
    {
        var usr = await set.FirstOrDefaultAsyncEF(x => x.UserId == userId && x.GuildId == guildId);

        if (usr == null)
        {
            await set.AddAsync(usr = new UserXpStats
            {
                Xp = 0, UserId = userId, NotifyOnLevelUp = XpNotificationLocation.None, GuildId = guildId
            });
        }

        return usr;
    }

    public static async Task<List<UserXpStats>> GetUsersFor(this DbSet<UserXpStats> set, ulong guildId, int page) =>
        await set.AsQueryable().AsNoTracking().Where(x => x.GuildId == guildId).OrderByDescending(x => x.Xp + x.AwardedXp)
            .Skip(page * 9).Take(9).ToListAsyncEF();

    public static async Task<List<UserXpStats>> GetTopUserXps(this DbSet<UserXpStats> set, ulong guildId) =>
        await set.AsQueryable().AsNoTracking().Where(x => x.GuildId == guildId).OrderByDescending(x => x.Xp + x.AwardedXp)
            .ToListAsyncEF();

    public static int GetUserGuildRanking(this DbSet<UserXpStats> set, ulong userId, ulong guildId) =>
        set.AsQueryable().AsNoTracking().Count(x =>
            x.GuildId == guildId
            && x.Xp + x.AwardedXp
            > set.AsQueryable().Where(y => y.UserId == userId && y.GuildId == guildId).Select(y => y.Xp + y.AwardedXp)
                .FirstOrDefault())
        + 1;

    public static void ResetGuildUserXp(this DbSet<UserXpStats> set, ulong userId, ulong guildId) =>
        set.Delete(x => x.UserId == userId && x.GuildId == guildId);

    public static void ResetGuildXp(this DbSet<UserXpStats> set, ulong guildId) =>
        set.Delete(x => x.GuildId == guildId);
}