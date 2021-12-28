using System.Collections.Generic;
using Mewdeko.Services.Database.Models;

namespace Mewdeko.Services.Database.Repositories;

public interface ICustomReactionRepository : IRepository<CustomReaction>
{
    IEnumerable<CustomReaction> ForId(ulong id);
    int ClearFromGuild(ulong id);
    CustomReaction GetByGuildIdAndInput(ulong? guildId, string input);
}