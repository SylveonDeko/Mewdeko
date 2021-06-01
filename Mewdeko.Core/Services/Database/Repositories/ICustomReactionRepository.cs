using System.Collections.Generic;
using Mewdeko.Core.Services.Database.Models;

namespace Mewdeko.Core.Services.Database.Repositories
{
    public interface ICustomReactionRepository : IRepository<CustomReaction>
    {
        IEnumerable<CustomReaction> ForId(ulong id);
        int ClearFromGuild(ulong id);
        CustomReaction GetByGuildIdAndInput(ulong? guildId, string input);
    }
}
