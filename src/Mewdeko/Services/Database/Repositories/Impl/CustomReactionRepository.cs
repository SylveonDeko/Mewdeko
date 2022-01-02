using System.Collections.Generic;
using Mewdeko.Services.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Services.Database.Repositories.Impl;

public class CustomReactionsRepository : Repository<CustomReaction>, ICustomReactionRepository
{
    public CustomReactionsRepository(DbContext context) : base(context)
    {
    }

    public int ClearFromGuild(ulong id) => _context.Database.ExecuteSqlInterpolated($"DELETE FROM CustomReactions WHERE GuildId={id};");

    public IEnumerable<CustomReaction> ForId(ulong id) =>
        _set
            .AsNoTracking()
            .AsQueryable()
            .Where(x => x.GuildId == id)
            .ToArray();

    public CustomReaction GetByGuildIdAndInput(ulong? guildId, string input) => _set.FirstOrDefault(x => x.GuildId == guildId && x.Trigger.ToUpper() == input);
}