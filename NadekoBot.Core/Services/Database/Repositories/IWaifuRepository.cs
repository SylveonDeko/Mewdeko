using Microsoft.EntityFrameworkCore;
using NadekoBot.Core.Services.Database.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NadekoBot.Core.Services.Database.Repositories
{
    public interface IWaifuRepository : IRepository<WaifuInfo>
    {
        IEnumerable<WaifuLbResult> GetTop(int count, int skip = 0);
        WaifuInfo ByWaifuUserId(ulong userId, Func<DbSet<WaifuInfo>, IQueryable<WaifuInfo>> includes = null);
        IEnumerable<string> GetWaifuNames(ulong userId);
        decimal GetTotalValue();
        int AffinityCount(ulong userId);
        WaifuInfoStats GetWaifuInfo(ulong id);
    }

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
}
