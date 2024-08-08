using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Database.Extensions;

/// <summary>
/// Provides extension methods for database operations on Entity Framework Core DbSet.
/// </summary>
public static class DbExtensions
{
    /// <summary>
    /// Retrieves an entity by its ID from the specified DbSet asynchronously.
    /// </summary>
    /// <typeparam name="T">The type of the entity, which must inherit from DbEntity.</typeparam>
    /// <param name="set">The DbSet to query.</param>
    /// <param name="id">The ID of the entity to retrieve.</param>
    /// <returns>
    /// A task that represents the asynchronous operation.
    /// The task result contains the entity with the specified ID, or null if no such entity is found.
    /// </returns>
    public static async Task<T> GetById<T>(this DbSet<T> set, int id) where T : DbEntity
        => await set.FirstOrDefaultAsync(x => x.Id == id);
}