using Lavalink4NET.Players;
using Mewdeko.Modules.Music.CustomPlayer;

namespace Mewdeko.Modules.Music.Common;

/// <summary>
///     Shared logic for stepping back to the previous track in the queue.
/// </summary>
public static class MusicPrevious
{
    /// <summary>
    ///     Plays the track before the current one, if there is one.
    /// </summary>
    /// <param name="cache">The data cache holding the queue.</param>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="player">The guild's player.</param>
    /// <returns>True if the previous track started playing.</returns>
    public static async Task<bool> PlayPreviousAsync(IDataCache cache, ulong guildId, MewdekoPlayer player)
    {
        var queue = await cache.GetMusicQueue(guildId);
        var currentTrack = await cache.GetCurrentTrack(guildId);
        if (currentTrack is not { Index: > 1 })
            return false;

        var prevTrack = queue.FirstOrDefault(x => x.Index == currentTrack.Index - 1);
        if (prevTrack == null)
            return false;

        await player.PlayAsync(prevTrack.Track);
        await cache.SetCurrentTrack(guildId, prevTrack);
        return true;
    }
}
