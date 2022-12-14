using Mewdeko.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Database.Extensions;

public static class DbExtensions
{
    public static async Task<T> GetById<T>(this DbSet<T> set, int id) where T : DbEntity
        => await set.FirstOrDefaultAsync(x => x.Id == id);
}