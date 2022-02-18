using Mewdeko.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Database.Extensions;

public static class SnipeExtensions
{
    public static List<SnipeStore> All(this DbSet<SnipeStore> set) =>
        set
            .AsQueryable()
            .AsNoTracking()
            .ToList();
}