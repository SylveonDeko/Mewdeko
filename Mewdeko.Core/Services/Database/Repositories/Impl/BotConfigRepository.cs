using Mewdeko.Core.Services.Database.Models;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using System;

namespace Mewdeko.Core.Services.Database.Repositories.Impl
{
    public class BotConfigRepository : Repository<BotConfig>, IBotConfigRepository
    {
        public BotConfigRepository(DbContext context) : base(context)
        {
        }

        public BotConfig GetOrCreate(Func<DbSet<BotConfig>, IQueryable<BotConfig>> includes = null)
        {
            BotConfig config;

            
            if (includes == null)
            {
                config = _set.Include(bc => bc.RotatingStatusMessages)
                    .Include(bc => bc.RaceAnimals)
                    .FirstOrDefault();

                config = _set
                    .Include(bc => bc.Blacklist)
                    .Include(bc => bc.EightBallResponses)
                    .Include(bc => bc.StartupCommands)
                    .FirstOrDefault();
            }
            else
            {
                config = includes(_set).FirstOrDefault();
            }

            if (config == null)
            {
                _set.Add(config = new BotConfig());
                _context.SaveChanges();
            }
            return config;
        }
    }
}
