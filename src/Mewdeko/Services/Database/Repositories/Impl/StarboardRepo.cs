using Mewdeko.Services.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Services.Database.Repositories.Impl;

public class StarboardRepository : Repository<Starboard>, IStarboardRepository
{
    public StarboardRepository(DbContext context) : base(context)
    {
    }

    public Starboard ForMsgId(ulong msgid)
    {
        var query = Set.AsQueryable().Where(x => x.MessageId == msgid);

        return query.FirstOrDefault();
    }

    public Starboard[] GetAll() 
        => Set.AsQueryable().ToArray();
}