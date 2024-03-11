using Mewdeko.Database.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace Mewdeko.Database.Extensions;

public static class DbExtensions
{
    public static Task<T> GetById<T>(this DbSet<T> set, int id) where T : DbEntity
        => set.FirstOrDefaultAsync(x => x.Id == id);

    public static IEnumerable<GuildConfig> GetActiveConfigs(this DbSet<GuildConfig> set, IEnumerable<ulong> guildIds)
    {
        var parameters = new List<object>();
        var ids = guildIds.Select((id, index) =>
        {
            // Safe only if id is guaranteed to be less than 9223372036854775808 (long.MaxValue)
            var parameter = new NpgsqlParameter($"@p{index}", NpgsqlDbType.Bigint)
            {
                Value = unchecked((long)id)
            };
            parameters.Add(parameter);
            return $"@p{index}";
        });

        var sqlQuery =
            $"SELECT * FROM \"GuildConfigs\" WHERE \"GuildId\" = ANY (ARRAY[{string.Join(",", ids)}]::bigint[])";

        return set.FromSqlRaw(sqlQuery, parameters.ToArray());
    }
}