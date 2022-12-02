using Mewdeko.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Database.Extensions;

public static class GlobalBanExensions
{
    public static GlobalBans[] AllGlobalBans(this DbSet<GlobalBans> set) => set.AsQueryable().ToArray();

    public static GlobalBans[] GlobalBansByType(this DbSet<GlobalBans> set, string type) => set.AsQueryable().Where(x => x.Type == type).ToArray();

    public static GlobalBans[] GetGlobalBansAddedBy(this DbSet<GlobalBans> set, ulong uid) => set.AsQueryable().Where(x => x.AddedBy == uid).ToArray();

    public static GlobalBans[] GetGlobalBanById(this DbSet<GlobalBans> set, int id) => set.AsQueryable().Where(x => x.Id == id).ToArray();

    public static GlobalBans[] GetGlobalBanByUserId(this DbSet<GlobalBans> set, ulong uid) => set.AsQueryable().Where(x => x.UserId == uid).ToArray();
}