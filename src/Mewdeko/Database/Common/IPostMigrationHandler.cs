namespace Mewdeko.Database.Common;

/// <summary>
/// Defines a contract for handling post-migration operations.
/// </summary>
public interface IPostMigrationHandler
{
    /// <summary>
    /// Handles post-migration operations.
    /// </summary>
    /// <param name="id">The identifier for the migration.</param>
    /// <param name="context">The MewdekoContext instance representing the database context.</param>
    /// <returns>A Task representing the asynchronous operation.</returns>
    static abstract Task PostMigrationHandler(string id, MewdekoContext context);
}