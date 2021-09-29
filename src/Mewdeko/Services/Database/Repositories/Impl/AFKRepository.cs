﻿using System.Linq;
using Mewdeko.Services.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Services.Database.Repositories.Impl
{
    public class AFKRepository : Repository<AFK>, IAFKRepository
    {
        public AFKRepository(DbContext context) : base(context)
        {
        }

        public AFK[] ForId(ulong guildId, ulong uid)
        {
            var query = _set.AsQueryable().Where(x => x.GuildId == guildId && x.UserId == uid);

            return query.ToArray();
        }
    }
}