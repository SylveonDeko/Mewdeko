using Mewdeko.Services.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Services.Database.Repositories.Impl;

public class StarboardRepository : Repository<StarboardPosts>, IStarboardRepository
{
    public StarboardRepository(DbContext context) : base(context)
    {
    }

    public StarboardPosts ForMsgId(ulong msgid)
    {
        var query = Set.AsQueryable().Where(x => x.MessageId == msgid);

        return query.FirstOrDefault();
    }

    public StarboardPosts[] All() 
        => Set.AsQueryable().ToArray();
}