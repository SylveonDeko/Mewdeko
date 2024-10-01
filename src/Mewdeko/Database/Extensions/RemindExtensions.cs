using LinqToDB.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Database.Extensions;

/// <summary>
///     Provides extension methods for querying Reminder entities.
/// </summary>
public static class RemindExtensions
{
    /// <summary>
    ///     Retrieves Reminder entities for a set of guild IDs or global reminders.
    /// </summary>
    /// <param name="reminders">The DbSet of Reminder entities to query.</param>
    /// <param name="guildIds">The collection of guild IDs to include.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains an IEnumerable of Reminder
    ///     entities.
    /// </returns>
    public static async Task<IEnumerable<Reminder>> GetIncludedReminders(this DbSet<Reminder> reminders,
        IEnumerable<ulong> guildIds)
    {
        return await reminders.AsQueryable()
            .Where(x => guildIds.Contains(x.ServerId) || x.ServerId == 0)
            .ToListAsyncEF();
    }

    /// <summary>
    ///     Retrieves a paged list of Reminder entities for a specific user.
    /// </summary>
    /// <param name="reminders">The DbSet of Reminder entities to query.</param>
    /// <param name="userId">The ID of the user to filter by.</param>
    /// <param name="page">The page number (zero-based) to retrieve.</param>
    /// <returns>An IEnumerable of Reminder entities for the specified user, paged.</returns>
    public static IEnumerable<Reminder> RemindersFor(this DbSet<Reminder> reminders, ulong userId, int page)
    {
        return reminders.AsQueryable()
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.DateAdded)
            .Skip(page * 10)
            .Take(10);
    }

    /// <summary>
    ///     Retrieves a paged list of Reminder entities for a specific server.
    /// </summary>
    /// <param name="reminders">The DbSet of Reminder entities to query.</param>
    /// <param name="serverId">The ID of the server to filter by.</param>
    /// <param name="page">The page number (zero-based) to retrieve.</param>
    /// <returns>An IEnumerable of Reminder entities for the specified server, paged.</returns>
    public static IEnumerable<Reminder> RemindersForServer(this DbSet<Reminder> reminders, ulong serverId, int page)
    {
        return reminders.AsQueryable()
            .Where(x => x.ServerId == serverId)
            .OrderBy(x => x.DateAdded)
            .Skip(page * 10)
            .Take(10);
    }

    /// <summary>
    ///     Retrieves all Reminder entities for a specific user, optionally filtered by guild.
    /// </summary>
    /// <param name="reminders">The DbSet of Reminder entities to query.</param>
    /// <param name="userId">The ID of the user to filter by.</param>
    /// <param name="guildId">The optional ID of the guild to filter by.</param>
    /// <returns>An IEnumerable of Reminder entities for the specified user and optional guild.</returns>
    public static IEnumerable<Reminder> AllRemindersFor(this DbSet<Reminder> reminders, ulong userId, ulong? guildId)
    {
        return reminders.AsQueryable()
            .Where(x => guildId != null && x.ServerId == guildId || x.UserId == userId)
            .OrderBy(x => x.DateAdded);
    }
}