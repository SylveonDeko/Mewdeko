using System.Collections.Generic;
using Mewdeko.Core.Services.Database.Models;

namespace Mewdeko.Core.Services.Database.Repositories
{
    public interface IReminderRepository : IRepository<Reminder>
    {
        IEnumerable<Reminder> GetIncludedReminders(IEnumerable<ulong> guildIds);
        IEnumerable<Reminder> RemindersFor(ulong userId, int page);
    }
}