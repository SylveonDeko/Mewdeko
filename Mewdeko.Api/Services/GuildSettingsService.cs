using Mewdeko.Database;
using Mewdeko.Database.Extensions;
using Mewdeko.Database.Models;

namespace Mewdeko.Api.Services;

/// <summary>
/// Service for managing guild settings.
/// </summary>
public class GuildSettingsService(
    DbService db,
    RedisCache cache)
{
    /// <summary>
    /// Gets the guild configuration for the specified guild ID.
    /// </summary>
    public async Task<GuildConfig> GetGuildConfig(ulong guildId)
    {
        var configExists = await cache.GetGuildConfigCache(guildId);
        if (configExists != null)
            return configExists;

        await using var uow = db.GetDbContext();
        var toLoad = uow.GuildConfigs.IncludeEverything().FirstOrDefault(x => x.GuildId == guildId);
        if (toLoad is null)
        {
            await uow.ForGuildId(guildId);
            toLoad = uow.GuildConfigs.IncludeEverything().FirstOrDefault(x => x.GuildId == guildId);
        }

        await cache.SetGuildConfigCache(guildId, toLoad!);
        return toLoad!;
    }

    /// <summary>
    /// Updates the guild configuration.
    /// </summary>
    public async Task UpdateGuildConfig(ulong guildId, GuildConfig toUpdate)
    {
        await using var uow = db.GetDbContext();
        var exists = await cache.GetGuildConfigCache(guildId);

        if (exists is not null)
        {
            var properties = typeof(GuildConfig).GetProperties();
            foreach (var property in properties)
            {
                var oldValue = property.GetValue(exists);
                var newValue = property.GetValue(toUpdate);

                if (newValue != null && !newValue.Equals(oldValue))
                {
                    property.SetValue(exists, newValue);
                }
            }

            await cache.SetGuildConfigCache(guildId, exists);
            uow.GuildConfigs.Update(exists);
            await uow.SaveChangesAsync();
        }
        else
        {
            var config = uow.GuildConfigs.IncludeEverything().FirstOrDefault(x => x.Id == toUpdate.Id);

            if (config != null)
            {
                var properties = typeof(GuildConfig).GetProperties();
                foreach (var property in properties)
                {
                    var oldValue = property.GetValue(config);
                    var newValue = property.GetValue(toUpdate);

                    if (newValue != null && !newValue.Equals(oldValue))
                    {
                        property.SetValue(config, newValue);
                    }
                }

                await cache.SetGuildConfigCache(guildId, config);
                uow.GuildConfigs.Update(config);
                await uow.SaveChangesAsync();
            }
        }
    }
}