using System.Collections.Generic;
using Mewdeko.Services.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Services.Database.Repositories.Impl;

public class CustomReactionsRepository : Repository<CustomReaction>, ICustomReactionRepository
{
    public CustomReactionsRepository(DbContext context) : base(context)
    {
    }

    public int ClearFromGuild(ulong id) => Context.Database.ExecuteSqlInterpolated($"DELETE FROM CustomReactions WHERE GuildId={id};");

    public IEnumerable<CustomReaction> ForId(ulong id) =>
        Set
            .AsNoTracking()
            .AsQueryable()
            .Where(x => x.GuildId == id)
            .ToArray();

    public CustomReaction GetByGuildIdAndInput(ulong? guildId, string input) => Set.FirstOrDefault(x => x.GuildId == guildId && x.Trigger.ToUpper() == input);
}