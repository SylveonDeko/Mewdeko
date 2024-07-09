using Microsoft.EntityFrameworkCore;
using Serilog;

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
    }
}