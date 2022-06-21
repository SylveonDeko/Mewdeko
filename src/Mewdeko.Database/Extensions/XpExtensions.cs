using LinqToDB;
using Mewdeko.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Database.Extensions;

public static class XpExtensions
{
    public static UserXpStats GetOrCreateUser(this DbSet<UserXpStats> set, ulong guildId, ulong userId)
    {
        var usr = set.FirstOrDefault(x => x.UserId == userId && x.GuildId == guildId);

        if (usr == null)
        {
            set.Add(usr = new UserXpStats
            {
                Xp = 0,
                UserId = userId,
                NotifyOnLevelUp = XpNotificationLocation.None,
                GuildId = guildId
            });
        }

        return usr;
    }

    public static List<UserXpStats> GetUsersFor(this DbSet<UserXpStats> set, ulong guildId, int page) =>
        set.AsQueryable().AsNoTracking().Where(x => x.GuildId == guildId).OrderByDescending(x => x.Xp + x.AwardedXp)
           .Skip(page * 9).Take(9).ToList();

    public static List<UserXpStats> GetTopUserXps(this DbSet<UserXpStats> set, ulong guildId) =>
        set.AsQueryable().AsNoTracking().Where(x => x.GuildId == guildId).OrderByDescending(x => x.Xp + x.AwardedXp)
           .ToList();

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