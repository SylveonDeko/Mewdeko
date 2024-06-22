using System.Globalization;
using Mewdeko.Services.Settings;

namespace Mewdeko.Services.Impl
{
    /// <summary>
    /// Provides functionality for managing localization settings and retrieving localized data.
    /// </summary>
    public class Localization(BotConfigService bss, MewdekoContext dbContext, GuildSettingsService service)
        : ILocalization
    {
        /// <inheritdoc/>
        public CultureInfo? DefaultCultureInfo => bss.Data.DefaultLocale;

        /// <inheritdoc/>
        public void SetGuildCulture(IGuild guild, CultureInfo? ci) => SetGuildCulture(guild.Id, ci);

        /// <inheritdoc />
        public void RemoveGuildCulture(IGuild guild)
        {
            RemoveGuildCulture(guild.Id);
        }

        /// <inheritdoc />
        public void SetDefaultCulture(CultureInfo? ci)
        {
            bss.ModifyConfig(bs => bs.DefaultLocale = ci);
        }

        /// <inheritdoc />
        public void ResetDefaultCulture()
        {
            SetDefaultCulture(CultureInfo.CurrentCulture);
        }

        /// <inheritdoc />
        public CultureInfo? GetCultureInfo(IGuild? guild)
        {
            return GetCultureInfo(guild?.Id);
        }

        /// <inheritdoc />
        public CultureInfo? GetCultureInfo(ulong? guildId)
        {
            if (!guildId.HasValue)
                return DefaultCultureInfo;

            var guildConfig = service.GetGuildConfig(guildId.Value).Result;

            return guildConfig.Locale == null ? DefaultCultureInfo : new CultureInfo(guildConfig.Locale);
        }

        /// <summary>
        /// Sets the culture info for the specified guild.
        /// </summary>
        /// <param name="guildId">The ID of the guild.</param>
        /// <param name="ci">The culture info to set.</param>
        private async void SetGuildCulture(ulong guildId, CultureInfo? ci)
        {
            if (ci?.Name == bss.Data.DefaultLocale?.Name)
            {
                RemoveGuildCulture(guildId);
                return;
            }


            var gc = await dbContext.ForGuildId(guildId, set => set);
            gc.Locale = ci.Name;
            await service.UpdateGuildConfig(guildId, gc);
        }

        /// <summary>
        /// Removes the culture info for the specified guild.
        /// </summary>
        /// <param name="guildId">The ID of the guild.</param>
        private async void RemoveGuildCulture(ulong guildId)
        {

            var gc = await dbContext.ForGuildId(guildId, set => set);
            gc.Locale = null;
            await service.UpdateGuildConfig(guildId, gc);
        }
    }
}