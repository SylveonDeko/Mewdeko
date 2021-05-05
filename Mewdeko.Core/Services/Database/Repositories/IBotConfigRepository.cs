using Microsoft.EntityFrameworkCore;
using Mewdeko.Core.Services.Database.Models;
using System;
using System.Linq;

namespace Mewdeko.Core.Services.Database.Repositories
{
    public interface IBotConfigRepository : IRepository<BotConfig>
    {
        BotConfig GetOrCreate(Func<DbSet<BotConfig>, IQueryable<BotConfig>> includes = null);
    }
}
