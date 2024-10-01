using LinqToDB.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Database.Extensions;

/// <summary>
///     Provides extension methods for querying and manipulating Warning entities.
/// </summary>
public static class WarningExtensions
{
    /// <summary>
    ///     Retrieves all warnings for a specific user in a guild.
    /// </summary>
    /// <param name="set">The DbSet of Warning entities to query.</param>
    /// <param name="guildId">The ID of the guild to filter by.</param>
    /// <param name="userId">The ID of the user to filter by.</param>
    /// <returns>An array of Warning entities for the specified user and guild, ordered by date added descending.</returns>
    public static Warning[] ForId(this DbSet<Warning> set, ulong guildId, ulong userId)
    {
        var query = set.AsQueryable().Where(x => x.GuildId == guildId && x.UserId == userId)
            .OrderByDescending(x => x.DateAdded);

        return query.ToArray();
    }

    /// <summary>
    ///     Forgives a specific warning for a user in a guild.
    /// </summary>
    /// <param name="set">The DbSet of Warning entities to query.</param>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="mod">The moderator who is forgiving the warning.</param>
    /// <param name="index">The index of the warning to forgive.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result is true if the warning was forgiven, false
    ///     otherwise.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when index is negative.</exception>
    public static async Task<bool> Forgive(this DbSet<Warning> set, ulong guildId, ulong userId, string mod, int index)
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
    ///     Forgives all warnings for a specific user in a guild.
    /// </summary>
    /// <param name="set">The DbSet of Warning entities to query.</param>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="mod">The moderator who is forgiving the warnings.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public static Task ForgiveAll(this DbSet<Warning> set, ulong guildId, ulong userId, string mod)
    {
        return set.AsQueryable().Where(x => x.GuildId == guildId && x.UserId == userId)
            .ForEachAsync(x =>
            {
                if (x.Forgiven) return;
                x.Forgiven = true;
                x.ForgivenBy = mod;
            });
    }

    /// <summary>
    ///     Retrieves all warnings for a specific guild.
    /// </summary>
    /// <param name="set">The DbSet of Warning entities to query.</param>
    /// <param name="id">The ID of the guild.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains an IEnumerable of Warning entities
    ///     for the specified guild.
    /// </returns>
    public static async Task<IEnumerable<Warning>> GetForGuild(this DbSet<Warning> set, ulong id)
    {
        return await set.AsQueryable().Where(x => x.GuildId == id).ToArrayAsyncEF();
    }
}