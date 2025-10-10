using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using Lavalink4NET;
using Lavalink4NET.Events.Players;
using Lavalink4NET.Players;
using Mewdeko.Modules.Music.CustomPlayer;
using Microsoft.AspNetCore.Http;

namespace Mewdeko.Services;

/// <summary>
///     Manages real-time music event notifications for WebSockets and SSE
/// </summary>
public class MusicEventManager : INService, IDisposable
{
    private readonly IAudioService audioService;

    // Semaphore to prevent simultaneous broadcasts
    private readonly SemaphoreSlim broadcastSemaphore = new(1, 1);
    private readonly IDataCache cache;
    private readonly DiscordShardedClient client;
    private readonly ILogger<MusicEventManager> logger;

    // Track active guilds with music players
    private readonly ConcurrentDictionary<ulong, Timer> positionUpdateTimers = new();

    // Track SSE connections by guild ID - store both callback and userId
    private readonly
        ConcurrentDictionary<ulong, ConcurrentDictionary<string, (Func<object, Task> Callback, ulong UserId)>>
        sseCallbacks = new();

    // Track WebSocket connections by guild ID - store both WebSocket and userId
    private readonly ConcurrentDictionary<ulong, ConcurrentDictionary<string, (WebSocket Socket, ulong UserId)>>
        webSocketConnections = new();

    /// <summary>
    ///     Constructor
    /// </summary>
    /// <param name="cache">The cache service.</param>
    /// <param name="audioService">The audioservice service.</param>
    /// <param name="client">The Discord client instance.</param>
    /// <param name="logger">The logger instance for structured logging.</param>
    public MusicEventManager(
        IDataCache cache,
        IAudioService audioService, DiscordShardedClient client, ILogger<MusicEventManager> logger)
    {
        this.cache = cache;
        this.audioService = audioService;
        this.client = client;
        this.logger = logger;

        // Listen for Lavalink player events
        this.audioService.TrackStarted += OnTrackStarted;
        this.audioService.TrackEnded += OnTrackEnded;
    }

    /// <summary>
    ///     Cleanup resources
    /// </summary>
    public void Dispose()
    {
        // Clean up event handlers
        audioService.TrackStarted -= OnTrackStarted;
        audioService.TrackEnded -= OnTrackEnded;

        // Dispose all timers
        foreach (var timer in positionUpdateTimers.Values)
        {
            timer.Dispose();
        }

        positionUpdateTimers.Clear();
        broadcastSemaphore.Dispose();
    }

    /// <summary>
    ///     Handle a new WebSocket connection
    /// </summary>
    /// <param name="guildId">The guild identifier.</param>
    /// <param name="userId">The userid identifier.</param>
    /// <param name="webSocket">The webSocket parameter.</param>
    /// <param name="context">The context parameter.</param>
    public async Task HandleWebSocketConnection(ulong guildId, ulong userId, WebSocket webSocket, HttpContext context)
    {
        var connectionId = Guid.NewGuid().ToString();

        try
        {
            // Register this connection with userId
            RegisterWebSocketConnection(guildId, connectionId, webSocket, userId);

            // Send initial player status
            await SendInitialStatus(guildId, userId, webSocket);

            // Ensure position updates are active for this guild
            EnsurePositionUpdatesActive(guildId);

            // Handle messages from client
            var buffer = new byte[1024 * 4];
            var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

            // Keep connection alive until closed
            while (!result.CloseStatus.HasValue)
            {
                // Process any messages (could implement commands here)
                if (result.MessageType == WebSocketMessageType.Text && result.Count > 0)
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    // Could handle commands here
                }

                // Get next message
                result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            }

            // Clean close
            await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
        }
        catch (WebSocketException ex)
        {
            logger.LogDebug(ex, "WebSocket closed: {ConnectionId}", connectionId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in WebSocket connection for guild {GuildId}", guildId);

            // Attempt to close socket if still open
            try
            {
                if (webSocket.State == WebSocketState.Open)
                {
                    await webSocket.CloseAsync(
                        WebSocketCloseStatus.InternalServerError,
                        "Internal server error",
                        CancellationToken.None);
                }
            }
            catch
            {
                // Ignore closing errors
            }
        }
        finally
        {
            // Unregister this connection
            UnregisterWebSocketConnection(guildId, connectionId);
        }
    }

    /// <summary>
    ///     Register a new WebSocket connection
    /// </summary>
    private void RegisterWebSocketConnection(ulong guildId, string connectionId, WebSocket webSocket, ulong userId)
    {
        var guildConnections = webSocketConnections.GetOrAdd(guildId, _ =>
            new ConcurrentDictionary<string, (WebSocket, ulong)>());
        guildConnections[connectionId] = (webSocket, userId);
        logger.LogDebug("Registered WebSocket connection {ConnectionId} for guild {GuildId} and user {UserId}",
            connectionId, guildId, userId);
    }

    /// <summary>
    ///     Unregister a WebSocket connection
    /// </summary>
    private void UnregisterWebSocketConnection(ulong guildId, string connectionId)
    {
        if (webSocketConnections.TryGetValue(guildId, out var connections))
        {
            connections.TryRemove(connectionId, out _);

            // If no more connections for this guild, remove the guild entry
            if (connections.IsEmpty)
            {
                webSocketConnections.TryRemove(guildId, out _);
                StopPositionUpdates(guildId);
            }

            logger.LogDebug("Unregistered WebSocket connection {ConnectionId} for guild {GuildId}", connectionId,
                guildId);
        }
    }

    /// <summary>
    ///     Register a new SSE connection
    /// </summary>
    /// <param name="guildId">The guild identifier.</param>
    /// <param name="connectionId">The connectionid string.</param>
    /// <param name="callback">The callback parameter.</param>
    /// <param name="userId">The userid identifier.</param>
    public void RegisterSseConnection(ulong guildId, string connectionId, Func<object, Task> callback, ulong userId)
    {
        var guildCallbacks = sseCallbacks.GetOrAdd(guildId, _ =>
            new ConcurrentDictionary<string, (Func<object, Task>, ulong)>());
        guildCallbacks[connectionId] = (callback, userId);

        // Ensure position updates are active for this guild
        EnsurePositionUpdatesActive(guildId);

        logger.LogDebug("Registered SSE connection {ConnectionId} for guild {GuildId} and user {UserId}",
            connectionId, guildId, userId);
    }

    /// <summary>
    ///     Unregister an SSE connection
    /// </summary>
    /// <param name="guildId">The guild identifier.</param>
    /// <param name="connectionId">The connectionid string.</param>
    public void UnregisterSseConnection(ulong guildId, string connectionId)
    {
        if (sseCallbacks.TryGetValue(guildId, out var callbacks))
        {
            callbacks.TryRemove(connectionId, out _);

            // If no more callbacks for this guild, remove the guild entry
            if (callbacks.IsEmpty)
            {
                sseCallbacks.TryRemove(guildId, out _);

                // If no more WebSocket connections either, stop position updates
                if (!webSocketConnections.ContainsKey(guildId) || webSocketConnections[guildId].IsEmpty)
                {
                    StopPositionUpdates(guildId);
                }
            }

            logger.LogDebug("Unregistered SSE connection {ConnectionId} for guild {GuildId}", connectionId, guildId);
        }
    }

    /// <summary>
    ///     Ensure position updates are active for a guild
    /// </summary>
    private void EnsurePositionUpdatesActive(ulong guildId)
    {
        // If we already have a timer for this guild, do nothing
        if (positionUpdateTimers.ContainsKey(guildId))
            return;

        // Create a new timer to periodically broadcast position updates
        var timer = new Timer(
            async _ => await BroadcastPositionUpdate(guildId),
            null,
            0,
            500);

        positionUpdateTimers[guildId] = timer;
        logger.LogDebug("Started position updates for guild {GuildId}", guildId);
    }

    /// <summary>
    ///     Stop position updates for a guild
    /// </summary>
    private void StopPositionUpdates(ulong guildId)
    {
        if (positionUpdateTimers.TryRemove(guildId, out var timer))
        {
            timer.Dispose();
            logger.LogDebug("Stopped position updates for guild {GuildId}", guildId);
        }
    }

    /// <summary>
    ///     Broadcast position updates for a guild
    /// </summary>
    private async Task BroadcastPositionUpdate(ulong guildId)
    {
        try
        {
            var hasWebSocketClients =
                webSocketConnections.TryGetValue(guildId, out var connections) && connections.Count > 0;
            var hasSseClients = sseCallbacks.TryGetValue(guildId, out var callbacks) && callbacks.Count > 0;

            // Skip if no clients
            if (!hasWebSocketClients && !hasSseClients)
            {
                StopPositionUpdates(guildId);
                return;
            }

            var player = await audioService.Players.GetPlayerAsync<MewdekoPlayer>(guildId);
            var queue = await cache.GetMusicQueue(guildId);
            if (player == null)
            {
                StopPositionUpdates(guildId);
                return;
            }

            // Only send position updates if the player is playing
            if (player.State != PlayerState.Playing)
                return;

            var currentTrack = await cache.GetCurrentTrack(guildId);
            var settings = await cache.GetMusicPlayerSettings(guildId);
            var botVoiceChannel = player.VoiceChannelId;
            var guild = client.GetGuild(guildId);

            // Send to WebSocket clients
            if (hasWebSocketClients)
            {
                var deadConnections = new List<string>();

                foreach (var (id, (socket, userId)) in connections)
                {
                    if (socket.State == WebSocketState.Open)
                    {
                        try
                        {
                            // Check if this user is in the voice channel
                            var user = guild?.GetUser(userId);
                            var isInVoiceChannel = user?.VoiceChannel?.Id == botVoiceChannel;

                            // Create a user-specific update
                            var positionUpdate = new
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
                                IsInVoiceChannel = isInVoiceChannel
                            };

                            var jsonString = JsonSerializer.Serialize(positionUpdate);
                            var buffer = Encoding.UTF8.GetBytes(jsonString);

                            await socket.SendAsync(
                                new ArraySegment<byte>(buffer),
                                WebSocketMessageType.Text,
                                true,
                                CancellationToken.None);
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Error sending position update to WebSocket client {ConnectionId}", id);
                            deadConnections.Add(id);
                        }
                    }
                    else
                    {
                        deadConnections.Add(id);
                    }
                }

                // Clean up dead connections
                foreach (var id in deadConnections)
                {
                    connections.TryRemove(id, out _);
                }

                // If no connections left, stop updates
                if (connections.IsEmpty)
                {
                    webSocketConnections.TryRemove(guildId, out _);

                    if (!sseCallbacks.ContainsKey(guildId) || sseCallbacks[guildId].IsEmpty)
                    {
                        StopPositionUpdates(guildId);
                    }
                }
            }

            // Send to SSE clients
            if (hasSseClients)
            {
                var deadCallbacks = new List<string>();

                foreach (var (id, (callback, userId)) in callbacks)
                {
                    try
                    {
                        // Check if this user is in the voice channel
                        var user = guild?.GetUser(userId);
                        var isInVoiceChannel = user?.VoiceChannel?.Id == botVoiceChannel;

                        // Create a user-specific update
                        var positionUpdate = new
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
                            IsInVoiceChannel = isInVoiceChannel
                        };

                        await callback(positionUpdate);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error sending position update to SSE client {ConnectionId}", id);
                        deadCallbacks.Add(id);
                    }
                }

                // Clean up dead callbacks
                foreach (var id in deadCallbacks)
                {
                    callbacks.TryRemove(id, out _);
                }

                // If no callbacks left, stop updates
                if (callbacks.IsEmpty)
                {
                    sseCallbacks.TryRemove(guildId, out _);

                    if (!webSocketConnections.ContainsKey(guildId) || webSocketConnections[guildId].IsEmpty)
                    {
                        StopPositionUpdates(guildId);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error broadcasting position update for guild {GuildId}", guildId);
        }
    }

    /// <summary>
    ///     Send initial player status to a client
    /// </summary>
    private async Task SendInitialStatus(ulong guildId, ulong userId, WebSocket webSocket)
    {
        try
        {
            var guild = client.GetGuild(guildId);
            var user = guild?.GetUser(userId);
            var botUser = guild?.GetUser(client.CurrentUser.Id);
            var botVoiceChannel = botUser?.VoiceChannel;

            var player = await audioService.Players.GetPlayerAsync<MewdekoPlayer>(guildId);

            object status;

            if (player == null)
            {
                // Bot is in a voice channel but not playing
                if (botVoiceChannel != null)
                {
                    var isInVoiceChannel = user?.VoiceChannel?.Id == botVoiceChannel.Id;
                    var idleSettings = await cache.GetMusicPlayerSettings(guildId);

                    status = new
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
                else
                {
                    // Bot is not in any voice channel - don't send status
                    return;
                }
            }
            else
            {
                var voiceChannelId = player.VoiceChannelId;
                var isInVoiceChannel = user?.VoiceChannel?.Id == voiceChannelId;

                var currentTrack = await cache.GetCurrentTrack(guildId);
                var queue = await cache.GetMusicQueue(guildId);
                var settings = await cache.GetMusicPlayerSettings(guildId);

                logger.LogInformation("Current track from cache: {Track}",
                    JsonSerializer.Serialize(currentTrack));
                logger.LogInformation(
                    "User in voice channel: {InVoiceChannel}, User: {UserId}, Bot channel: {BotChannel}",
                    isInVoiceChannel, userId, voiceChannelId);

                status = new
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
                    IsInVoiceChannel = isInVoiceChannel,
                    BotInChannel = true,
                    ChannelId = voiceChannelId,
                    ChannelName = botVoiceChannel?.Name ?? guild?.GetChannel(voiceChannelId)?.Name
                };
            }

            var jsonString = JsonSerializer.Serialize(status);
            var buffer = Encoding.UTF8.GetBytes(jsonString);
            await webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true,
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending initial status to client for guild {GuildId}", guildId);
        }
    }

    /// <summary>
    ///     Broadcast player update to all connected clients for a guild
    /// </summary>
    /// <param name="guildId">The guild identifier.</param>
    public async Task BroadcastPlayerUpdate(ulong guildId)
    {
        // Use semaphore to prevent simultaneous broadcasts
        await broadcastSemaphore.WaitAsync();

        try
        {
            var hasWebSocketClients =
                webSocketConnections.TryGetValue(guildId, out var connections) && connections.Count > 0;
            var hasSseClients = sseCallbacks.TryGetValue(guildId, out var callbacks) && callbacks.Count > 0;

            // Skip if no clients
            if (!hasWebSocketClients && !hasSseClients) return;

            var player = await audioService.Players.GetPlayerAsync<MewdekoPlayer>(guildId);
            if (player == null) return;

            var currentTrack = await cache.GetCurrentTrack(guildId);
            var queue = await cache.GetMusicQueue(guildId);
            var settings = await cache.GetMusicPlayerSettings(guildId);
            var botVoiceChannel = player.VoiceChannelId;
            var guild = client.GetGuild(guildId);

            // Send to WebSocket clients
            if (hasWebSocketClients)
            {
                var deadConnections = new List<string>();

                foreach (var (id, (socket, userId)) in connections)
                {
                    if (socket.State == WebSocketState.Open)
                    {
                        try
                        {
                            // Check if this user is in the voice channel
                            var user = guild?.GetUser(userId);
                            var isInVoiceChannel = user?.VoiceChannel?.Id == botVoiceChannel;

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
                                IsInVoiceChannel = isInVoiceChannel
                            };

                            var jsonString = JsonSerializer.Serialize(status);
                            var buffer = Encoding.UTF8.GetBytes(jsonString);

                            await socket.SendAsync(
                                new ArraySegment<byte>(buffer),
                                WebSocketMessageType.Text,
                                true,
                                CancellationToken.None);
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Error sending WebSocket update to client {ConnectionId}", id);
                            deadConnections.Add(id);
                        }
                    }
                    else
                    {
                        deadConnections.Add(id);
                    }
                }

                // Clean up dead connections
                foreach (var id in deadConnections)
                {
                    connections.TryRemove(id, out _);
                }
            }

            // Send to SSE clients
            if (hasSseClients)
            {
                var deadCallbacks = new List<string>();

                foreach (var (id, (callback, userId)) in callbacks)
                {
                    try
                    {
                        // Check if this user is in the voice channel
                        var user = guild?.GetUser(userId);
                        var isInVoiceChannel = user?.VoiceChannel?.Id == botVoiceChannel;

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
                            IsInVoiceChannel = isInVoiceChannel
                        };

                        await callback(status);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error sending SSE update to client {ConnectionId}", id);
                        deadCallbacks.Add(id);
                    }
                }

                // Clean up dead callbacks
                foreach (var id in deadCallbacks)
                {
                    callbacks.TryRemove(id, out _);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error broadcasting player update for guild {GuildId}", guildId);
        }
        finally
        {
            broadcastSemaphore.Release();
        }
    }

    /// <summary>
    ///     Handle track started events
    /// </summary>
    private async Task OnTrackStarted(object sender, TrackStartedEventArgs eventArgs)
    {
        if (eventArgs.Player is MewdekoPlayer player)
        {
            await BroadcastPlayerUpdate(player.GuildId);
            EnsurePositionUpdatesActive(player.GuildId);
        }
    }

    /// <summary>
    ///     Handle track ended events
    /// </summary>
    private async Task OnTrackEnded(object sender, TrackEndedEventArgs eventArgs)
    {
        if (eventArgs.Player is MewdekoPlayer player)
        {
            await BroadcastPlayerUpdate(player.GuildId);
        }
    }

    /// <summary>
    ///     Broadcast disconnection to all connected clients for a guild
    /// </summary>
    public async Task BroadcastDisconnection(ulong guildId)
    {
        await broadcastSemaphore.WaitAsync();

        try
        {
            var hasWebSocketClients =
                webSocketConnections.TryGetValue(guildId, out var connections) && connections.Count > 0;
            var hasSseClients = sseCallbacks.TryGetValue(guildId, out var callbacks) && callbacks.Count > 0;

            if (!hasWebSocketClients && !hasSseClients) return;

            // Create disconnection status
            var disconnectionStatus = new
            {
                CurrentTrack = (object)null,
                Queue = new List<object>(),
                State = 0, // Stopped
                Volume = 0,
                Position = TimeSpan.Zero,
                RepeatMode = 0,
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
                IsInVoiceChannel = false,
                BotInChannel = false,
                Disconnected = true // Special flag to indicate explicit disconnection
            };

            // Send to WebSocket clients
            if (hasWebSocketClients)
            {
                var deadConnections = new List<string>();

                foreach (var (id, (socket, userId)) in connections)
                {
                    if (socket.State == WebSocketState.Open)
                    {
                        try
                        {
                            var jsonString = JsonSerializer.Serialize(disconnectionStatus);
                            var buffer = Encoding.UTF8.GetBytes(jsonString);

                            await socket.SendAsync(
                                new ArraySegment<byte>(buffer),
                                WebSocketMessageType.Text,
                                true,
                                CancellationToken.None);
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Error sending disconnection to WebSocket client {ConnectionId}", id);
                            deadConnections.Add(id);
                        }
                    }
                    else
                    {
                        deadConnections.Add(id);
                    }
                }

                // Clean up dead connections
                foreach (var id in deadConnections)
                {
                    connections.TryRemove(id, out _);
                }
            }

            // Send to SSE clients
            if (hasSseClients)
            {
                var deadCallbacks = new List<string>();

                foreach (var (id, (callback, userId)) in callbacks)
                {
                    try
                    {
                        await callback(disconnectionStatus);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error sending disconnection to SSE client {ConnectionId}", id);
                        deadCallbacks.Add(id);
                    }
                }

                // Clean up dead callbacks
                foreach (var id in deadCallbacks)
                {
                    callbacks.TryRemove(id, out _);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error broadcasting disconnection for guild {GuildId}", guildId);
        }
        finally
        {
            broadcastSemaphore.Release();
        }
    }
}