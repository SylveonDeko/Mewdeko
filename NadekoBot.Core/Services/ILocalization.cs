using System.Collections.Concurrent;
using System.Globalization;
using Discord;

namespace NadekoBot.Core.Services
{
    public interface ILocalization : INService
    {
        CultureInfo DefaultCultureInfo { get; }
        ConcurrentDictionary<ulong, CultureInfo> GuildCultureInfos { get; }

        CultureInfo GetCultureInfo(IGuild guild);
        CultureInfo GetCultureInfo(ulong? guildId);
        void RemoveGuildCulture(IGuild guild);
        void RemoveGuildCulture(ulong guildId);
        void ResetDefaultCulture();
        void SetDefaultCulture(CultureInfo ci);
        void SetGuildCulture(IGuild guild, CultureInfo ci);
        void SetGuildCulture(ulong guildId, CultureInfo ci);
    }
}