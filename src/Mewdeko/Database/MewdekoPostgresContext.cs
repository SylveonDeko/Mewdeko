using Mewdeko.Services.Impl;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Database
{
    /// <summary>
    /// Represents the database context for Mewdeko using PostgreSQL.
    /// </summary>
    public class MewdekoPostgresContext : MewdekoContext
    {
        /// <summary>
        /// Context use for psql
        /// </summary>
        /// <param name="options"></param>
        public MewdekoPostgresContext(DbContextOptions<MewdekoPostgresContext> options) : base(options)
        {

        }

        /// <inheritdoc />
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            var credentials = new BotCredentials();
            optionsBuilder
                .UseNpgsql(credentials.PsqlConnectionString,
                    x => x.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
                .EnableDetailedErrors()
                .EnableSensitiveDataLogging();
        }
    }
}