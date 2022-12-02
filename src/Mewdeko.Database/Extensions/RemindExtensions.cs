using LinqToDB.EntityFrameworkCore;
using Mewdeko.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Database.Extensions;

public static class RemindExtensions
{
    public static async Task<IEnumerable<Reminder>> GetIncludedReminders(this DbSet<Reminder> reminders, IEnumerable<ulong> guildIds)
        => await reminders.AsQueryable()
            .Where(x => guildIds.Contains(x.ServerId) || x.ServerId == 0)
            .ToListAsyncEF();

    public static IEnumerable<Reminder> RemindersFor(this DbSet<Reminder> reminders, ulong userId, int page)
        => reminders.AsQueryable()
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.DateAdded)
            .Skip(page * 10)
            .Take(10);

    public static IEnumerable<Reminder> RemindersForServer(this DbSet<Reminder> reminders, ulong serverId, int page)
        => reminders.AsQueryable()
            .Where(x => x.ServerId == serverId)
            .OrderBy(x => x.DateAdded)
            .Skip(page * 10)
            .Take(10);

    public static IEnumerable<Reminder> AllRemindersFor(this DbSet<Reminder> reminders, ulong userId, ulong? guildId) =>
        reminders.AsQueryable()
            .Where(x => (guildId != null && x.ServerId == guildId) || x.UserId == userId)
            .OrderBy(x => x.DateAdded);
}