using System.Net.Http;
using System.Text.Json;
using System.Threading;
using DataModel;
using IF.Lastfm.Core.Api;
using IF.Lastfm.Core.Api.Enums;
using IF.Lastfm.Core.Api.Helpers;
using IF.Lastfm.Core.Objects;
using LinqToDB;
using LinqToDB.Async;

namespace Mewdeko.Modules.Music.Services;

/// <summary>
///     A linked guild member's Last.fm account, paired with their Discord user.
/// </summary>
/// <param name="Member">The Discord guild member.</param>
/// <param name="LastFmUser">Their linked Last.fm account.</param>
public record LinkedLastFmMember(IGuildUser Member, LastFmUser LastFmUser);

/// <summary>
///     A user's play count for a specific artist, used for whoknows/crowns ranking.
/// </summary>
/// <param name="Username">The Last.fm username.</param>
/// <param name="Playcount">The number of scrobbles for the artist.</param>
public record ArtistPlaycount(string Username, int Playcount);

/// <summary>
///     A track entry returned from the raw user.getTopTracks endpoint, which the installed
///     Last.fm client library does not expose a typed method for.
/// </summary>
/// <param name="Name">The track name.</param>
/// <param name="Artist">The artist name.</param>
/// <param name="Playcount">The user's play count for the track.</param>
/// <param name="Url">The Last.fm URL for the track.</param>
/// <param name="ImageUrl">The largest available track/artist image URL, if any.</param>
public record LastFmTopTrack(string Name, string Artist, int Playcount, string Url, string? ImageUrl);

/// <summary>
///     Provides access to Last.fm listening statistics for linked Mewdeko users, on top of the
///     scrobbling support in the music module.
/// </summary>
public class LastFmStatsService(
    IBotCredentials creds,
    IDataConnectionFactory dbFactory,
    IHttpClientFactory httpClientFactory,
    ILogger<LastFmStatsService> logger) : INService
{
    /// <summary>
    ///     Creates a new Last.fm API client using the bot's configured credentials.
    /// </summary>
    public LastfmClient CreateClient()
    {
        return new LastfmClient(creds.LastFmApiKey, creds.LastFmApiSecret);
    }

    /// <summary>
    ///     Gets the linked Last.fm account for a Discord user, if any.
    /// </summary>
    /// <param name="userId">The Discord user's id.</param>
    public async Task<LastFmUser?> GetLinkedUserAsync(ulong userId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        return await db.GetTable<LastFmUser>().FirstOrDefaultAsync(x => x.UserId == userId);
    }

    /// <summary>
    ///     Gets every guild member who currently has a Last.fm account linked.
    /// </summary>
    /// <param name="guild">The guild to check members of.</param>
    public async Task<List<LinkedLastFmMember>> GetLinkedGuildMembersAsync(IGuild guild)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var linked = await db.GetTable<LastFmUser>().ToListAsync();
        if (linked.Count == 0)
            return [];

        var linkedById = linked.ToDictionary(x => x.UserId);
        var members = await guild.GetUsersAsync();

        var result = new List<LinkedLastFmMember>();
        foreach (var member in members)
        {
            if (linkedById.TryGetValue(member.Id, out var lastFmUser))
                result.Add(new LinkedLastFmMember(member, lastFmUser));
        }

        return result;
    }

    /// <summary>
    ///     Parses a user-supplied time period argument into the values needed by the various
    ///     Last.fm APIs, accepting a handful of friendly aliases.
    /// </summary>
    /// <param name="period">The raw period argument, e.g. "week", "1month", "overall".</param>
    public (LastStatsTimeSpan Span, string RawPeriod, string DisplayName) ParsePeriod(string? period)
    {
        return period?.ToLowerInvariant() switch
        {
            "7day" or "week" or "weekly" => (LastStatsTimeSpan.Week, "7day", "7 days"),
            "1month" or "month" or "monthly" => (LastStatsTimeSpan.Month, "1month", "1 month"),
            "3month" or "quarter" => (LastStatsTimeSpan.Quarter, "3month", "3 months"),
            "6month" or "half" => (LastStatsTimeSpan.Half, "6month", "6 months"),
            "12month" or "year" or "yearly" => (LastStatsTimeSpan.Year, "12month", "1 year"),
            _ => (LastStatsTimeSpan.Overall, "overall", "all time")
        };
    }

    /// <summary>
    ///     Gets a user's profile info from Last.fm.
    /// </summary>
    public async Task<LastResponse<LastUser>> GetUserInfoAsync(string username)
    {
        return await CreateClient().User.GetInfoAsync(username);
    }

    /// <summary>
    ///     Gets a user's recent (or currently playing) tracks.
    /// </summary>
    public async Task<PageResponse<LastTrack>> GetRecentTracksAsync(string username, int count = 10)
    {
        return await CreateClient().User.GetRecentScrobbles(username, null, null, true, 1, Math.Min(count, 50));
    }

    /// <summary>
    ///     Gets a user's top artists for the given period.
    /// </summary>
    public async Task<PageResponse<LastArtist>> GetTopArtistsAsync(string username, LastStatsTimeSpan span,
        int count = 10)
    {
        return await CreateClient().User.GetTopArtists(username, span, 1, Math.Min(count, 50));
    }

    /// <summary>
    ///     Gets a user's top albums for the given period.
    /// </summary>
    public async Task<PageResponse<LastAlbum>> GetTopAlbumsAsync(string username, LastStatsTimeSpan span,
        int count = 10)
    {
        return await CreateClient().User.GetTopAlbums(username, span, 1, Math.Min(count, 50));
    }

    /// <summary>
    ///     Gets full Last.fm info for an artist (bio, global stats, tags).
    /// </summary>
    public async Task<LastResponse<LastArtist>> GetArtistInfoAsync(string artist)
    {
        return await CreateClient().Artist.GetInfoAsync(artist);
    }

    /// <summary>
    ///     Gets full Last.fm info for an album, including a specific user's play count for it.
    /// </summary>
    public async Task<LastResponse<LastAlbum>> GetAlbumInfoAsync(string artist, string album, string? username)
    {
        return await CreateClient().Album.GetInfoAsync(artist, album, username: username ?? string.Empty);
    }

    /// <summary>
    ///     Gets full Last.fm info for a track, including a specific user's play count for it.
    /// </summary>
    public async Task<LastResponse<LastTrack>> GetTrackInfoAsync(string track, string artist, string? username)
    {
        return await CreateClient().Track.GetInfoAsync(track, artist, username ?? string.Empty);
    }

    /// <summary>
    ///     Gets a user's play count for a specific artist. The bundled Last.fm client library
    ///     doesn't expose a username-scoped overload for artist.getInfo, so this calls the raw
    ///     web API directly.
    /// </summary>
    /// <param name="artist">The artist name.</param>
    /// <param name="username">The Last.fm username to get the play count for.</param>
    public async Task<int> GetArtistUserPlaycountAsync(string artist, string username)
    {
        try
        {
            var doc = await CallRawApiAsync(new Dictionary<string, string>
            {
                {
                    "method", "artist.getinfo"
                },
                {
                    "artist", artist
                },
                {
                    "username", username
                },
                {
                    "autocorrect", "1"
                }
            });

            if (doc is null)
                return 0;

            if (doc.RootElement.TryGetProperty("artist", out var artistElement) &&
                artistElement.TryGetProperty("stats", out var stats) &&
                stats.TryGetProperty("userplaycount", out var userPlaycount))
            {
                return int.TryParse(userPlaycount.GetString(), out var count) ? count : 0;
            }

            return 0;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error getting Last.fm artist playcount for {Username}", username);
            return 0;
        }
    }

    /// <summary>
    ///     Gets a user's top tracks for the given period. Not exposed by the bundled Last.fm
    ///     client library, so this calls the raw web API directly.
    /// </summary>
    /// <param name="username">The Last.fm username.</param>
    /// <param name="rawPeriod">The raw Last.fm period string, e.g. "overall" or "7day".</param>
    /// <param name="count">The number of tracks to fetch.</param>
    public async Task<List<LastFmTopTrack>> GetTopTracksAsync(string username, string rawPeriod, int count = 10)
    {
        var doc = await CallRawApiAsync(new Dictionary<string, string>
        {
            {
                "method", "user.gettoptracks"
            },
            {
                "user", username
            },
            {
                "period", rawPeriod
            },
            {
                "limit", Math.Min(count, 50).ToString()
            }
        });

        var result = new List<LastFmTopTrack>();
        if (doc is null)
            return result;

        if (!doc.RootElement.TryGetProperty("toptracks", out var topTracks) ||
            !topTracks.TryGetProperty("track", out var tracks))
        {
            return result;
        }

        foreach (var track in tracks.EnumerateArray())
        {
            var name = track.GetProperty("name").GetString() ?? "Unknown";
            var artist = track.TryGetProperty("artist", out var artistElement)
                ? artistElement.GetProperty("name").GetString() ?? "Unknown"
                : "Unknown";
            var playcount = track.TryGetProperty("playcount", out var pc) && int.TryParse(pc.GetString(), out var p)
                ? p
                : 0;
            var url = track.TryGetProperty("url", out var urlElement) ? urlElement.GetString() ?? "" : "";
            string? image = null;
            if (track.TryGetProperty("image", out var images) && images.GetArrayLength() > 0)
            {
                var lastImage = images[images.GetArrayLength() - 1];
                if (lastImage.TryGetProperty("#text", out var text))
                    image = text.GetString();
            }

            result.Add(new LastFmTopTrack(name, artist, playcount, url, string.IsNullOrEmpty(image) ? null : image));
        }

        return result;
    }

    /// <summary>
    ///     Gets the play counts for an artist across every provided linked member, skipping any
    ///     that error out. Runs a bounded number of requests concurrently to avoid hammering the
    ///     Last.fm API.
    /// </summary>
    /// <param name="artist">The artist to check.</param>
    /// <param name="members">The linked members to check play counts for.</param>
    public async Task<List<(LinkedLastFmMember Member, int Playcount)>> GetArtistPlaycountsAsync(string artist,
        IReadOnlyCollection<LinkedLastFmMember> members)
    {
        using var throttle = new SemaphoreSlim(5);
        var tasks = members.Select(async member =>
        {
            await throttle.WaitAsync();
            try
            {
                var playcount = await GetArtistUserPlaycountAsync(artist, member.LastFmUser.Username);
                return (Member: member, Playcount: playcount);
            }
            finally
            {
                throttle.Release();
            }
        });

        var results = await Task.WhenAll(tasks);
        return results.Where(x => x.Playcount > 0).OrderByDescending(x => x.Playcount).ToList();
    }

    /// <summary>
    ///     Gets the play counts for an album across every provided linked member, skipping any
    ///     that error out. Runs a bounded number of requests concurrently.
    /// </summary>
    public async Task<List<(LinkedLastFmMember Member, int Playcount)>> GetAlbumPlaycountsAsync(string artist,
        string album, IReadOnlyCollection<LinkedLastFmMember> members)
    {
        using var throttle = new SemaphoreSlim(5);
        var tasks = members.Select(async member =>
        {
            await throttle.WaitAsync();
            try
            {
                var response = await GetAlbumInfoAsync(artist, album, member.LastFmUser.Username);
                var playcount = response.Success ? response.Content.UserPlayCount ?? 0 : 0;
                return (Member: member, Playcount: playcount);
            }
            finally
            {
                throttle.Release();
            }
        });

        var results = await Task.WhenAll(tasks);
        return results.Where(x => x.Playcount > 0).OrderByDescending(x => x.Playcount).ToList();
    }

    /// <summary>
    ///     Gets the play counts for a track across every provided linked member, skipping any
    ///     that error out. Runs a bounded number of requests concurrently.
    /// </summary>
    public async Task<List<(LinkedLastFmMember Member, int Playcount)>> GetTrackPlaycountsAsync(string artist,
        string track, IReadOnlyCollection<LinkedLastFmMember> members)
    {
        using var throttle = new SemaphoreSlim(5);
        var tasks = members.Select(async member =>
        {
            await throttle.WaitAsync();
            try
            {
                var response = await GetTrackInfoAsync(track, artist, member.LastFmUser.Username);
                var playcount = response.Success ? response.Content.UserPlayCount ?? 0 : 0;
                return (Member: member, Playcount: playcount);
            }
            finally
            {
                throttle.Release();
            }
        });

        var results = await Task.WhenAll(tasks);
        return results.Where(x => x.Playcount > 0).OrderByDescending(x => x.Playcount).ToList();
    }

    /// <summary>
    ///     Calls the raw Last.fm web API for read-only, unsigned methods and parses the JSON
    ///     response.
    /// </summary>
    private async Task<JsonDocument?> CallRawApiAsync(Dictionary<string, string> parameters)
    {
        parameters["api_key"] = creds.LastFmApiKey;
        parameters["format"] = "json";

        var query = string.Join("&", parameters.Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value)}"));
        var url = $"https://ws.audioscrobbler.com/2.0/?{query}";

        var httpClient = httpClientFactory.CreateClient();
        var response = await httpClient.GetAsync(url);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Last.fm raw API error: {Content}", content);
            return null;
        }

        return JsonDocument.Parse(content);
    }
}