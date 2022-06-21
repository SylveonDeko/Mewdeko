using Mewdeko.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Database.Extensions;

public static class DbExtensions
{
    public static T GetById<T>(this DbSet<T> set, int id) where T : DbEntity
        => set.FirstOrDefault(x => x.Id == id);
}