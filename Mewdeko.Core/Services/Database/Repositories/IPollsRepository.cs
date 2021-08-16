using System.Collections.Generic;
using Mewdeko.Core.Services.Database.Models;

namespace Mewdeko.Core.Services.Database.Repositories
{
    public interface IPollsRepository : IRepository<Poll>
    {
        IEnumerable<Poll> GetAllPolls();
        void RemovePoll(int id);
    }
}