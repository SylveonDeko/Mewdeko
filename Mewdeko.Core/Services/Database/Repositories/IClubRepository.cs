using System;
using System.Linq;
using Mewdeko.Core.Services.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Core.Services.Database.Repositories
{
    public interface IClubRepository : IRepository<ClubInfo>
    {
        int GetNextDiscrim(string clubName);
        ClubInfo GetByName(string v, int discrim, Func<DbSet<ClubInfo>, IQueryable<ClubInfo>> func = null);
        ClubInfo GetByOwner(ulong userId, Func<DbSet<ClubInfo>, IQueryable<ClubInfo>> func = null);
        ClubInfo GetByOwnerOrAdmin(ulong userId);
        ClubInfo GetByMember(ulong userId, Func<DbSet<ClubInfo>, IQueryable<ClubInfo>> func = null);
        ClubInfo[] GetClubLeaderboardPage(int page);
    }
}