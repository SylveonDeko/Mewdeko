using Microsoft.EntityFrameworkCore;
using NadekoBot.Core.Services.Database.Models;
using System;
using System.Linq;

namespace NadekoBot.Core.Services.Database.Repositories
{
    public interface IBotConfigRepository : IRepository<BotConfig>
    {
        BotConfig GetOrCreate(Func<DbSet<BotConfig>, IQueryable<BotConfig>> includes = null);
    }
}
