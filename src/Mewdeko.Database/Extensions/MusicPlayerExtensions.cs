using Mewdeko.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Database.Extensions;

public static class MusicPlayerSettingsExtensions
{
    public static async Task<MusicPlayerSettings> ForGuildAsync(this DbSet<MusicPlayerSettings> settings, ulong guildId)
    {
        var toReturn = await settings
            .AsQueryable()
            .FirstOrDefaultAsync(x => x.GuildId == guildId).ConfigureAwait(false);

        if (toReturn is not null) return toReturn;
        var newSettings = new MusicPlayerSettings
        {
            GuildId = guildId, PlayerRepeat = PlayerRepeatType.Queue
        };

        await settings.AddAsync(newSettings).ConfigureAwait(false);
        return newSettings;
    }
}