using System.Globalization;

namespace Mewdeko.Services;

public interface ILocalization : INService
{
    CultureInfo? DefaultCultureInfo { get; }

    CultureInfo? GetCultureInfo(IGuild guild);
    CultureInfo? GetCultureInfo(ulong? guildId);
    void RemoveGuildCulture(IGuild guild);
    void ResetDefaultCulture();
    void SetDefaultCulture(CultureInfo? ci);
    void SetGuildCulture(IGuild guild, CultureInfo? ci);
}