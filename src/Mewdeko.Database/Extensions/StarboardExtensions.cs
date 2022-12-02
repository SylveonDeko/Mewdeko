using LinqToDB.EntityFrameworkCore;
using Mewdeko.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Database.Extensions;

public static class StarboardExtensions
{
    public static async Task<StarboardPosts> ForMsgId(this DbSet<StarboardPosts> set, ulong msgid)
        => await set.AsQueryable().FirstOrDefaultAsyncEF(x => x.MessageId == msgid);

    public static async Task<IEnumerable<StarboardPosts>> All(this DbSet<StarboardPosts> set)
        => await set.AsQueryable().ToArrayAsyncEF();
}