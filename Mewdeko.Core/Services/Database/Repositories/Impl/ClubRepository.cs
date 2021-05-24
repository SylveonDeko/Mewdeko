using System;
using System.Linq;
using Mewdeko.Core.Services.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Core.Services.Database.Repositories.Impl
{
    public class ClubRepository : Repository<ClubInfo>, IClubRepository
    {
        public ClubRepository(DbContext context) : base(context)
        {
        }

        public ClubInfo GetByOwner(ulong userId, Func<DbSet<ClubInfo>, IQueryable<ClubInfo>> func = null)
        {
            if (func == null)
                return _set
                    .Include(x => x.Bans)
                    .Include(x => x.Applicants)
                    .Include(x => x.Users)
                    .Include(x => x.Owner)
                    .FirstOrDefault(x => x.Owner.UserId == userId);

            return func(_set).FirstOrDefault(x => x.Owner.UserId == userId);
        }

        public ClubInfo GetByOwnerOrAdmin(ulong userId)
        {
            return _set
                       .Include(x => x.Bans)
                       .ThenInclude<ClubInfo, ClubBans, DiscordUser>(x => x.User)
                       .Include(x => x.Applicants)
                       .ThenInclude<ClubInfo, ClubApplicants, DiscordUser>(x => x.User)
                       .Include(x => x.Owner)
                       .Include(x => x.Users)
                       .FirstOrDefault(x => x.Owner.UserId == userId) ??
                   _context.Set<DiscordUser>()
                       .Include(x => x.Club)
                       .ThenInclude(x => x.Users)
                       .Include(x => x.Club)
                       .ThenInclude(x => x.Bans)
                       .ThenInclude<DiscordUser, ClubBans, DiscordUser>(x => x.User)
                       .Include(x => x.Club)
                       .ThenInclude(x => x.Applicants)
                       .ThenInclude<DiscordUser, ClubApplicants, DiscordUser>(x => x.User)
                       .Include(x => x.Club)
                       .ThenInclude(x => x.Owner)
                       .FirstOrDefault(x => x.UserId == userId && x.IsClubAdmin)
                       ?.Club;
        }

        public ClubInfo GetByName(string name, int discrim, Func<DbSet<ClubInfo>, IQueryable<ClubInfo>> func = null)
        {
            if (func == null)
                return _set.AsQueryable()
                    .Where(x => x.Name == name && x.Discrim == discrim)
                    .Include(x => x.Users)
                    .Include(x => x.Bans)
                    .Include(x => x.Applicants)
                    .FirstOrDefault();

            return func(_set).FirstOrDefault(x => x.Name == name && x.Discrim == discrim);
        }

        public int GetNextDiscrim(string clubName)
        {
            return _set.AsQueryable()
                .Where(x => x.Name.ToUpper() == clubName.ToUpper())
                .Select(x => x.Discrim)
                .ToList()
                .DefaultIfEmpty()
                .Max() + 1;
        }

        public ClubInfo GetByMember(ulong userId, Func<DbSet<ClubInfo>, IQueryable<ClubInfo>> func = null)
        {
            if (func == null)
                return _set
                    .Include(x => x.Users)
                    .Include(x => x.Bans)
                    .Include(x => x.Applicants)
                    .FirstOrDefault(x => x.Users.Any(y => y.UserId == userId));

            return func(_set).FirstOrDefault(x => x.Users.Any(y => y.UserId == userId));
        }

        public ClubInfo[] GetClubLeaderboardPage(int page)
        {
            return _set.AsQueryable()
                .OrderByDescending(x => x.Xp)
                .Skip(page * 9)
                .Take(9)
                .ToArray();
        }
    }
}