using System.Collections.Generic;
using Mewdeko.Services.Database.Models;

namespace Mewdeko.Services.Database.Repositories;

public interface IAfkRepository : IRepository<AFK>
{
    List<AFK> ForId(ulong guildid, ulong uid);
    AFK[] ForGuild(ulong guildid);
}