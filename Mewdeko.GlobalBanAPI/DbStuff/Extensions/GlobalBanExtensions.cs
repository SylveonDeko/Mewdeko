using Mewdeko.GlobalBanAPI.DbStuff.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.GlobalBanAPI.DbStuff.Extensions;

public static class GlobalBanExtensions
{
    public static GlobalBans[] AllGlobalBans(this DbSet<GlobalBans> set) => set.AsQueryable().ToArray();

    public static GlobalBans[] GlobalBansByType(this DbSet<GlobalBans> set, GbType type) =>
        set.AsQueryable().Where(x => x.Type == type).ToArray();

    public static GlobalBans[] GetGlobalBansAddedBy(this DbSet<GlobalBans> set, ulong uid) =>
        set.AsQueryable().Where(x => x.AddedBy == uid).ToArray();

    public static GlobalBans? GetGlobalBanById(this DbSet<GlobalBans> set, int id) =>
        set.AsQueryable().FirstOrDefault(x => x.Id == id);

    public static GlobalBans[] GetGlobalBanByUserId(this DbSet<GlobalBans> set, ulong uid) =>
        set.AsQueryable().Where(x => x.UserId == uid).ToArray();
}