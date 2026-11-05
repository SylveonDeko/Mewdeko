using System.Text;
using Mewdeko.Common.Cards;
using Mewdeko.Services.Strings;

namespace Mewdeko.Modules.MultiGreets.Common;

/// <summary>
///     Builds the list of background presets shown by the preset commands.
/// </summary>
public static class CardPresetEmbeds
{
    private const int MaxDescription = 3800;

    /// <summary>
    ///     Lists the categories, or the presets of one category.
    /// </summary>
    /// <param name="strings">The localized strings.</param>
    /// <param name="guildId">The guild ID, for the language.</param>
    /// <param name="catalog">The catalog.</param>
    /// <param name="category">A category id, or null to list the categories.</param>
    /// <param name="command">How to run the command, shown in the hint.</param>
    /// <returns>The embed, or null when the category doesn't exist.</returns>
    public static Discord.Embed? Build(GeneratedBotStrings strings, ulong guildId, CardPresetCatalog catalog, string? category, string command)
    {
        var embed = new EmbedBuilder().WithOkColor().WithTitle(strings.CardpresetsTitle(guildId));
        if (catalog.Presets.Count == 0)
            return embed.WithDescription(strings.CardpresetsEmpty(guildId)).Build();

        var text = new StringBuilder();
        if (string.IsNullOrWhiteSpace(category))
        {
            foreach (var c in catalog.Categories)
            {
                var count = catalog.Presets.Count(p => p.Category == c.Id);
                if (count > 0)
                    text.AppendLine(strings.CardpresetsCategoryLine(guildId, c.Id, c.Name, count));
            }

            text.AppendLine().Append(strings.CardpresetsHint(guildId, command));
            return embed.WithDescription(text.ToString()).Build();
        }

        var id = category.Trim().ToLowerInvariant();
        var match = catalog.Categories.FirstOrDefault(c => c.Id == id);
        if (match is null)
            return null;

        var presets = catalog.Presets.Where(p => p.Category == id).ToList();
        var shown = 0;
        foreach (var preset in presets)
        {
            var line = strings.CardpresetsPresetLine(guildId, preset.Id, preset.Name);
            if (text.Length + line.Length > MaxDescription)
                break;
            text.AppendLine(line);
            shown++;
        }

        if (shown < presets.Count)
            text.Append(strings.CardpresetsMore(guildId, presets.Count - shown));
        return embed.WithTitle($"{strings.CardpresetsTitle(guildId)}: {match.Name}")
            .WithDescription(text.ToString()).Build();
    }
}
