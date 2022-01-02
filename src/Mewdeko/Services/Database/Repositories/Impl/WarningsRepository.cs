using Mewdeko.Services.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Services.Database.Repositories.Impl;

public class WarningsRepository2 : Repository<Warning2>, IWarningsRepository2
{
    public WarningsRepository2(DbContext context) : base(context)
    {
    }

    public Warning2[] ForId(ulong guildId, ulong userId)
    {
        var query = _set.AsQueryable().Where(x => x.GuildId == guildId && x.UserId == userId)
            .OrderByDescending(x => x.DateAdded);

        return query.ToArray();
    }

    public bool Forgive(ulong guildId, ulong userId, string mod, int index)
    {
        if (index < 0)
            throw new ArgumentOutOfRangeException(nameof(index));

        var warn2 = _set.AsQueryable().Where(x => x.GuildId == guildId && x.UserId == userId)
            .OrderByDescending(x => x.DateAdded)
            .Skip(index)
            .FirstOrDefault();

        if (warn2 == null || warn2.Forgiven)
            return false;

        warn2.Forgiven = true;
        warn2.ForgivenBy = mod;
        return true;
    }

    public async Task ForgiveAll(ulong guildId, ulong userId, string mod) =>
        await _set.AsQueryable().Where(x => x.GuildId == guildId && x.UserId == userId)
                  .ForEachAsync(x =>
                  {
                      if (x.Forgiven) return;
                      x.Forgiven = true;
                      x.ForgivenBy = mod;
                  });

    public Warning2[] GetForGuild(ulong id) => _set.AsQueryable().Where(x => x.GuildId == id).ToArray();
}