using Mewdeko.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Database.Extensions;

public static class HighlightExtensions
{
    public static List<Highlights> ForUser(this DbSet<Highlights> set, ulong guildId, ulong userId)
        => set.AsQueryable().Where(x => x.UserId == userId && x.GuildId == guildId).ToList();

    public static List<HighlightSettings> ForUser(this DbSet<HighlightSettings> set, ulong guildId, ulong userId)
        => set.AsQueryable().Where(x => x.UserId == userId && x.GuildId == guildId).ToList();

    public static List<HighlightSettings> AllHighlightSettings(this DbSet<HighlightSettings> set)
        => set.AsQueryable().ToList();

    public static List<Highlights> AllHighlights(this DbSet<Highlights> set)
        => set.AsQueryable().ToList();
}