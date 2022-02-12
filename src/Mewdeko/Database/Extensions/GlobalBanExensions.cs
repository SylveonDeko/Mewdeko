using Mewdeko.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Database.Extensions;

public static class GlobalBanExensions
{
    public static GlobalBans[] AllGlobalBans(this DbSet<GlobalBans> Set) => Set.AsQueryable().ToArray();

    public static GlobalBans[] GlobalBansByType(this DbSet<GlobalBans> Set, string type) => Set.AsQueryable().Where(x => x.Type == type).ToArray();

    public static GlobalBans[] GetGlobalBansAddedBy(this DbSet<GlobalBans> Set, ulong uid) => Set.AsQueryable().Where(x => x.AddedBy == uid).ToArray();

    public static GlobalBans[] GetGlobalBanById(this DbSet<GlobalBans> Set, int id) => Set.AsQueryable().Where(x => x.Id == id).ToArray();

    public static GlobalBans[] GetGlobalBanByUserId(this DbSet<GlobalBans> Set, ulong uid) => Set.AsQueryable().Where(x => x.UserId == uid).ToArray();
}