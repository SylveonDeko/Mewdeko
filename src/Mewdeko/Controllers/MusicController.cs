using System.Net.Http;
using System.Text.Json;
using DataModel;
using Lavalink4NET;
using Lavalink4NET.Filters;
using Lavalink4NET.Players;
using Lavalink4NET.Rest.Entities.Tracks;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Controllers.Common.Music;
using Mewdeko.Modules.Music.Common;
using Mewdeko.Modules.Music.CustomPlayer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     Controller for managing music playback and settings
/// </summary>
[ApiController]
[Route("botapi/[controller]/{guildId}")]
public class MusicController : Controller
{
    private readonly IAudioService audioService;
    private readonly IDataCache cache;
    private readonly DiscordShardedClient client;
    private readonly IDataConnectionFactory dbFactory;
    private readonly MusicEventManager eventManager;
    private readonly ILogger<MusicController> logger;

    /// <summary>
    ///     Controller for managing music playback and settings
    /// </summary>
    /// <param name="audioService">The audio service for managing music playback operations</param>
    /// <param name="cache">The data cache for storing and retrieving music-related information</param>
    /// <param name="client">The Discord client for accessing guild and user information</param>
    /// <param name="dbFactory">The database connection factory</param>
    /// <param name="eventManager">The event manager for music events</param>
    /// <param name="logger">The logger instance for structured logging.</param>
    public MusicController(
        IAudioService audioService,
        IDataCache cache,
        DiscordShardedClient client,
        IDataConnectionFactory dbFactory,
        MusicEventManager eventManager, ILogger<MusicController> logger)
    {
        this.audioService = audioService;
        this.cache = cache;
        this.client = client;
        this.dbFactory = dbFactory;
        this.eventManager = eventManager;
        this.logger = logger;
    }

    /// <summary>
    ///     Gets the current player state and track information
    /// </summary>
    /// <param name="guildId">The Discord guild ID</param>
    /// <param name="userId">The Discord user ID making the request</param>
    /// <returns>Player status including current track, queue, and settings</returns>
    [HttpGet("status")]
    [Authorize("ApiKeyPolicy")]
    public async Task<IActionResult> GetPlayerStatus(ulong guildId, [FromQuery] ulong userId)
    {
        var guild = client.GetGuild(guildId);
        var user = guild?.GetUser(userId);
        var botUser = guild?.GetUser(client.CurrentUser.Id);
        var botVoiceChannel = botUser?.VoiceChannel;

        var player = await audioService.Players.GetPlayerAsync<MewdekoPlayer>(guildId);

        // Check if bot is in a voice channel but player doesn't exist or is idle
        if (player == null)
        {
            // Bot is in a voice channel but not playing
            if (botVoiceChannel != null)
            {
                var isInVoiceChannel = user?.VoiceChannel?.Id == botVoiceChannel.Id;
                var idleSettings = await cache.GetMusicPlayerSettings(guildId);

                var idleStatus = new
                {
                    CurrentTrack = (object)null,
                    Queue = await cache.GetMusicQueue(guildId),
                    State = PlayerState.NotPlaying,
                    Volume = idleSettings?.Volume ?? 100,
                    Position = TimeSpan.Zero,
                    RepeatMode = idleSettings?.PlayerRepeat ?? 0,
                    Filters = new
                    {
                        BassBoost = false,
                        Nightcore = false,
                        Vaporwave = false,
                        Karaoke = false,
                        Tremolo = false,
                        Vibrato = false,
                        Rotation = false,
                        Distortion = false,
                        ChannelMix = false
                    },
                    IsInVoiceChannel = isInVoiceChannel,
                    BotInChannel = true,
                    ChannelId = botVoiceChannel.Id,
                    ChannelName = botVoiceChannel.Name
                };

                return Ok(idleStatus);
            }

            // Bot is not in any voice channel
            return NotFound(new
            {
                Error = "No active player found", BotInChannel = false
            });
        }

        var voiceChannelId = player.VoiceChannelId;
        var isUserInVoiceChannel = user?.VoiceChannel?.Id == voiceChannelId;

        var currentTrack = await cache.GetCurrentTrack(guildId);
        var queue = await cache.GetMusicQueue(guildId);
        var settings = await cache.GetMusicPlayerSettings(guildId);

        var status = new
        {
            CurrentTrack = currentTrack,
            Queue = queue,
            player.State,
            player.Volume,
            player.Position,
            RepeatMode = settings.PlayerRepeat,
            Filters = new
            {
                BassBoost = player.Filters.Equalizer != null,
                Nightcore = player.Filters.Timescale?.Speed > 1.0f,
                Vaporwave = player.Filters.Timescale?.Speed < 1.0f,
                Karaoke = player.Filters.Karaoke != null,
                Tremolo = player.Filters.Tremolo != null,
                Vibrato = player.Filters.Vibrato != null,
                Rotation = player.Filters.Rotation != null,
                Distortion = player.Filters.Distortion != null,
                ChannelMix = player.Filters.ChannelMix != null
            },
            IsInVoiceChannel = isUserInVoiceChannel,
            BotInChannel = true,
            ChannelId = voiceChannelId,
            ChannelName = botVoiceChannel?.Name ?? guild?.GetChannel(voiceChannelId)?.Name
        };

        return Ok(status);
    }

    /// <summary>
    ///     Searches for tracks using the provided query and search mode
    /// </summary>
    /// <param name="query">The search query</param>
    /// <param name="mode">The search mode (YouTube, Spotify, SoundCloud)</param>
    /// <param name="limit">Maximum number of results to return</param>
    /// <returns>A list of matching tracks</returns>
    [HttpGet("search")]
    [Authorize("ApiKeyPolicy")]
    public async Task<IActionResult> SearchTracks([FromQuery] string query, [FromQuery] string mode = "YouTube",
        [FromQuery] int limit = 10)
    {
        if (string.IsNullOrWhiteSpace(query))
            return BadRequest("Search query is required");

        if (limit is < 1 or > 25)
            limit = 10;

        try
        {
            // Parse the search mode
            var searchMode = mode.ToLower() switch
            {
                "youtube" => TrackSearchMode.YouTube,
                "spotify" => TrackSearchMode.Spotify,
                "soundcloud" => TrackSearchMode.SoundCloud,
                "youtubemusic" => TrackSearchMode.YouTubeMusic,
                _ => TrackSearchMode.YouTube
            };

            // Perform the search
            var trackResults = await audioService.Tracks.LoadTracksAsync(query, new TrackLoadOptions
            {
                SearchMode = searchMode
            });

            if (!trackResults.IsSuccess)
                return Ok(new
                {
                    Tracks = Array.Empty<object>()
                });

            // Map the tracks to a response-friendly format
            var tracks = trackResults.Tracks
                .Take(limit)
                .Select(track => new
                {
                    track.Title,
                    track.Author,
                    Duration = track.Duration.ToString(@"mm\:ss"),
                    Uri = track.Uri?.ToString(),
                    ArtworkUri = track.ArtworkUri?.ToString(),
                    Provider = track.Provider.ToString()
                })
                .ToList();

            return Ok(new
            {
                Tracks = tracks
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error searching for tracks with query: {Query}", query);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    ///     Extracts track information from a provided URL
    /// </summary>
    /// <param name="url">The URL to extract information from</param>
    /// <returns>Track information if available</returns>
    [HttpGet("extract")]
    [Authorize("ApiKeyPolicy")]
    public async Task<IActionResult> ExtractTrack([FromQuery] string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return BadRequest("URL is required");

        try
        {
            // Determine the appropriate search mode based on the URL
            var searchMode = url.ToLower() switch
            {
                var u when u.Contains("spotify.com") => TrackSearchMode.Spotify,
                var u when u.Contains("music.youtube") => TrackSearchMode.YouTubeMusic,
                var u when u.Contains("youtube.com") || u.Contains("youtu.be") => TrackSearchMode.YouTube,
                var u when u.Contains("soundcloud.com") => TrackSearchMode.SoundCloud,
                _ => TrackSearchMode.None
            };

            // Load the track
            var track = await audioService.Tracks.LoadTrackAsync(url, new TrackLoadOptions
            {
                SearchMode = searchMode
            });

            if (track == null)
                return NotFound("Could not extract track information from URL");

            // Map the track to a response-friendly format
            var trackInfo = new
            {
                track.Title,
                track.Author,
                Duration = track.Duration.ToString(@"mm\:ss"),
                Uri = track.Uri?.ToString(),
                ArtworkUri = track.ArtworkUri?.ToString(),
                Provider = track.Provider.ToString()
            };

            return Ok(trackInfo);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error extracting track info from URL: {Url}", url);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    ///     WebSocket endpoint for real-time music updates
    /// </summary>
    /// <param name="guildId">The Discord guild ID</param>
    /// <param name="userId">The Discord user ID making the request</param>
    /// <returns>A WebSocket or SSE connection providing real-time player updates</returns>
    [HttpGet("events")]
    [AllowAnonymous]
    public async Task GetMusicEvents(ulong guildId, [FromQuery] ulong userId)
    {
        if (!HttpContext.WebSockets.IsWebSocketRequest)
        {
            // Fall back to Server-Sent Events if WebSockets not available
            if (Request.Headers["Accept"].ToString().Contains("text/event-stream"))
            {
                await HandleServerSentEvents(guildId, userId);
                return;
            }

            HttpContext.Response.StatusCode = 400;
            await HttpContext.Response.WriteAsync("WebSocket or EventSource connection required");
            return;
        }

        // Accept the WebSocket connection
        using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
        await eventManager.HandleWebSocketConnection(guildId, userId, webSocket, HttpContext);
    }

    /// <summary>
    ///     Fallback Server-Sent Events handler
    /// </summary>
    /// <param name="guildId">The Discord guild ID</param>
    /// <param name="userId">The Discord user ID making the request</param>
    /// <returns>A task that completes when the SSE connection is closed</returns>
    private async Task HandleServerSentEvents(ulong guildId, ulong userId)
    {
        HttpContext.Response.Headers["Content-Type"] = "text/event-stream";
        HttpContext.Response.Headers["Cache-Control"] = "no-cache";
        HttpContext.Response.Headers["Connection"] = "keep-alive";

        // Send initial status
        var initialStatus = await GetInitialStatus(guildId, userId);
        var initialJson = JsonSerializer.Serialize(initialStatus);
        await HttpContext.Response.WriteAsync($"event: status\ndata: {initialJson}\n\n");
        await HttpContext.Response.Body.FlushAsync();

        // Register for updates
        var connectionId = Guid.NewGuid().ToString();
        var completion = new TaskCompletionSource<bool>();

        // Register for player updates
        eventManager.RegisterSseConnection(guildId, connectionId, async status =>
        {
            try
            {
                var json = JsonSerializer.Serialize(status);
                await HttpContext.Response.WriteAsync($"event: status\ndata: {json}\n\n");
                await HttpContext.Response.Body.FlushAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error sending SSE update");
                completion.TrySetResult(false);
            }
        }, userId);

        // Handle disconnection
        HttpContext.RequestAborted.Register(() =>
        {
            eventManager.UnregisterSseConnection(guildId, connectionId);
            completion.TrySetResult(true);
        });

        // Keep the connection alive with heartbeats
        _ = Task.Run(async () =>
        {
            try
            {
                while (!completion.Task.IsCompleted)
                {
                    await Task.Delay(30000); // 30 second heartbeat
                    try
                    {
                        await HttpContext.Response.WriteAsync("event: heartbeat\ndata: {\"time\":" +
                                                              DateTimeOffset.UtcNow.ToUnixTimeSeconds() + "}\n\n");
                        await HttpContext.Response.Body.FlushAsync();
                    }
                    catch
                    {
                        completion.TrySetResult(false);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in SSE heartbeat");
            }
        });

        // Wait for the connection to close
        await completion.Task;
        eventManager.UnregisterSseConnection(guildId, connectionId);
    }

    /// <summary>
    ///     Plays a specific track from the queue by its index
    /// </summary>
    /// <param name="guildId">The Discord guild ID</param>
    /// <param name="index">The index of the track to play</param>
    /// <returns>The result of the operation</returns>
    [HttpPost("play-track/{index}")]
    [Authorize("ApiKeyPolicy")]
    public async Task<IActionResult> PlayTrackAt(ulong guildId, int index)
    {
        var player = await audioService.Players.GetPlayerAsync<MewdekoPlayer>(guildId);
        if (player == null)
            return NotFound("No active player found");

        var queue = await cache.GetMusicQueue(guildId);
        var track = queue.FirstOrDefault(t => t.Index == index);

        if (track == null)
            return NotFound("Track not found in queue");

        // Play the requested track
        await player.PlayAsync(track.Track);

        // Update the current track in cache
        await cache.SetCurrentTrack(guildId, track);

        // Notify clients of track change
        await eventManager.BroadcastPlayerUpdate(guildId);

        return Ok(new
        {
            Message = $"Now playing track {index}: {track.Track.Title}", Track = track
        });
    }

    /// <summary>
    ///     Get initial player status for real-time connections
    /// </summary>
    /// <param name="guildId">The Discord guild ID</param>
    /// <param name="userId">The Discord user ID making the request</param>
    /// <returns>An object containing the current player status</returns>
    private async Task<object> GetInitialStatus(ulong guildId, ulong userId)
    {
        var guild = client.GetGuild(guildId);
        var user = guild?.GetUser(userId);
        var botUser = guild?.GetUser(client.CurrentUser.Id);
        var botVoiceChannel = botUser?.VoiceChannel;

        var player = await audioService.Players.GetPlayerAsync<MewdekoPlayer>(guildId);

        // Check if bot is in a voice channel but player doesn't exist
        if (player == null)
        {
            // Bot is in a voice channel but not playing
            if (botVoiceChannel != null)
            {
                var isInVoiceChannel = user?.VoiceChannel?.Id == botVoiceChannel.Id;
                var idleSettings = await cache.GetMusicPlayerSettings(guildId);

                return new
                {
                    CurrentTrack = (object)null,
                    Queue = await cache.GetMusicQueue(guildId),
                    State = PlayerState.NotPlaying,
                    Volume = idleSettings?.Volume ?? 100,
                    Position = TimeSpan.Zero,
                    RepeatMode = idleSettings?.PlayerRepeat ?? 0,
                    Filters = new
                    {
                        BassBoost = false,
                        Nightcore = false,
                        Vaporwave = false,
                        Karaoke = false,
                        Tremolo = false,
                        Vibrato = false,
                        Rotation = false,
                        Distortion = false,
                        ChannelMix = false
                    },
                    IsInVoiceChannel = isInVoiceChannel,
                    BotInChannel = true,
                    ChannelId = botVoiceChannel.Id,
                    ChannelName = botVoiceChannel.Name
                };
            }

            // Bot is not in any voice channel
            return new
            {
                error = "No active player", BotInChannel = false
            };
        }

        var voiceChannelId = player.VoiceChannelId;
        var isUserInVoiceChannel = user?.VoiceChannel?.Id == voiceChannelId;

        var currentTrack = await cache.GetCurrentTrack(guildId);
        var queue = await cache.GetMusicQueue(guildId);
        var settings = await cache.GetMusicPlayerSettings(guildId);

        return new
        {
            CurrentTrack = currentTrack,
            Queue = queue,
            player.State,
            player.Volume,
            player.Position,
            RepeatMode = settings.PlayerRepeat,
            Filters = new
            {
                BassBoost = player.Filters.Equalizer != null,
                Nightcore = player.Filters.Timescale?.Speed > 1.0f,
                Vaporwave = player.Filters.Timescale?.Speed < 1.0f,
                Karaoke = player.Filters.Karaoke != null,
                Tremolo = player.Filters.Tremolo != null,
                Vibrato = player.Filters.Vibrato != null,
                Rotation = player.Filters.Rotation != null,
                Distortion = player.Filters.Distortion != null,
                ChannelMix = player.Filters.ChannelMix != null
            },
            IsInVoiceChannel = isUserInVoiceChannel,
            BotInChannel = true,
            ChannelId = voiceChannelId,
            ChannelName = botVoiceChannel?.Name ?? guild?.GetChannel(voiceChannelId)?.Name
        };
    }

    /// <summary>
    ///     Plays or enqueues a track
    /// </summary>
    /// <param name="guildId">The Discord guild ID</param>
    /// <param name="request">The play request containing URL and requester information</param>
    /// <returns>The loaded track and its position in the queue</returns>
    [Authorize("ApiKeyPolicy")]
    [HttpPost("play")]
    public async Task<IActionResult> Play(ulong guildId, [FromBody] PlayRequest request)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound("Guild not found");

        var player = await audioService.Players.GetPlayerAsync<MewdekoPlayer>(guildId);
        if (player == null)
            return NotFound("No active player found");

        var searchMode = request.Url.Contains("spotify") ? TrackSearchMode.Spotify :
            request.Url.Contains("youtube") ? TrackSearchMode.YouTube : TrackSearchMode.None;

        var trackResult = await audioService.Tracks.LoadTrackAsync(request.Url, new TrackLoadOptions
        {
            SearchMode = searchMode
        });
        if (trackResult is null)
            return BadRequest("Failed to load track");

        var queue = await cache.GetMusicQueue(guildId);
        queue.Add(new MewdekoTrack(queue.Count + 1, trackResult, request.Requester));
        await cache.SetMusicQueue(guildId, queue);

        if (player.State != PlayerState.Playing)
        {
            await player.PlayAsync(trackResult);
            await cache.SetCurrentTrack(guildId, queue[0]);

            // Notify clients of track change
            await eventManager.BroadcastPlayerUpdate(guildId);
        }

        return Ok(new
        {
            Track = trackResult, Position = queue.Count
        });
    }

    /// <summary>
    ///     Pauses or resumes playback
    /// </summary>
    /// <param name="guildId">The Discord guild ID</param>
    /// <returns>The new player state</returns>
    [HttpPost("pause")]
    [Authorize("ApiKeyPolicy")]
    public async Task<IActionResult> PauseResume(ulong guildId)
    {
        var player = await audioService.Players.GetPlayerAsync<MewdekoPlayer>(guildId);
        if (player == null)
            return NotFound("No active player found");

        if (player.State == PlayerState.Playing)
            await player.PauseAsync();
        else
            await player.ResumeAsync();

        // Notify clients of state change
        await eventManager.BroadcastPlayerUpdate(guildId);

        return Ok(new
        {
            player.State
        });
    }

    /// <summary>
    ///     Gets the current queue
    /// </summary>
    /// <param name="guildId">The Discord guild ID</param>
    /// <returns>The current music queue</returns>
    [HttpGet("queue")]
    [Authorize("ApiKeyPolicy")]
    public async Task<IActionResult> GetQueue(ulong guildId)
    {
        var queue = await cache.GetMusicQueue(guildId);
        return Ok(queue);
    }

    /// <summary>
    ///     Clears the queue
    /// </summary>
    /// <param name="guildId">The Discord guild ID</param>
    /// <returns>OK result when queue is cleared</returns>
    [HttpDelete("queue")]
    [Authorize("ApiKeyPolicy")]
    public async Task<IActionResult> ClearQueue(ulong guildId)
    {
        await cache.SetMusicQueue(guildId, []);

        // Notify clients of queue change
        await eventManager.BroadcastPlayerUpdate(guildId);

        return Ok();
    }

    /// <summary>
    ///     Removes a track from the queue
    /// </summary>
    /// <param name="guildId">The Discord guild ID</param>
    /// <param name="index">The index of the track to remove</param>
    /// <returns>OK result when track is removed</returns>
    [HttpDelete("queue/{index}")]
    [Authorize("ApiKeyPolicy")]
    public async Task<IActionResult> RemoveTrack(ulong guildId, int index)
    {
        var queue = await cache.GetMusicQueue(guildId);
        var track = queue.FirstOrDefault(x => x.Index == index);
        if (track == null)
            return NotFound("Track not found");

        queue.Remove(track);
        await cache.SetMusicQueue(guildId, queue);

        // Notify clients of queue change
        await eventManager.BroadcastPlayerUpdate(guildId);

        return Ok();
    }

    /// <summary>
    ///     Sets the volume
    /// </summary>
    /// <param name="guildId">The Discord guild ID</param>
    /// <param name="volume">The volume level (0-100)</param>
    /// <returns>The new volume level</returns>
    [HttpPost("volume/{volume}")]
    [Authorize("ApiKeyPolicy")]
    public async Task<IActionResult> SetVolume(ulong guildId, int volume)
    {
        if (volume is < 0 or > 100)
            return BadRequest("Volume must be between 0 and 100");

        var player = await audioService.Players.GetPlayerAsync<MewdekoPlayer>(guildId);
        if (player == null)
            return NotFound("No active player found");

        await player.SetVolumeAsync(volume / 100f);
        await player.SetGuildVolumeAsync(volume);

        // Notify clients of volume change
        await eventManager.BroadcastPlayerUpdate(guildId);

        return Ok(new
        {
            Volume = volume
        });
    }

    /// <summary>
    ///     Seek to position in track
    /// </summary>
    /// <param name="guildId">The Discord guild ID</param>
    /// <param name="request">The seek request containing position in seconds</param>
    /// <returns>OK result when seek is successful</returns>
    [HttpPost("seek")]
    [Authorize("ApiKeyPolicy")]
    public async Task<IActionResult> Seek(ulong guildId, [FromBody] SeekRequest request)
    {
        var player = await audioService.Players.GetPlayerAsync<MewdekoPlayer>(guildId);
        if (player == null)
            return NotFound("No active player found");

        await player.SeekAsync(TimeSpan.FromSeconds(request.Position));

        // Notify clients of position change
        await eventManager.BroadcastPlayerUpdate(guildId);

        return Ok();
    }

    /// <summary>
    ///     Skips to the next track
    /// </summary>
    /// <param name="guildId">The Discord guild ID</param>
    /// <returns>OK result when skip is successful</returns>
    [HttpPost("skip")]
    [Authorize("ApiKeyPolicy")]
    public async Task<IActionResult> SkipTrack(ulong guildId)
    {
        var player = await audioService.Players.GetPlayerAsync<MewdekoPlayer>(guildId);
        if (player == null)
            return NotFound("No active player found");

        await player.SeekAsync(player.CurrentTrack.Duration);

        // Notify clients of track change
        await eventManager.BroadcastPlayerUpdate(guildId);

        return Ok();
    }

    /// <summary>
    ///     Goes to previous track
    /// </summary>
    /// <param name="guildId">The Discord guild ID</param>
    /// <returns>OK result when previous operation is successful</returns>
    [HttpPost("previous")]
    [Authorize("ApiKeyPolicy")]
    public async Task<IActionResult> PreviousTrack(ulong guildId)
    {
        var player = await audioService.Players.GetPlayerAsync<MewdekoPlayer>(guildId);
        if (player == null)
            return NotFound("No active player found");

        // Implement previous track logic
        // This depends on your implementation of player.PreviousAsync() or similar

        // Notify clients of track change
        await eventManager.BroadcastPlayerUpdate(guildId);

        return Ok();
    }

    /// <summary>
    ///     Shuffles the queue
    /// </summary>
    /// <param name="guildId">The Discord guild ID</param>
    /// <returns>OK result when shuffle is successful</returns>
    [HttpPost("shuffle")]
    [Authorize("ApiKeyPolicy")]
    public async Task<IActionResult> ShuffleQueue(ulong guildId)
    {
        var queue = await cache.GetMusicQueue(guildId);
        if (queue.Count <= 1)
            return Ok(); // Nothing to shuffle

        // Get the current track (if any)
        var currentTrack = queue.FirstOrDefault();

        // Remove current track from the shuffle
        if (currentTrack != null)
        {
            queue.Remove(currentTrack);
        }

        // Shuffle the remaining tracks
        var random = new Random();
        var shuffled = queue.OrderBy(x => random.Next()).ToList();

        // Reset indices
        for (var i = 0; i < shuffled.Count; i++)
        {
            shuffled[i].Index = i + 1;
        }

        // Re-add current track if it existed
        if (currentTrack != null)
        {
            currentTrack.Index = 0;
            shuffled.Insert(0, currentTrack);
        }

        await cache.SetMusicQueue(guildId, shuffled);

        // Notify clients of queue change
        await eventManager.BroadcastPlayerUpdate(guildId);

        return Ok();
    }

    /// <summary>
    ///     Sets the repeat mode
    /// </summary>
    /// <param name="guildId">The Discord guild ID</param>
    /// <param name="mode">The repeat mode (0=Off, 1=Track, 2=Queue)</param>
    /// <returns>OK result when repeat mode is set</returns>
    [HttpPost("repeat/{mode}")]
    [Authorize("ApiKeyPolicy")]
    public async Task<IActionResult> SetRepeatMode(ulong guildId, string mode)
    {
        var settings = await cache.GetMusicPlayerSettings(guildId);

        switch (mode.ToLower())
        {
            case "off":
            case "0":
                settings.PlayerRepeat = 0;
                break;
            case "track":
            case "1":
                settings.PlayerRepeat = 1;
                break;
            case "queue":
            case "2":
                settings.PlayerRepeat = 2;
                break;
            default:
                return BadRequest("Invalid repeat mode. Valid modes: off, track, queue");
        }

        await cache.SetMusicPlayerSettings(guildId, settings);

        // Notify clients of settings change
        await eventManager.BroadcastPlayerUpdate(guildId);

        return Ok(new
        {
            RepeatMode = settings.PlayerRepeat
        });
    }

    /// <summary>
    ///     Gets or sets player settings
    /// </summary>
    /// <param name="guildId">The Discord guild ID</param>
    /// <returns>The current music player settings</returns>
    [HttpGet("settings")]
    [Authorize("ApiKeyPolicy")]
    public async Task<IActionResult> GetSettings(ulong guildId)
    {
        var settings = await cache.GetMusicPlayerSettings(guildId);
        return Ok(settings);
    }

    /// <summary>
    ///     Updates player settings
    /// </summary>
    /// <param name="guildId">The Discord guild ID</param>
    /// <param name="settings">The new settings to apply</param>
    /// <returns>The updated settings</returns>
    [HttpPost("settings")]
    [Authorize("ApiKeyPolicy")]
    public async Task<IActionResult> UpdateSettings(ulong guildId, [FromBody] MusicPlayerSetting settings)
    {
        var player = await audioService.Players.GetPlayerAsync<MewdekoPlayer>(guildId);
        await player.SetMusicSettings(guildId, settings);

        // Notify clients of settings change
        await eventManager.BroadcastPlayerUpdate(guildId);

        return Ok(settings);
    }

    /// <summary>
    ///     Gets all TTS settings for the guild including guild-wide and per-VC settings
    /// </summary>
    /// <param name="guildId">The Discord guild ID</param>
    /// <returns>The TTS settings</returns>
    [HttpGet("tts")]
    [Authorize("ApiKeyPolicy")]
    public async Task<IActionResult> GetTtsSettings(ulong guildId)
    {
        var settings = await GetOrCreateMusicSettings(guildId);
        await using var db = await dbFactory.CreateConnectionAsync();
        var vcSettings = await db.GetTable<TtsVoiceChannelSetting>()
            .Where(x => x.GuildId == guildId)
            .ToListAsync();

        return Ok(new
        {
            settings.TtsVolume,
            settings.TtsSpeed,
            settings.TtsDefaultVoice,
            settings.TtsReplyContext,
            settings.TtsAttachmentNarration,
            settings.TtsConsecutiveGrouping,
            settings.TtsMaxQueueSize,
            settings.TtsRoleId,
            VoiceChannels = vcSettings.Select(vc => new
            {
                vc.VoiceChannelId,
                vc.Enabled,
                vc.LinkedTextChannelId,
                vc.AnnounceJoinLeave,
                vc.JoinFormat,
                vc.LeaveFormat
            })
        });
    }

    /// <summary>
    ///     Updates guild-wide TTS settings
    /// </summary>
    /// <param name="guildId">The Discord guild ID</param>
    /// <param name="request">The TTS settings to update</param>
    /// <returns>The updated settings</returns>
    [HttpPost("tts/settings")]
    [Authorize("ApiKeyPolicy")]
    public async Task<IActionResult> UpdateTtsSettings(ulong guildId, [FromBody] TtsGuildSettingsRequest request)
    {
        var settings = await GetOrCreateMusicSettings(guildId);

        if (request.Volume.HasValue) settings.TtsVolume = request.Volume.Value;
        if (request.Speed.HasValue) settings.TtsSpeed = request.Speed.Value;
        if (request.DefaultVoice != null)
            settings.TtsDefaultVoice = request.DefaultVoice == "" ? null : request.DefaultVoice;
        if (request.ReplyContext.HasValue) settings.TtsReplyContext = request.ReplyContext.Value;
        if (request.AttachmentNarration.HasValue) settings.TtsAttachmentNarration = request.AttachmentNarration.Value;
        if (request.ConsecutiveGrouping.HasValue) settings.TtsConsecutiveGrouping = request.ConsecutiveGrouping.Value;
        if (request.MaxQueueSize.HasValue) settings.TtsMaxQueueSize = request.MaxQueueSize.Value;
        if (request.RoleId != null) settings.TtsRoleId = request.RoleId == 0 ? null : request.RoleId;

        await using var db = await dbFactory.CreateConnectionAsync();
        await db.UpdateAsync(settings);
        await cache.SetMusicPlayerSettings(guildId, settings);

        return Ok(new
        {
            Message = "TTS settings updated"
        });
    }

    /// <summary>
    ///     Creates or updates TTS settings for a voice channel
    /// </summary>
    /// <param name="guildId">The Discord guild ID</param>
    /// <param name="request">The voice channel TTS settings</param>
    /// <returns>The updated VC settings</returns>
    [HttpPost("tts/vc")]
    [Authorize("ApiKeyPolicy")]
    public async Task<IActionResult> UpsertTtsVcSetting(ulong guildId, [FromBody] TtsVcSettingRequest request)
    {
        var setting = new TtsVoiceChannelSetting
        {
            GuildId = guildId,
            VoiceChannelId = request.VoiceChannelId,
            Enabled = request.Enabled,
            LinkedTextChannelId = request.LinkedTextChannelId,
            AnnounceJoinLeave = request.AnnounceJoinLeave,
            JoinFormat = request.JoinFormat,
            LeaveFormat = request.LeaveFormat
        };

        await using var db = await dbFactory.CreateConnectionAsync();
        var existing = await db.GetTable<TtsVoiceChannelSetting>()
            .FirstOrDefaultAsync(x => x.GuildId == guildId && x.VoiceChannelId == request.VoiceChannelId);

        if (existing is not null)
        {
            setting.Id = existing.Id;
            await db.UpdateAsync(setting);
        }
        else
        {
            await db.InsertAsync(setting);
        }

        return Ok(new
        {
            Message = "Voice channel TTS settings updated"
        });
    }

    /// <summary>
    ///     Removes TTS settings for a voice channel
    /// </summary>
    /// <param name="guildId">The Discord guild ID</param>
    /// <param name="voiceChannelId">The voice channel ID</param>
    /// <returns>OK result when settings are removed</returns>
    [HttpDelete("tts/vc/{voiceChannelId}")]
    [Authorize("ApiKeyPolicy")]
    public async Task<IActionResult> RemoveTtsVcSetting(ulong guildId, ulong voiceChannelId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        await db.GetTable<TtsVoiceChannelSetting>()
            .DeleteAsync(x => x.GuildId == guildId && x.VoiceChannelId == voiceChannelId);

        return Ok(new
        {
            Message = "Voice channel TTS settings removed"
        });
    }

    /// <summary>
    ///     Sets a user's TTS voice for this guild
    /// </summary>
    /// <param name="guildId">The Discord guild ID</param>
    /// <param name="userId">The user ID</param>
    /// <param name="request">The voice name, or null to reset</param>
    /// <returns>The updated user voice</returns>
    [HttpPost("tts/user/{userId}/voice")]
    [Authorize("ApiKeyPolicy")]
    public async Task<IActionResult> SetTtsUserVoice(ulong guildId, ulong userId, [FromBody] TtsVoiceRequest request)
    {
        await UpsertTtsUserSettingAsync(guildId, userId, s => s.Voice = request.Voice);

        return Ok(new
        {
            UserId = userId, request.Voice
        });
    }

    /// <summary>
    ///     Blocks or unblocks a user from TTS
    /// </summary>
    /// <param name="guildId">The Discord guild ID</param>
    /// <param name="userId">The user ID</param>
    /// <param name="blocked">Whether to block or unblock</param>
    /// <returns>The updated block status</returns>
    [HttpPost("tts/user/{userId}/block/{blocked}")]
    [Authorize("ApiKeyPolicy")]
    public async Task<IActionResult> SetTtsUserBlocked(ulong guildId, ulong userId, bool blocked)
    {
        await UpsertTtsUserSettingAsync(guildId, userId, s => s.IsBlocked = blocked);

        return Ok(new
        {
            UserId = userId, IsBlocked = blocked
        });
    }

    /// <summary>
    ///     Gets a user's TTS settings for this guild
    /// </summary>
    /// <param name="guildId">The Discord guild ID</param>
    /// <param name="userId">The user ID</param>
    /// <returns>The user's TTS settings</returns>
    [HttpGet("tts/user/{userId}")]
    [Authorize("ApiKeyPolicy")]
    public async Task<IActionResult> GetTtsUserSetting(ulong guildId, ulong userId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var setting = await db.GetTable<TtsUserSetting>()
                          .FirstOrDefaultAsync(x => x.GuildId == guildId && x.UserId == userId)
                      ?? new TtsUserSetting
                      {
                          GuildId = guildId, UserId = userId
                      };

        return Ok(new
        {
            setting.UserId, setting.Voice, setting.IsBlocked
        });
    }

    /// <summary>
    ///     Gets all blocked TTS users for this guild
    /// </summary>
    /// <param name="guildId">The Discord guild ID</param>
    /// <returns>List of blocked users</returns>
    [HttpGet("tts/blocked")]
    [Authorize("ApiKeyPolicy")]
    public async Task<IActionResult> GetTtsBlockedUsers(ulong guildId)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound("Guild not found");

        await using var db = await dbFactory.CreateConnectionAsync();
        var blockedUsers = await db.GetTable<TtsUserSetting>()
            .Where(x => x.GuildId == guildId && x.IsBlocked)
            .ToListAsync();

        return Ok(blockedUsers.Select(u => new
        {
            u.UserId, u.Voice, u.IsBlocked
        }));
    }

    /// <summary>
    ///     Searches available TTS voices from Flowery TTS API
    /// </summary>
    /// <param name="guildId">The Discord guild ID</param>
    /// <param name="search">The search query (name, language, gender, or source)</param>
    /// <returns>Matching voices</returns>
    [HttpGet("tts/voices")]
    [Authorize("ApiKeyPolicy")]
    public async Task<IActionResult> SearchTtsVoices(ulong guildId, [FromQuery] string search)
    {
        if (string.IsNullOrWhiteSpace(search))
            return BadRequest("Search query is required");

        try
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.UserAgent.ParseAdd("Mewdeko/1.0");
            var json = await http.GetStringAsync("https://api.flowery.pw/v1/tts/voices");
            var response = JsonSerializer.Deserialize<JsonElement>(json);

            if (!response.TryGetProperty("voices", out var voicesArray))
                return Ok(Array.Empty<object>());

            var results = new List<object>();
            foreach (var v in voicesArray.EnumerateArray())
            {
                var name = v.GetProperty("name").GetString() ?? "";
                var gender = v.TryGetProperty("gender", out var g) ? g.GetString() : null;
                var source = v.TryGetProperty("source", out var s) ? s.GetString() : null;
                var langName = v.TryGetProperty("language", out var l) && l.TryGetProperty("name", out var ln)
                    ? ln.GetString()
                    : null;
                var langCode = l.TryGetProperty("code", out var lc) ? lc.GetString() : null;

                if (source == "SAM")
                    continue;

                if (name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    (gender?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (langName?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (langCode?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (source?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false))
                {
                    results.Add(new
                    {
                        Name = name,
                        Gender = gender,
                        Source = source,
                        Language = langName,
                        LanguageCode = langCode
                    });
                }
            }

            return Ok(results);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error searching TTS voices");
            return StatusCode(500, "Failed to fetch voices from Flowery TTS API");
        }
    }

    /// <summary>
    ///     Gets or sets filters
    /// </summary>
    /// <param name="guildId">The Discord guild ID</param>
    /// <param name="filterName">The name of the filter to toggle</param>
    /// <param name="enable">Whether to enable or disable the filter</param>
    /// <returns>The filter status</returns>
    [HttpPost("filter/{filterName}")]
    [Authorize("ApiKeyPolicy")]
    public async Task<IActionResult> ToggleFilter(ulong guildId, string filterName, [FromBody] bool enable)
    {
        var player = await audioService.Players.GetPlayerAsync<MewdekoPlayer>(guildId);
        if (player == null)
            return NotFound("No active player found");

        switch (filterName.ToLower())
        {
            case "bass":
                player.Filters.Equalizer = enable
                    ? new EqualizerFilterOptions(new Equalizer
                    {
                        [0] = 0.6f, [1] = 0.67f, [2] = 0.67f, [3] = 0.4f
                    })
                    : null;
                break;
            case "nightcore":
                player.Filters.Timescale = enable
                    ? new TimescaleFilterOptions
                    {
                        Speed = 1.2f, Pitch = 1.2f, Rate = 1.0f
                    }
                    : null;
                break;
            case "vaporwave":
                player.Filters.Timescale = enable
                    ? new TimescaleFilterOptions
                    {
                        Speed = 0.8f, Pitch = 0.8f, Rate = 1.0f
                    }
                    : null;
                break;
            case "karaoke":
                player.Filters.Karaoke = enable
                    ? new KaraokeFilterOptions
                    {
                        Level = 1.0f, MonoLevel = 1.0f, FilterBand = 220.0f, FilterWidth = 100.0f
                    }
                    : null;
                break;
            case "tremolo":
                player.Filters.Tremolo = enable
                    ? new TremoloFilterOptions
                    {
                        Depth = 0.5f, Frequency = 2.0f
                    }
                    : null;
                break;
            case "vibrato":
                player.Filters.Vibrato = enable
                    ? new VibratoFilterOptions
                    {
                        Depth = 0.5f, Frequency = 2.0f
                    }
                    : null;
                break;
            case "rotation":
                player.Filters.Rotation = enable
                    ? new RotationFilterOptions
                    {
                        Frequency = 0.2f
                    }
                    : null;
                break;
            case "distortion":
                player.Filters.Distortion = enable
                    ? new DistortionFilterOptions
                    {
                        SinOffset = 0.0f,
                        SinScale = 1.0f,
                        CosOffset = 0.0f,
                        CosScale = 1.0f,
                        TanOffset = 0.0f,
                        TanScale = 1.0f,
                        Offset = 0.0f,
                        Scale = 5.0f
                    }
                    : null;
                break;
            case "channelmix":
                player.Filters.ChannelMix = enable
                    ? new ChannelMixFilterOptions
                    {
                        LeftToLeft = 1.0f, LeftToRight = 0.5f, RightToLeft = 0.5f, RightToRight = 1.0f
                    }
                    : null;
                break;
            default:
                return BadRequest("Unknown filter");
        }

        await player.Filters.CommitAsync();

        // Notify clients of filter change
        await eventManager.BroadcastPlayerUpdate(guildId);

        return Ok(new
        {
            Filter = filterName, Enabled = enable
        });
    }

    private async Task<MusicPlayerSetting> GetOrCreateMusicSettings(ulong guildId)
    {
        var settings = await cache.GetMusicPlayerSettings(guildId);
        if (settings is not null)
            return settings;

        await using var db = await dbFactory.CreateConnectionAsync();
        settings = await db.MusicPlayerSettings
            .FirstOrDefaultAsync(x => x.GuildId == guildId);

        if (settings is null)
        {
            settings = new MusicPlayerSetting
            {
                GuildId = guildId
            };
            await db.InsertAsync(settings);
        }

        await cache.SetMusicPlayerSettings(guildId, settings);
        return settings;
    }

    private async Task UpsertTtsUserSettingAsync(ulong guildId, ulong userId, Action<TtsUserSetting> update)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var existing = await db.GetTable<TtsUserSetting>()
            .FirstOrDefaultAsync(x => x.GuildId == guildId && x.UserId == userId);

        if (existing is not null)
        {
            update(existing);
            await db.UpdateAsync(existing);
        }
        else
        {
            var setting = new TtsUserSetting
            {
                GuildId = guildId, UserId = userId
            };
            update(setting);
            await db.InsertAsync(setting);
        }
    }
}