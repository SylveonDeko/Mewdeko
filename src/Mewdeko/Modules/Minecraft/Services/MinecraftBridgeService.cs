using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using DataModel;
using Mewdeko.Modules.Minecraft.Common;
using Mewdeko.Services.Strings;
using Microsoft.AspNetCore.Http;

namespace Mewdeko.Modules.Minecraft.Services;

/// <summary>
///     Manages WebSocket connections from companion Minecraft plugins,
///     routing events between MC servers and Discord channels.
/// </summary>
/// <param name="minecraftService">The Minecraft service for server lookups and status recording.</param>
/// <param name="client">The Discord client.</param>
/// <param name="strings">The localization service.</param>
/// <param name="logger">The logger instance.</param>
public class MinecraftBridgeService(
    MinecraftService minecraftService,
    DiscordShardedClient client,
    GeneratedBotStrings strings,
    ILogger<MinecraftBridgeService> logger) : INService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true
    };

    private readonly ConcurrentDictionary<int, BridgeConnection> connections = new();

    /// <summary>
    ///     Handles an incoming WebSocket connection from a companion plugin.
    /// </summary>
    /// <param name="server">The authenticated server entry.</param>
    /// <param name="webSocket">The WebSocket connection.</param>
    /// <param name="context">The HTTP context.</param>
    public async Task HandleConnectionAsync(MinecraftServer server, WebSocket webSocket, HttpContext context)
    {
        var ct = context.RequestAborted;
        var connection = new BridgeConnection(server.Id, server.GuildId, server.Name, webSocket);

        connections[server.Id] = connection;
        logger.LogInformation("MC plugin connected: {ServerName} (guild {GuildId})", server.Name, server.GuildId);

        try
        {
            var hello = new BridgeMessage
            {
                Type = "hello", Timestamp = DateTime.UtcNow
            };
            await SendMessageAsync(webSocket, hello, ct);

            var buffer = new byte[4096];
            var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);

            while (!result.CloseStatus.HasValue)
            {
                if (result.MessageType == WebSocketMessageType.Text && result.Count > 0)
                {
                    var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    await ProcessIncomingMessageAsync(server, json);
                }

                result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
            }

            await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
        }
        catch (WebSocketException ex)
        {
            logger.LogDebug(ex, "Plugin WebSocket closed: {ServerName}", server.Name);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in plugin connection for {ServerName}", server.Name);
            try
            {
                if (webSocket.State == WebSocketState.Open)
                    await webSocket.CloseAsync(WebSocketCloseStatus.InternalServerError, "Internal error",
                        CancellationToken.None);
            }
            catch
            {
            }
        }
        finally
        {
            connections.TryRemove(server.Id, out _);
            logger.LogInformation("MC plugin disconnected: {ServerName} (guild {GuildId})", server.Name,
                server.GuildId);
        }
    }

    /// <summary>
    ///     Sends a chat message from Discord to the connected MC server.
    /// </summary>
    /// <param name="serverId">The server database ID.</param>
    /// <param name="user">The Discord user's display name.</param>
    /// <param name="message">The message content.</param>
    /// <param name="channel">The Discord channel name.</param>
    public async Task SendChatToServerAsync(int serverId, string user, string message, string channel)
    {
        if (!connections.TryGetValue(serverId, out var conn)) return;

        var msg = new DiscordChatMessage
        {
            Type = "chat", User = user, Message = message, Channel = channel
        };

        await SendMessageAsync(conn.Socket, msg, CancellationToken.None);
    }

    /// <summary>
    ///     Sends a command to the connected MC server via the plugin and waits for a response.
    /// </summary>
    /// <param name="serverId">The server database ID.</param>
    /// <param name="command">The command to execute.</param>
    /// <param name="timeout">The timeout in milliseconds.</param>
    /// <returns>The command response, or null if not connected or timed out.</returns>
    public async Task<string?> SendCommandAsync(int serverId, string command, int timeout = 5000)
    {
        if (!connections.TryGetValue(serverId, out var conn)) return null;

        var requestId = Guid.NewGuid().ToString("N")[..8];
        var tcs = new TaskCompletionSource<string>();
        conn.PendingCommands[requestId] = tcs;

        var msg = new BridgeCommandMessage
        {
            Type = "command", Id = requestId, Command = command
        };

        await SendMessageAsync(conn.Socket, msg, CancellationToken.None);

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(timeout));
        conn.PendingCommands.TryRemove(requestId, out _);

        return completed == tcs.Task ? tcs.Task.Result : null;
    }

    /// <summary>
    ///     Checks whether a server has an active plugin connection.
    /// </summary>
    /// <param name="serverId">The server database ID.</param>
    /// <returns>True if the plugin is connected.</returns>
    public bool IsConnected(int serverId)
    {
        return connections.TryGetValue(serverId, out var conn) && conn.Socket.State == WebSocketState.Open;
    }

    private async Task ProcessIncomingMessageAsync(MinecraftServer server, string json)
    {
        try
        {
            var baseMsg = JsonSerializer.Deserialize<BridgeMessage>(json, JsonOptions);
            if (baseMsg == null) return;

            var guild = client.GetGuild(server.GuildId);
            if (guild == null) return;

            var watchChannel = server.WatchChannelId.HasValue
                ? guild.GetTextChannel(server.WatchChannelId.Value)
                : null;

            switch (baseMsg.Type)
            {
                case "player_join":
                    var joinMsg = JsonSerializer.Deserialize<PlayerJoinMessage>(json, JsonOptions);
                    if (joinMsg != null && watchChannel != null)
                    {
                        var eb = new EmbedBuilder()
                            .WithOkColor()
                            .WithAuthor(joinMsg.Player, $"https://minotar.net/avatar/{joinMsg.Player}/32")
                            .WithDescription($"**{joinMsg.Player}** joined the server");
                        await watchChannel.SendMessageAsync(embed: eb.Build());
                    }

                    break;

                case "player_leave":
                    var leaveMsg = JsonSerializer.Deserialize<PlayerLeaveMessage>(json, JsonOptions);
                    if (leaveMsg != null && watchChannel != null)
                    {
                        var eb = new EmbedBuilder()
                            .WithErrorColor()
                            .WithAuthor(leaveMsg.Player, $"https://minotar.net/avatar/{leaveMsg.Player}/32")
                            .WithDescription($"**{leaveMsg.Player}** left the server");
                        await watchChannel.SendMessageAsync(embed: eb.Build());
                    }

                    break;

                case "chat":
                    var chatMsg = JsonSerializer.Deserialize<ChatMessage>(json, JsonOptions);
                    if (chatMsg != null && watchChannel != null)
                    {
                        await watchChannel.SendMessageAsync(
                            $"**{chatMsg.Player}**: {chatMsg.Message}",
                            allowedMentions: AllowedMentions.None);
                    }

                    break;

                case "death":
                    var deathMsg = JsonSerializer.Deserialize<DeathMessage>(json, JsonOptions);
                    if (deathMsg != null && watchChannel != null)
                    {
                        var eb = new EmbedBuilder()
                            .WithColor(new Color(128, 128, 128))
                            .WithDescription($":skull: {deathMsg.Message}");
                        await watchChannel.SendMessageAsync(embed: eb.Build());
                    }

                    break;

                case "advancement":
                    var advMsg = JsonSerializer.Deserialize<AdvancementMessage>(json, JsonOptions);
                    if (advMsg != null && watchChannel != null)
                    {
                        var eb = new EmbedBuilder()
                            .WithOkColor()
                            .WithDescription($":trophy: **{advMsg.Player}** earned **{advMsg.Advancement}**");
                        await watchChannel.SendMessageAsync(embed: eb.Build());
                    }

                    break;

                case "server_status":
                    var statusMsg = JsonSerializer.Deserialize<ServerStatusMessage>(json, JsonOptions);
                    if (statusMsg != null)
                    {
                        var status = new McServerStatus
                        {
                            IsOnline = true,
                            PlayersOnline = statusMsg.Players.Count,
                            PlayersMax = 0,
                            PlayerList = statusMsg.Players.Select(p => p.Name).ToList(),
                            PlayerUuids = statusMsg.Players.ToDictionary(p => p.Name, p => p.Uuid),
                            Latency = 0,
                            Version = "",
                            Software =
                                $"TPS: {statusMsg.Tps:F1} | Mem: {statusMsg.UsedMemory}MB/{statusMsg.MaxMemory}MB"
                        };
                        await minecraftService.RecordSnapshotAsync(server, status);
                    }

                    break;

                case "console_log":
                    break;

                case "command_response":
                    var cmdResp = JsonSerializer.Deserialize<CommandResponseMessage>(json, JsonOptions);
                    if (cmdResp != null && connections.TryGetValue(server.Id, out var conn)
                                        && conn.PendingCommands.TryRemove(cmdResp.Id, out var tcs))
                    {
                        tcs.TrySetResult(cmdResp.Response);
                    }

                    break;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing plugin message from {ServerName}", server.Name);
        }
    }

    private static async Task SendMessageAsync(WebSocket socket, object message, CancellationToken ct)
    {
        if (socket.State != WebSocketState.Open) return;

        var json = JsonSerializer.Serialize(message, JsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);
        await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct);
    }

    private class BridgeConnection(int serverId, ulong guildId, string serverName, WebSocket socket)
    {
        public int ServerId { get; } = serverId;
        public ulong GuildId { get; } = guildId;
        public string ServerName { get; } = serverName;
        public WebSocket Socket { get; } = socket;
        public ConcurrentDictionary<string, TaskCompletionSource<string>> PendingCommands { get; } = new();
    }
}