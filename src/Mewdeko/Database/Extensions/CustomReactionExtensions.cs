using LinqToDB;
using Mewdeko.Database.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;

namespace Mewdeko.Database.Extensions;

public static class CustomReactionExtensions
{
    public static int ClearFromGuild(this DbSet<CustomReaction> crs, ulong guildId) 
        => crs.Delete(x => x.GuildId == guildId);

    public static IEnumerable<CustomReaction> ForId(this DbSet<CustomReaction> crs, ulong id) =>
        crs
            .AsNoTracking()
            .AsQueryable()
            .Where(x => x.GuildId == id)
            .ToArray();

    public static CustomReaction GetByGuildIdAndInput(this DbSet<CustomReaction> crs, ulong? guildId, string input) 
        => crs.FirstOrDefault(x => x.GuildId == guildId && x.Trigger.ToUpper() == input);
}