using System.Text.Json;
using DataModel;
using Mewdeko.Modules.Xp.Models;
using Mewdeko.Modules.Xp.Services;

namespace Mewdeko.Modules.Import.Common;

/// <summary>
///     Writes a server's XP to a file that the XP import reads back.
/// </summary>
public static class XpExport
{
    /// <summary>
    ///     The format name written into every export.
    /// </summary>
    public const string Format = "mewdeko-xp";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true
    };

    /// <summary>
    ///     Serialises member XP into the export file text.
    /// </summary>
    /// <param name="guildId">The guild the XP belongs to.</param>
    /// <param name="curve">The level curve the guild uses.</param>
    /// <param name="rows">The members' XP rows.</param>
    /// <returns>The file text.</returns>
    public static string Serialize(ulong guildId, XpCurveType curve, IEnumerable<GuildUserXp> rows)
    {
        return JsonSerializer.Serialize(new
        {
            format = Format,
            guildId = guildId.ToString(),
            curve = curve.ToString(),
            exportedAt = DateTime.UtcNow,
            members = rows
                .OrderByDescending(x => x.TotalXp)
                .Select(x => new
                {
                    userId = x.UserId.ToString(),
                    xp = x.TotalXp,
                    level = XpCalculator.CalculateLevel(x.TotalXp, curve),
                    bonusXp = x.BonusXp
                })
        }, Options);
    }
}
