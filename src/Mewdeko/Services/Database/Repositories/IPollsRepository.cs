using System.Collections.Generic;
using Mewdeko.Services.Database.Models;

namespace Mewdeko.Services.Database.Repositories;

public interface IPollsRepository : IRepository<Poll>
{
    IEnumerable<Poll> GetAllPolls();
    void RemovePoll(int id);
}