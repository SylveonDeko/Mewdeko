using Mewdeko.Core.Services.Database.Models;
using System.Collections.Generic;

namespace Mewdeko.Core.Services.Database.Repositories
{
    public interface IPollsRepository : IRepository<Poll>
    {
        IEnumerable<Poll> GetAllPolls();
        void RemovePoll(int id);
    }
}
