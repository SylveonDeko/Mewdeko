using Mewdeko.Services.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Services.Database.Repositories.Impl;

public class ClubRepository : Repository<ClubInfo>, IClubRepository
{
    public ClubRepository(DbContext context) : base(context)
    {
    }

    public ClubInfo GetByOwner(ulong userId, Func<DbSet<ClubInfo>, IQueryable<ClubInfo>> func = null)
    {
        if (func == null)
            return Set
                .Include(x => x.Bans)
                .Include(x => x.Applicants)
                .Include(x => x.Users)
                .Include(x => x.Owner)
                .FirstOrDefault(x => x.Owner.UserId == userId);

        return func(Set).FirstOrDefault(x => x.Owner.UserId == userId);
    }

    public ClubInfo GetByOwnerOrAdmin(ulong userId) =>
        Set
            .Include(x => x.Bans)
            .ThenInclude(x => x.User)
            .Include(x => x.Applicants)
            .ThenInclude(x => x.User)
            .Include(x => x.Owner)
            .Include(x => x.Users)
            .FirstOrDefault(x => x.Owner.UserId == userId) ??
        Context.Set<DiscordUser>()
                .Include(x => x.Club)
                .ThenInclude(x => x.Users)
                .Include(x => x.Club)
                .ThenInclude(x => x.Bans)
                .ThenInclude(x => x.User)
                .Include(x => x.Club)
                .ThenInclude(x => x.Applicants)
                .ThenInclude(x => x.User)
                .Include(x => x.Club)
                .ThenInclude(x => x.Owner)
                .FirstOrDefault(x => x.UserId == userId && x.IsClubAdmin)
                ?.Club;

    public ClubInfo GetByName(string name, int discrim, Func<DbSet<ClubInfo>, IQueryable<ClubInfo>> func = null)
    {
        if (func == null)
            return Set.AsQueryable()
                .Where(x => x.Name == name && x.Discrim == discrim)
                .Include(x => x.Users)
                .Include(x => x.Bans)
                .Include(x => x.Applicants)
                .FirstOrDefault();

        return func(Set).FirstOrDefault(x => x.Name == name && x.Discrim == discrim);
    }

    public int GetNextDiscrim(string clubName) =>
        Set.AsQueryable()
            .Where(x => x.Name.ToUpper() == clubName.ToUpper())
            .Select(x => x.Discrim)
            .ToList()
            .DefaultIfEmpty()
            .Max() + 1;

    public ClubInfo GetByMember(ulong userId, Func<DbSet<ClubInfo>, IQueryable<ClubInfo>> func = null)
    {
        if (func == null)
            return Set
                .Include(x => x.Users)
                .Include(x => x.Bans)
                .Include(x => x.Applicants)
                .FirstOrDefault(x => x.Users.Any(y => y.UserId == userId));

        return func(Set).FirstOrDefault(x => x.Users.Any(y => y.UserId == userId));
    }

    public ClubInfo[] GetClubLeaderboardPage(int page) =>
        Set.AsQueryable()
            .OrderByDescending(x => x.Xp)
            .Skip(page * 9)
            .Take(9)
            .ToArray();
}