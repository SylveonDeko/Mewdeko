using Lavalink4NET.Rest.Entities.Tracks;

namespace Mewdeko.Modules.Music.Common;

/// <summary>
///     Maps user facing source names and Lavalink search prefixes to <see cref="TrackSearchMode" /> values.
/// </summary>
public static class MusicSearchSources
{
    private static readonly Dictionary<string, TrackSearchMode> Modes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["youtube"] = TrackSearchMode.YouTube,
        ["yt"] = TrackSearchMode.YouTube,
        ["ytsearch"] = TrackSearchMode.YouTube,
        ["youtubemusic"] = TrackSearchMode.YouTubeMusic,
        ["ytm"] = TrackSearchMode.YouTubeMusic,
        ["ytmsearch"] = TrackSearchMode.YouTubeMusic,
        ["soundcloud"] = TrackSearchMode.SoundCloud,
        ["sc"] = TrackSearchMode.SoundCloud,
        ["scsearch"] = TrackSearchMode.SoundCloud,
        ["bandcamp"] = TrackSearchMode.Bandcamp,
        ["bc"] = TrackSearchMode.Bandcamp,
        ["bcsearch"] = TrackSearchMode.Bandcamp,
        ["spotify"] = TrackSearchMode.Spotify,
        ["sp"] = TrackSearchMode.Spotify,
        ["spsearch"] = TrackSearchMode.Spotify,
        ["applemusic"] = TrackSearchMode.AppleMusic,
        ["apple"] = TrackSearchMode.AppleMusic,
        ["am"] = TrackSearchMode.AppleMusic,
        ["amsearch"] = TrackSearchMode.AppleMusic,
        ["deezer"] = TrackSearchMode.Deezer,
        ["dz"] = TrackSearchMode.Deezer,
        ["dzsearch"] = TrackSearchMode.Deezer,
        ["yandexmusic"] = TrackSearchMode.YandexMusic,
        ["yandex"] = TrackSearchMode.YandexMusic,
        ["ym"] = TrackSearchMode.YandexMusic,
        ["ymsearch"] = TrackSearchMode.YandexMusic,
        ["vkmusic"] = new TrackSearchMode("vksearch"),
        ["vk"] = new TrackSearchMode("vksearch"),
        ["vksearch"] = new TrackSearchMode("vksearch"),
        ["tidal"] = new TrackSearchMode("tdsearch"),
        ["td"] = new TrackSearchMode("tdsearch"),
        ["tdsearch"] = new TrackSearchMode("tdsearch"),
        ["qobuz"] = new TrackSearchMode("qbsearch"),
        ["qb"] = new TrackSearchMode("qbsearch"),
        ["qbsearch"] = new TrackSearchMode("qbsearch"),
        ["flowerytts"] = new TrackSearchMode("ftts"),
        ["ftts"] = new TrackSearchMode("ftts")
    };

    /// <summary>
    ///     The source names accepted by <see cref="TryParse" />, suitable for showing to users.
    /// </summary>
    public static readonly IReadOnlyList<string> KnownSources =
    [
        "youtube", "youtubemusic", "soundcloud", "bandcamp", "spotify", "applemusic", "deezer", "yandexmusic",
        "vkmusic", "tidal", "qobuz", "flowerytts"
    ];

    /// <summary>
    ///     Resolves a source name to its search mode.
    /// </summary>
    /// <param name="name">The source name, alias or raw Lavalink prefix (with or without the trailing colon).</param>
    /// <param name="mode">The resolved search mode.</param>
    /// <returns>Whether the name was recognised.</returns>
    public static bool TryParse(string? name, out TrackSearchMode mode)
    {
        mode = TrackSearchMode.None;
        if (string.IsNullOrWhiteSpace(name))
            return false;

        return Modes.TryGetValue(name.Trim().TrimEnd(':'), out mode);
    }

    /// <summary>
    ///     Resolves the configured default search source, falling back to YouTube when the value is unknown.
    /// </summary>
    /// <param name="credentials">The bot credentials holding the configured default.</param>
    /// <returns>The default search mode.</returns>
    public static TrackSearchMode GetDefault(IBotCredentials credentials)
    {
        return TryParse(credentials.LavalinkDefaultSearchSource, out var mode) ? mode : TrackSearchMode.YouTube;
    }

    /// <summary>
    ///     Splits a query of the form "source: terms" or "dzsearch:terms" into its search mode and the remaining
    ///     query. Queries without a recognised prefix are returned unchanged with the given default mode.
    /// </summary>
    /// <param name="query">The raw user query.</param>
    /// <param name="defaultMode">The mode to use when no prefix is present.</param>
    /// <returns>The search mode and the query text to search for.</returns>
    public static (TrackSearchMode Mode, string Query) Resolve(string query, TrackSearchMode defaultMode)
    {
        var trimmed = query.Trim();
        var colon = trimmed.IndexOf(':');
        if (colon <= 0)
            return (defaultMode, trimmed);

        var prefix = trimmed[..colon];
        if (prefix.Any(char.IsWhiteSpace) || !TryParse(prefix, out var mode))
            return (defaultMode, trimmed);

        var rest = trimmed[(colon + 1)..].Trim();
        return rest.Length == 0 ? (defaultMode, trimmed) : (mode, rest);
    }

    /// <summary>
    ///     Whether the query carries a recognised source prefix such as "deezer:" or "spsearch:".
    /// </summary>
    /// <param name="query">The raw user query.</param>
    /// <returns>True when a source prefix is present.</returns>
    public static bool HasSourcePrefix(string query)
    {
        return Resolve(query, TrackSearchMode.None).Mode != TrackSearchMode.None;
    }
}