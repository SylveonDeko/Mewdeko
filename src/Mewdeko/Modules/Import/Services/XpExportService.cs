using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Modules.Import.Common;
using Mewdeko.Modules.Xp.Models;
using Mewdeko.Modules.Xp.Services;

namespace Mewdeko.Modules.Import.Services;

/// <summary>
///     Exports every member's XP in a server so it can be backed up and restored with the XP import.
/// </summary>
/// <param name="dbFactory">The database connection factory.</param>
/// <param name="xpService">The XP service.</param>
public class XpExportService(IDataConnectionFactory dbFactory, XpService xpService) : INService
{
    /// <summary>
    ///     Builds the export file for a server.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>The file text.</returns>
    public async Task<string> ExportAsync(ulong guildId)
    {
        var settings = await xpService.GetGuildXpSettingsAsync(guildId);
        await using var db = await dbFactory.CreateConnectionAsync();
        var rows = await db.GuildUserXps.Where(x => x.GuildId == guildId && (x.TotalXp > 0 || x.BonusXp > 0))
            .ToListAsync();
        return XpExport.Serialize(guildId, (XpCurveType)settings.XpCurveType, rows);
    }
}
