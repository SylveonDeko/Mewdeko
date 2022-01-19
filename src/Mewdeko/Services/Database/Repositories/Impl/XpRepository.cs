using System.Collections.Generic;
using Mewdeko.Services.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Services.Database.Repositories.Impl;

public class XpRepository : Repository<UserXpStats>, IXpRepository
{
    public XpRepository(DbContext context) : base(context)
    {
    }

    public UserXpStats GetOrCreateUser(ulong guildId, ulong userId)
    {
        var usr = Set.FirstOrDefault(x => x.UserId == userId && x.GuildId == guildId);

        if (usr == null)
            Context.Add(usr = new UserXpStats
            {
                Xp = 0,
                UserId = userId,
                NotifyOnLevelUp = XpNotificationLocation.None,
                GuildId = guildId
            });

        return usr;
    }

    public List<UserXpStats> GetUsersFor(ulong guildId, int page) =>
        Set
            .AsQueryable()
            .AsNoTracking()
            .Where(x => x.GuildId == guildId)
            .OrderByDescending(x => x.Xp + x.AwardedXp)
            .Skip(page * 9)
            .Take(9)
            .ToList();

    public List<UserXpStats> GetTopUserXps(ulong guildId, int count) =>
        Set
            .AsQueryable()
            .AsNoTracking()
            .Where(x => x.GuildId == guildId)
            .OrderByDescending(x => x.Xp + x.AwardedXp)
            .Take(count)
            .ToList();

    public int GetUserGuildRanking(ulong userId, ulong guildId) =>
        Set
            .AsQueryable()
            .AsNoTracking()
            .Count(x => x.GuildId == guildId && x.Xp + x.AwardedXp >
                Set.AsQueryable()
                    .Where(y => y.UserId == userId && y.GuildId == guildId)
                    .Select(y => y.Xp + y.AwardedXp)
                    .FirstOrDefault()) + 1;

    public void ResetGuildUserXp(ulong userId, ulong guildId) =>
        Context.Database.ExecuteSqlInterpolated(
            $"DELETE FROM UserXpStats WHERE UserId={userId} AND GuildId={guildId};");

    public void ResetGuildXp(ulong guildId) => Context.Database.ExecuteSqlInterpolated($"DELETE FROM UserXpStats WHERE GuildId={guildId};");
}