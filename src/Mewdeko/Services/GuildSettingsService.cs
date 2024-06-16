using Mewdeko.Services.Settings;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using ZiggyCreatures.Caching.Fusion;

namespace Mewdeko.Services
{
    /// <summary>
    /// Service for managing guild settings.
    /// </summary>
    public class GuildSettingsService(

        DbService db,
        IConfigService? bss,
        IServiceProvider services,
        IFusionCache cache)
    {

        private ConcurrentDictionary<ulong, GuildConfig> AllGuildConfigs { get; set; }

        /// <summary>
        /// Sets the prefix for the specified guild.
        /// </summary>
        public async Task<string> SetPrefix(IGuild guild, string prefix)
        {
            if (string.IsNullOrWhiteSpace(prefix))
                throw new ArgumentNullException(nameof(prefix));
            ArgumentNullException.ThrowIfNull(guild);

            var config = await GetGuildConfig(guild.Id);
            config.Prefix = prefix;
            await UpdateGuildConfig(guild.Id, config);
            return prefix;
        }

        /// <summary>
        /// Gets the prefix for the specified guild.
        /// </summary>
        public Task<string?> GetPrefix(IGuild? guild) => GetPrefix(guild?.Id);

        /// <summary>
        /// Gets the prefix for the guild with the specified ID.
        /// </summary>
        public async Task<string?> GetPrefix(ulong? id)
        {
            bss = services.GetRequiredService<BotConfigService>();
            if (!id.HasValue)
                return bss.GetSetting("prefix");
            var prefix = (await GetGuildConfig(id.Value)).Prefix;
            return string.IsNullOrWhiteSpace(prefix) ? bss.GetSetting("prefix") : prefix;
        }

        /// <summary>
        /// Gets the default prefix.
        /// </summary>
        public Task<string?> GetPrefix() => Task.FromResult(bss.GetSetting("prefix"));

        /// <summary>
        /// Gets the guild configuration for the specified guild ID.
        /// </summary>
        public async Task<GuildConfig> GetGuildConfig(ulong guildId)
        {
            var configExists = await cache.TryGetAsync<GuildConfig>($"guildconfig_{guildId}");
            if (configExists.HasValue)
                return configExists;

            await using var uow = db.GetDbContext();
            var toLoad = uow.GuildConfigs.IncludeEverything().FirstOrDefault(x => x.GuildId == guildId);
            if (toLoad is null)
            {
                await uow.ForGuildId(guildId);
                toLoad = uow.GuildConfigs.IncludeEverything().FirstOrDefault(x => x.GuildId == guildId);
            }

            await cache.SetAsync($"guildconfig_{guildId}", toLoad);
            return toLoad;
        }

        /// <summary>
        /// Updates the guild configuration.
        /// </summary>
        public async Task UpdateGuildConfig(ulong guildId, GuildConfig toUpdate)
        {
            try
            {
                await using var uow = db.GetDbContext();
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

                        await cache.SetAsync($"guildconfig_{guildId}", config);
                        uow.GuildConfigs.Update(config);
                        await uow.SaveChangesAsync();
                    }
            }
            catch (Exception e)
            {
                Log.Error(e, "There was an issue updating a GuildConfig");
            }
        }
    }
}