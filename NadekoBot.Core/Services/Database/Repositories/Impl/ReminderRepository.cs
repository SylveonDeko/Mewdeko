using NadekoBot.Core.Services.Database.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;

namespace NadekoBot.Core.Services.Database.Repositories.Impl
{
    public class ReminderRepository : Repository<Reminder>, IReminderRepository
    {
        public ReminderRepository(DbContext context) : base(context)
        {
        }

        public IEnumerable<Reminder> GetIncludedReminders(IEnumerable<ulong> guildIds)
        {
            return _set.AsQueryable().Where(x => guildIds.Contains(x.ServerId) || x.ServerId == 0).ToList();
        }

        public IEnumerable<Reminder> RemindersFor(ulong userId, int page)
        {
            return _set.AsQueryable()
                .Where(x => x.UserId == userId)
                .OrderBy(x => x.DateAdded)
                .Skip(page * 10)
                .Take(10);
        }
    }
}
