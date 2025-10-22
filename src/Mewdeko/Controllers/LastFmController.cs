using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using DataModel;
using IF.Lastfm.Core.Api;
using IF.Lastfm.Core.Api.Enums;
using LinqToDB;
using LinqToDB.Async;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     Controller for Last.fm integration
/// </summary>
[ApiController]
[Route("botapi/[controller]")]
public class LastFmController : ControllerBase
{
    private readonly IBotCredentials creds;
    private readonly IDataConnectionFactory dbFactory;
    private readonly IHttpClientFactory httpClientFactory;
    private readonly ILogger<LastFmController> logger;

    /// <summary>
    ///     Controller for Last.fm integration
    /// </summary>
    /// <param name="creds">Bot credentials containing Last.fm API keys</param>
    /// <param name="dbFactory">Database connection factory</param>
    /// <param name="logger">Logger instance</param>
    /// <param name="httpClientFactory">HTTP client factory for Last.fm API calls</param>
    public LastFmController(IBotCredentials creds, IDataConnectionFactory dbFactory,
        ILogger<LastFmController> logger, IHttpClientFactory httpClientFactory)
    {
        this.creds = creds;
        this.dbFactory = dbFactory;
        this.logger = logger;
        this.httpClientFactory = httpClientFactory;
    }

    /// <summary>
    ///     Gets the Last.fm OAuth authorization URL
    /// </summary>
    /// <param name="userId">The Discord user ID</param>
    /// <returns>The authorization URL</returns>
    [HttpGet("auth-url")]
    [Authorize("ApiKeyPolicy")]
    public IActionResult GetAuthUrl([FromQuery] ulong userId)
    {
        if (string.IsNullOrEmpty(creds.LastFmApiKey))
        {
            return BadRequest(new
            {
                error = "Last.fm API key not configured"
            });
        }

        var callbackUrl = $"{Request.Scheme}://{Request.Host}/botapi/lastfm/callback?userId={userId}";
        var authUrl =
            $"https://www.last.fm/api/auth/?api_key={creds.LastFmApiKey}&cb={Uri.EscapeDataString(callbackUrl)}";

        return Ok(new
        {
            authUrl
        });
    }

    /// <summary>
    ///     Handles the Last.fm OAuth callback
    /// </summary>
    /// <param name="token">The Last.fm auth token</param>
    /// <param name="userId">The Discord user ID</param>
    /// <returns>Redirect or success result</returns>
    [HttpGet("callback")]
    [AllowAnonymous]
    public async Task<IActionResult> HandleCallback([FromQuery] string token, [FromQuery] ulong userId)
    {
        try
        {
            if (string.IsNullOrEmpty(token))
            {
                return BadRequest(new
                {
                    error = "No token provided"
                });
            }

            if (string.IsNullOrEmpty(creds.LastFmApiKey) || string.IsNullOrEmpty(creds.LastFmApiSecret))
            {
                return BadRequest(new
                {
                    error = "Last.fm API credentials not configured"
                });
            }

            // Exchange token for session key using web auth flow
            var sessionData = await GetSessionFromTokenAsync(token);

            if (sessionData == null)
            {
                logger.LogWarning("Failed to get Last.fm session from token");
                return BadRequest(new
                {
                    error = "Failed to authenticate with Last.fm"
                });
            }

            // Store or update user's Last.fm session
            await using var db = await dbFactory.CreateConnectionAsync();

            var existingUser = await db.GetTable<LastFmUser>().FirstOrDefaultAsync(x => x.UserId == userId);

            if (existingUser != null)
            {
                existingUser.SessionKey = sessionData.Value.Key;
                existingUser.Username = sessionData.Value.Username;
                existingUser.ScrobblingEnabled = true;
                await db.UpdateAsync(existingUser);
            }
            else
            {
                var newUser = new LastFmUser
                {
                    UserId = userId,
                    SessionKey = sessionData.Value.Key,
                    Username = sessionData.Value.Username,
                    ScrobblingEnabled = true,
                    DateAdded = DateTime.UtcNow
                };
                await db.InsertAsync(newUser);
            }

            logger.LogInformation(
                $"Successfully linked Last.fm account {sessionData.Value.Username} for user {userId}");

            // Redirect to dashboard
            return Redirect($"{creds.DashboardUrl}/me?lastfm=success");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling Last.fm callback");
            return StatusCode(500, new
            {
                error = "Internal server error"
            });
        }
    }

    /// <summary>
    ///     Unlinks the user's Last.fm account
    /// </summary>
    /// <param name="userId">The Discord user ID</param>
    /// <returns>Success or error result</returns>
    [HttpDelete("unlink")]
    [Authorize("ApiKeyPolicy")]
    public async Task<IActionResult> Unlink([FromQuery] ulong userId)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();

            var deleted = await db.GetTable<LastFmUser>()
                .Where(x => x.UserId == userId)
                .DeleteAsync();

            if (deleted > 0)
            {
                return Ok(new
                {
                    success = true, message = "Last.fm account unlinked"
                });
            }

            return NotFound(new
            {
                error = "No Last.fm account linked"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error unlinking Last.fm account");
            return StatusCode(500, new
            {
                error = "Internal server error"
            });
        }
    }

    /// <summary>
    ///     Gets the user's Last.fm link status
    /// </summary>
    /// <param name="userId">The Discord user ID</param>
    /// <returns>The user's Last.fm status</returns>
    [HttpGet("status")]
    [Authorize("ApiKeyPolicy")]
    public async Task<IActionResult> GetStatus([FromQuery] ulong userId)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();

            var lastFmUser = await db.GetTable<LastFmUser>()
                .FirstOrDefaultAsync(x => x.UserId == userId);

            if (lastFmUser == null)
            {
                return Ok(new
                {
                    linked = false, username = (string)null, scrobblingEnabled = false
                });
            }

            return Ok(new
            {
                linked = true, username = lastFmUser.Username, scrobblingEnabled = lastFmUser.ScrobblingEnabled
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting Last.fm status");
            return StatusCode(500, new
            {
                error = "Internal server error"
            });
        }
    }

    /// <summary>
    ///     Toggles scrobbling on or off for the user
    /// </summary>
    /// <param name="userId">The Discord user ID</param>
    /// <param name="enabled">Whether scrobbling should be enabled</param>
    /// <returns>Updated status</returns>
    [HttpPost("toggle")]
    [Authorize("ApiKeyPolicy")]
    public async Task<IActionResult> ToggleScrobbling([FromQuery] ulong userId, [FromQuery] bool enabled)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();

            var lastFmUser = await db.GetTable<LastFmUser>()
                .FirstOrDefaultAsync(x => x.UserId == userId);

            if (lastFmUser == null)
            {
                return NotFound(new
                {
                    error = "No Last.fm account linked"
                });
            }

            lastFmUser.ScrobblingEnabled = enabled;
            await db.UpdateAsync(lastFmUser);

            return Ok(new
            {
                success = true, scrobblingEnabled = lastFmUser.ScrobblingEnabled
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error toggling Last.fm scrobbling");
            return StatusCode(500, new
            {
                error = "Internal server error"
            });
        }
    }

    /// <summary>
    ///     Gets the user's Last.fm profile information including scrobble count
    /// </summary>
    /// <param name="userId">The Discord user ID</param>
    /// <returns>User's Last.fm profile data</returns>
    [HttpGet("user-info")]
    [Authorize("ApiKeyPolicy")]
    public async Task<IActionResult> GetUserInfo([FromQuery] ulong userId)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();
            var lastFmUser = await db.GetTable<LastFmUser>()
                .FirstOrDefaultAsync(x => x.UserId == userId);

            if (lastFmUser == null)
            {
                return NotFound(new
                {
                    error = "No Last.fm account linked"
                });
            }

            var lastfmClient = new LastfmClient(creds.LastFmApiKey, creds.LastFmApiSecret);
            var userInfo = await lastfmClient.User.GetInfoAsync(lastFmUser.Username);

            if (!userInfo.Success)
            {
                return BadRequest(new
                {
                    error = "Failed to fetch Last.fm user info"
                });
            }

            return Ok(new
            {
                username = userInfo.Content.Name,
                playcount = userInfo.Content.Playcount,
                country = userInfo.Content.Country,
                avatar = userInfo.Content.Avatar?.Large?.ToString()
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting Last.fm user info");
            return StatusCode(500, new
            {
                error = "Internal server error"
            });
        }
    }

    /// <summary>
    ///     Gets the user's recent tracks from Last.fm
    /// </summary>
    /// <param name="userId">The Discord user ID</param>
    /// <param name="count">Number of tracks to fetch (default 10, max 50)</param>
    /// <returns>List of recently played tracks</returns>
    [HttpGet("recent-tracks")]
    [Authorize("ApiKeyPolicy")]
    public async Task<IActionResult> GetRecentTracks([FromQuery] ulong userId, [FromQuery] int count = 10)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();
            var lastFmUser = await db.GetTable<LastFmUser>()
                .FirstOrDefaultAsync(x => x.UserId == userId);

            if (lastFmUser == null)
            {
                return NotFound(new
                {
                    error = "No Last.fm account linked"
                });
            }

            var lastfmClient = new LastfmClient(creds.LastFmApiKey, creds.LastFmApiSecret);
            var recentTracks =
                await lastfmClient.User.GetRecentScrobbles(lastFmUser.Username, null, null, false, 1,
                    Math.Min(count, 50));

            if (!recentTracks.Success)
            {
                return BadRequest(new
                {
                    error = "Failed to fetch recent tracks"
                });
            }

            var tracks = recentTracks.Content.Select(t => new
            {
                name = t.Name,
                artist = t.ArtistName,
                album = t.AlbumName,
                url = t.Url?.ToString(),
                image = t.Images?.Large?.ToString(),
                isNowPlaying = t.IsNowPlaying,
                timePlayed = t.TimePlayed
            });

            return Ok(tracks);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting Last.fm recent tracks");
            return StatusCode(500, new
            {
                error = "Internal server error"
            });
        }
    }

    /// <summary>
    ///     Gets the user's top artists from Last.fm
    /// </summary>
    /// <param name="userId">The Discord user ID</param>
    /// <param name="period">Time period: overall, 7day, 1month, 3month, 6month, 12month</param>
    /// <param name="count">Number of artists to fetch (default 10, max 50)</param>
    /// <returns>List of top artists</returns>
    [HttpGet("top-artists")]
    [Authorize("ApiKeyPolicy")]
    public async Task<IActionResult> GetTopArtists([FromQuery] ulong userId, [FromQuery] string period = "overall",
        [FromQuery] int count = 10)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();
            var lastFmUser = await db.GetTable<LastFmUser>()
                .FirstOrDefaultAsync(x => x.UserId == userId);

            if (lastFmUser == null)
            {
                return NotFound(new
                {
                    error = "No Last.fm account linked"
                });
            }

            var timePeriod = ParseTimePeriod(period);
            var lastfmClient = new LastfmClient(creds.LastFmApiKey, creds.LastFmApiSecret);
            var topArtists =
                await lastfmClient.User.GetTopArtists(lastFmUser.Username, timePeriod, 1, Math.Min(count, 50));

            if (!topArtists.Success)
            {
                return BadRequest(new
                {
                    error = "Failed to fetch top artists"
                });
            }

            var artists = topArtists.Content.Select(a => new
            {
                name = a.Name, playcount = a.PlayCount, url = a.Url?.ToString(), image = a.MainImage?.Large?.ToString()
            });

            return Ok(artists);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting Last.fm top artists");
            return StatusCode(500, new
            {
                error = "Internal server error"
            });
        }
    }

    /// <summary>
    ///     Gets the user's top albums from Last.fm
    /// </summary>
    /// <param name="userId">The Discord user ID</param>
    /// <param name="period">Time period: overall, 7day, 1month, 3month, 6month, 12month</param>
    /// <param name="count">Number of albums to fetch (default 10, max 50)</param>
    /// <returns>List of top albums</returns>
    [HttpGet("top-albums")]
    [Authorize("ApiKeyPolicy")]
    public async Task<IActionResult> GetTopAlbums([FromQuery] ulong userId, [FromQuery] string period = "overall",
        [FromQuery] int count = 10)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();
            var lastFmUser = await db.GetTable<LastFmUser>()
                .FirstOrDefaultAsync(x => x.UserId == userId);

            if (lastFmUser == null)
            {
                return NotFound(new
                {
                    error = "No Last.fm account linked"
                });
            }

            var timePeriod = ParseTimePeriod(period);
            var lastfmClient = new LastfmClient(creds.LastFmApiKey, creds.LastFmApiSecret);
            var topAlbums =
                await lastfmClient.User.GetTopAlbums(lastFmUser.Username, timePeriod, 1, Math.Min(count, 50));

            if (!topAlbums.Success)
            {
                return BadRequest(new
                {
                    error = "Failed to fetch top albums"
                });
            }

            var albums = topAlbums.Content.Select(a => new
            {
                name = a.Name,
                artist = a.ArtistName,
                playcount = a.PlayCount,
                url = a.Url?.ToString(),
                image = a.Images?.Large?.ToString()
            });

            return Ok(albums);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting Last.fm top albums");
            return StatusCode(500, new
            {
                error = "Internal server error"
            });
        }
    }

    /// <summary>
    ///     Parses time period string to LastStatsTimeSpan enum
    /// </summary>
    private LastStatsTimeSpan ParseTimePeriod(string period)
    {
        return period?.ToLower() switch
        {
            "7day" => LastStatsTimeSpan.Week,
            "1month" => LastStatsTimeSpan.Month,
            "3month" => LastStatsTimeSpan.Quarter,
            "6month" => LastStatsTimeSpan.Half,
            "12month" => LastStatsTimeSpan.Year,
            _ => LastStatsTimeSpan.Overall
        };
    }

    /// <summary>
    ///     Exchanges a Last.fm auth token for a session key using the web auth flow
    /// </summary>
    /// <param name="token">The auth token from Last.fm callback</param>
    /// <returns>Session information or null if failed</returns>
    private async Task<(string Key, string Username)?> GetSessionFromTokenAsync(string token)
    {
        try
        {
            // Build parameters for auth.getSession
            var parameters = new Dictionary<string, string>
            {
                {
                    "method", "auth.getSession"
                },
                {
                    "api_key", creds.LastFmApiKey
                },
                {
                    "token", token
                }
            };

            // Generate API signature
            var signature = GenerateSignature(parameters);
            parameters["api_sig"] = signature;

            // Build query string
            var queryString =
                string.Join("&", parameters.Select(kvp => $"{kvp.Key}={Uri.EscapeDataString(kvp.Value)}"));
            var url = $"https://ws.audioscrobbler.com/2.0/?{queryString}";

            // Make request
            var httpClient = httpClientFactory.CreateClient();
            var response = await httpClient.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError($"Last.fm API error: {content}");
                return null;
            }

            // Parse XML response
            var doc = XDocument.Parse(content);
            var lfm = doc.Element("lfm");
            var status = lfm?.Attribute("status")?.Value;

            if (status != "ok")
            {
                var error = lfm?.Element("error");
                logger.LogError($"Last.fm returned error: {error?.Value}");
                return null;
            }

            var sessionElement = lfm?.Element("session");
            var username = sessionElement?.Element("name")?.Value;
            var sessionKey = sessionElement?.Element("key")?.Value;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(sessionKey))
            {
                logger.LogError("Failed to parse Last.fm session response");
                return null;
            }

            return (sessionKey, username);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting Last.fm session from token");
            return null;
        }
    }

    /// <summary>
    ///     Generates Last.fm API signature
    /// </summary>
    /// <param name="parameters">The API parameters</param>
    /// <returns>MD5 hash signature</returns>
    private string GenerateSignature(Dictionary<string, string> parameters)
    {
        // Sort parameters alphabetically
        var sorted = parameters.OrderBy(kvp => kvp.Key);

        // Build signature string
        var signatureBuilder = new StringBuilder();
        foreach (var kvp in sorted)
        {
            signatureBuilder.Append(kvp.Key).Append(kvp.Value);
        }

        signatureBuilder.Append(creds.LastFmApiSecret);

        // Compute MD5 hash
        var signatureString = signatureBuilder.ToString();
        var bytes = Encoding.UTF8.GetBytes(signatureString);
        var hash = MD5.HashData(bytes);

        return Convert.ToHexString(hash).ToLower();
    }
}