using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Mewdeko.Database;

public interface IDbContextFactory
{
    MewdekoPostgresContext CreateDbContext();
}

public class DbContextFactory(string connectionString) : IDbContextFactory
{
    public MewdekoPostgresContext CreateDbContext()
    {
        Log.Information("New Context Created");
        return new MewdekoPostgresContext(new DbContextOptions<MewdekoPostgresContext>());
    }
}