using LinqToDB.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Database.Extensions;

/// <summary>
/// Provides extension methods for querying and manipulating Warning2 entities.
/// </summary>
public static class MiniWarningExtensions
{
    /// <summary>
    /// Retrieves all warnings for a specific user in a guild.
    /// </summary>
    /// <param name="set">The DbSet of Warning2 entities to query.</param>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="userId">The ID of the user.</param>
    /// <returns>An array of Warning2 entities for the specified user and guild, ordered by date added descending.</returns>
    public static Warning2[] ForId(this DbSet<Warning2> set, ulong guildId, ulong userId)
    {
        var query = set.AsQueryable().Where(x => x.GuildId == guildId && x.UserId == userId)
            .OrderByDescending(x => x.DateAdded);

        return query.ToArray();
    }

    /// <summary>
    /// Forgives a specific warning for a user in a guild.
    /// </summary>
    /// <param name="set">The DbSet of Warning2 entities to query.</param>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="mod">The moderator who is forgiving the warning.</param>
    /// <param name="index">The index of the warning to forgive.</param>
    /// <returns>A task that represents the asynchronous operation. The task result is true if the warning was forgiven, false otherwise.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when index is negative.</exception>
    public static async Task<bool> Forgive(this DbSet<Warning2> set, ulong guildId, ulong userId, string mod, int index)
    {
        if (index < 0)
            throw new ArgumentOutOfRangeException(nameof(index));

        var warn = await set.AsQueryable().Where(x => x.GuildId == guildId && x.UserId == userId)
            .OrderByDescending(x => x.DateAdded)
            .Skip(index)
            .FirstOrDefaultAsyncEF();

        if (warn == null || warn.Forgiven)
            return false;

        warn.Forgiven = true;
        warn.ForgivenBy = mod;
        return true;
    }

    /// <summary>
    /// Forgives all warnings for a specific user in a guild.
    /// </summary>
    /// <param name="set">The DbSet of Warning2 entities to query.</param>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="mod">The moderator who is forgiving the warnings.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public static Task ForgiveAll(this DbSet<Warning2> set, ulong guildId, ulong userId, string mod) =>
        set.AsQueryable().Where(x => x.GuildId == guildId && x.UserId == userId)
            .ForEachAsync(x =>
            {
                if (x.Forgiven) return;
                x.Forgiven = true;
                x.ForgivenBy = mod;
            });

    /// <summary>
    /// Retrieves all warnings for a specific guild.
    /// </summary>
    /// <param name="set">The DbSet of Warning2 entities to query.</param>
    /// <param name="id">The ID of the guild.</param>
    /// <returns>An array of Warning2 entities for the specified guild.</returns>
    public static Warning2[] GetForGuild(this DbSet<Warning2> set, ulong id) =>
        set.AsQueryable().Where(x => x.GuildId == id).ToArray();
}