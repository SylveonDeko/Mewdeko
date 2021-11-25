using System.Collections.Generic;
using System.Linq;
using Mewdeko.Services.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Services.Database.Repositories.Impl
{
    public class ReminderRepository : Repository<Reminder>, IReminderRepository
    {
        public ReminderRepository(DbContext context) : base(context)
        {
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