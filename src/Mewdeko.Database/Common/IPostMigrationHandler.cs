namespace Mewdeko.Database.Common;

public interface IPostMigrationHandler
{
    static abstract Task PostMigrationHandler(string id, MewdekoContext dbContext);
}