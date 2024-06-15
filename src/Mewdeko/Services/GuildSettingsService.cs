using Mewdeko.Services.Settings;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Mewdeko.Services
{
    /// <summary>
    /// Service for managing guild settings.
    /// </summary>
    public class GuildSettingsService(
        DbService db,
        IConfigService? bss,
        IServiceProvider services,
        IDataCache cache)
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

            await using var uow = db.GetDbContext();
            var toLoad = uow.GuildConfigs.IncludeEverything().FirstOrDefault(x => x.GuildId == guildId);
            if (toLoad is not null) return toLoad;
            {
                await uow.ForGuildId(guildId);
                toLoad = uow.GuildConfigs.IncludeEverything().FirstOrDefault(x => x.GuildId == guildId);
            }

            return toLoad;
        }

        /// <summary>
        /// Updates the guild configuration.
        /// </summary>
        public async Task UpdateGuildConfig(ulong _, GuildConfig toUpdate)
        {
            await using var uow = db.GetDbContext();
            try
            {
                    uow.GuildConfigs.Update(toUpdate);
                    await uow.SaveChangesAsync();
            }
            catch (Exception e)
            {
                Log.Error(e, "There was an issue updating a GuildConfig");
            }
        }
    }
}