using System.Collections.Generic;
using Mewdeko.Services.Database.Models;

namespace Mewdeko.Services.Database.Repositories
{
    public interface IReminderRepository : IRepository<Reminder>
    {
        IEnumerable<Reminder> RemindersFor(ulong userId, int page);
    }
}