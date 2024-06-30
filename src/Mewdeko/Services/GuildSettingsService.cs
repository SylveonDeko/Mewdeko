using System.Diagnostics;
using System.Runtime.CompilerServices;
using LinqToDB.EntityFrameworkCore;
using Mewdeko.Services.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using ZiggyCreatures.Caching.Fusion;

namespace Mewdeko.Services
{
    /// <summary>
    /// Service for managing guild settings.
    /// </summary>
    public class GuildSettingsService(

        MewdekoContext dbContext,
        IConfigService? bss,
        IServiceProvider services,
        IFusionCache cache)
    {

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
        public async Task<string?> GetPrefix(IGuild? guild) => await GetPrefix(guild?.Id);

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
        public async Task<GuildConfig> GetGuildConfig(ulong guildId, [CallerMemberName] string callerName = "", [CallerFilePath] string filePath = "")
        {
            try
            {
                var sw = new Stopwatch();
                sw.Start();
                var configExists = await cache.TryGetAsync<GuildConfig>($"guildconfig_{guildId}");
                if (configExists.HasValue)
                    return configExists;

                var toLoad = await dbContext.GuildConfigs.FirstOrDefaultAsync(x => x.GuildId == guildId);
                if (toLoad is null)
                {
                    await dbContext.ForGuildId(guildId);
                    toLoad = await dbContext.GuildConfigs.FirstOrDefaultAsync(x => x.GuildId == guildId);
                }

                await cache.SetAsync($"guildconfig_{guildId}", toLoad);
                sw.Stop();
                Log.Information($"GuildConfig Get for {guildId} took {sw.Elapsed}");
                return toLoad;
            }
            catch (Exception e)
            {
                Log.Information($"Executing from {callerName} at {filePath}");
                Log.Information(e.Message, "Failed to get guild config");
                throw;
            }
        }

        /// <summary>
        /// Updates the guild configuration.
        /// </summary>
        public async Task UpdateGuildConfig(ulong guildId, GuildConfig toUpdate)
        {
            var sw = new Stopwatch();
            sw.Start();
            try
            {
                var config = await dbContext.GuildConfigs.FirstOrDefaultAsync(x => x.Id == toUpdate.Id);
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
                        dbContext.GuildConfigs.Update(config);
                        await dbContext.SaveChangesAsync();
                        sw.Stop();
                        Log.Information($"GuildConfig Set for {guildId} took {sw.Elapsed}");
                    }
            }
            catch (Exception e)
            {
                sw.Stop();
                Log.Error(e, "There was an issue updating a GuildConfig");
            }
        }
    }
}