using NadekoBot.Core.Services.Database.Models;
using System.Collections.Generic;

namespace NadekoBot.Core.Services.Database.Repositories
{
    public interface IPollsRepository : IRepository<Poll>
    {
        IEnumerable<Poll> GetAllPolls();
        void RemovePoll(int id);
    }
}
