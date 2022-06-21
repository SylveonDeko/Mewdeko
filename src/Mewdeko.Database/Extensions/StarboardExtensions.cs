using Mewdeko.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Database.Extensions;

public static class StarboardExtensions
{
    public static StarboardPosts ForMsgId(this DbSet<StarboardPosts> set, ulong msgid) => set.AsQueryable().FirstOrDefault(x => x.MessageId == msgid);

    public static StarboardPosts[] All(this DbSet<StarboardPosts> set)
        => set.AsQueryable().ToArray();
}