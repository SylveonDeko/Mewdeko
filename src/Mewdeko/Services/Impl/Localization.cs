using System.Globalization;
using System.IO;
using Mewdeko.Services.Settings;
using Newtonsoft.Json;

namespace Mewdeko.Services.Impl
{
    /// <summary>
    /// Provides functionality for managing localization settings and retrieving localized data.
    /// </summary>
    public class Localization : ILocalization
    {
        private static readonly Dictionary<string, CommandData> CommandData =
            JsonConvert.DeserializeObject<Dictionary<string, CommandData>>(
                File.ReadAllText("./data/strings/commands/commands.en-US.json"));

        private readonly BotConfigService bss;
        private readonly DbService db;

        /// <summary>
        /// Initializes a new instance of the <see cref="Localization"/> class.
        /// </summary>
        /// <param name="bss">The bot configuration service.</param>
        /// <param name="db">The database service.</param>
        /// <param name="bot">The bot instance.</param>
        public Localization(BotConfigService bss, DbService db, Mewdeko bot)
        {
            this.bss = bss;
            this.db = db;
            using var uow = db.GetDbContext();
            var allgc = bot.AllGuildConfigs;
            var cultureInfoNames = allgc
                .ToDictionary(x => x.GuildId, x => x.Locale);

            GuildCultureInfos = new ConcurrentDictionary<ulong, CultureInfo?>(cultureInfoNames.ToDictionary(x => x.Key,
                x =>
                {
                    var cultureInfo = new CultureInfo("en-US");
                    try
                    {
                        switch (x.Value)
                        {
                            case null:
                                return null;
                            case "english":
                                cultureInfo = new CultureInfo("en-US");
                                break;
                            default:
                                try
                                {
                                    cultureInfo = new CultureInfo(x.Value);
                                }
                                catch
                                {
                                    cultureInfo = new CultureInfo("en-US");
                                }

                                break;
                        }
                    }
                    catch
                    {
                        // ignored
                    }

                    return cultureInfo;
                }).Where(x => x.Value != null));
        }

        /// <summary>
        /// Gets the localized command data for the specified command.
        /// </summary>
        private ConcurrentDictionary<ulong, CultureInfo?> GuildCultureInfos { get; }

        /// <inheritdoc/>
        public CultureInfo? DefaultCultureInfo => bss.Data.DefaultLocale;

        /// <inheritdoc/>
        public void SetGuildCulture(IGuild guild, CultureInfo? ci) => SetGuildCulture(guild.Id, ci);

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

            await using (var uow = db.GetDbContext())
            {
                var gc = await uow.ForGuildId(guildId, set => set);
                gc.Locale = ci.Name;
                await uow.SaveChangesAsync().ConfigureAwait(false);
            }

            GuildCultureInfos.AddOrUpdate(guildId, ci, (_, _) => ci);
        }

        /// <inheritdoc/>
        public void RemoveGuildCulture(IGuild guild) => RemoveGuildCulture(guild.Id);

        /// <summary>
        /// Removes the culture info for the specified guild.
        /// </summary>
        /// <param name="guildId">The ID of the guild.</param>
        public async void RemoveGuildCulture(ulong guildId)
        {
            if (!GuildCultureInfos.TryRemove(guildId, out _)) return;
            await using var uow = db.GetDbContext();
            var gc = await uow.ForGuildId(guildId, set => set);
            gc.Locale = null;
            await uow.SaveChangesAsync().ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public void SetDefaultCulture(CultureInfo? ci) => bss.ModifyConfig(bs => bs.DefaultLocale = ci);

        /// <inheritdoc/>
        public void ResetDefaultCulture() => SetDefaultCulture(CultureInfo.CurrentCulture);

        /// <inheritdoc/>
        public CultureInfo? GetCultureInfo(IGuild? guild) => GetCultureInfo(guild?.Id);

        /// <inheritdoc/>
        public CultureInfo? GetCultureInfo(ulong? guildId)
        {
            if (guildId is null || !GuildCultureInfos.TryGetValue(guildId.Value, out var info) || info is null)
                return bss.Data.DefaultLocale;

            return info;
        }
    }
}