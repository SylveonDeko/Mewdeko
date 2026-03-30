using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Database.DbContextStuff;
using Mewdeko.Modules.Minecraft.Common;
using Mewdeko.Services.Settings;
using Mewdeko.Services.Strings;
using Microsoft.Extensions.Caching.Memory;
using ZiggyCreatures.Caching.Fusion;
using Embed = Discord.Embed;

namespace Mewdeko.Modules.Minecraft.Services;

/// <summary>
///     Service for managing Minecraft server monitoring, status queries, and player lookups.
/// </summary>
/// <param name="dbFactory">The database connection factory.</param>
/// <param name="client">The Discord client instance.</param>
/// <param name="cache">The memory cache instance.</param>
/// <param name="fusionCache">The distributed fusion cache for status data.</param>
/// <param name="httpFactory">The HTTP client factory.</param>
/// <param name="botConfig">The bot configuration service.</param>
/// <param name="strings">The localization service.</param>
/// <param name="logger">The logger instance.</param>
public class MinecraftService(
    IDataConnectionFactory dbFactory,
    DiscordShardedClient client,
    IMemoryCache cache,
    IFusionCache fusionCache,
    IHttpClientFactory httpFactory,
    BotConfigService botConfig,
    GeneratedBotStrings strings,
    ILogger<MinecraftService> logger)
    : INService, IReadyExecutor, IDisposable
{
    private const string ServersCacheKey = "mc_servers_{0}";
    private const string StatusCacheKey = "mc_status_{0}";
    private const string SnapshotsCacheKey = "mc_snapshots_{0}_{1}";
    private const string QueryRateLimitKey = "mc_ratelimit_{0}";
    private const int MaxQueriesPerMinute = 10;
    private readonly ConcurrentDictionary<int, Timer> watchTimers = new();
    private bool isDisposed;

    /// <summary>
    ///     Disposes of all watch timers.
    /// </summary>
    public void Dispose()
    {
        if (isDisposed) return;
        isDisposed = true;

        foreach (var timer in watchTimers.Values)
            timer.Dispose();

        watchTimers.Clear();
    }

    /// <summary>
    ///     Initializes watch timers and warms the status cache from persisted data.
    /// </summary>
    public async Task OnReadyAsync()
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();
            var allServers = await db.MinecraftServers.ToListAsync();

            foreach (var server in allServers)
            {
                if (!string.IsNullOrWhiteSpace(server.LastStatusJson))
                {
                    try
                    {
                        var status = JsonSerializer.Deserialize<McServerStatus>(server.LastStatusJson);
                        if (status != null)
                            await fusionCache.SetAsync(string.Format(StatusCacheKey, server.Id), status,
                                TimeSpan.FromHours(1));
                    }
                    catch
                    {
                        // ignored
                    }
                }

                if (server.WatchChannelId != null)
                    StartWatchTimer(server);
            }

            _ = Task.Run(() => CleanupSnapshotsAsync());

            logger.LogInformation("Minecraft Service Ready - {Count} servers cached, {WatchCount} being watched",
                allServers.Count, allServers.Count(s => s.WatchChannelId != null));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error initializing Minecraft service");
        }
    }

    /// <summary>
    ///     Validates that a server address is not a private/reserved IP range (SSRF prevention).
    ///     Respects the AllowPrivateMinecraftAddresses bot config setting.
    /// </summary>
    /// <param name="address">The address to validate.</param>
    /// <returns>True if the address is safe to connect to.</returns>
    public bool IsAddressSafe(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
            return false;

        if (botConfig.Data.AllowPrivateMinecraftAddresses)
            return true;

        if (IPAddress.TryParse(address, out var ip))
            return !IsPrivateOrReserved(ip);

        var lower = address.ToLowerInvariant();
        if (lower is "localhost" or "host.docker.internal" or "kubernetes.default")
            return false;
        if (lower.EndsWith(".local") || lower.EndsWith(".internal"))
            return false;

        try
        {
            var addresses = Dns.GetHostAddresses(address);
            return addresses.Length > 0 && addresses.All(a => !IsPrivateOrReserved(a));
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    ///     Checks rate limiting for server queries per guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>True if the query is allowed, false if rate limited.</returns>
    public async Task<bool> CheckQueryRateLimitAsync(ulong guildId)
    {
        var cacheKey = string.Format(QueryRateLimitKey, guildId);
        var count = await fusionCache.GetOrDefaultAsync<int>(cacheKey);
        if (count >= MaxQueriesPerMinute)
            return false;

        await fusionCache.SetAsync(cacheKey, count + 1, TimeSpan.FromMinutes(1));
        return true;
    }

    private static bool IsPrivateOrReserved(IPAddress ip)
    {
        var bytes = ip.GetAddressBytes();

        if (ip.AddressFamily == AddressFamily.InterNetwork && bytes.Length == 4)
        {
            if (bytes[0] == 10) return true;
            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return true;
            if (bytes[0] == 192 && bytes[1] == 168) return true;
            if (bytes[0] == 127) return true;
            if (bytes[0] == 0) return true;
            if (bytes[0] == 169 && bytes[1] == 254) return true;
            if (bytes[0] == 100 && bytes[1] >= 64 && bytes[1] <= 127) return true;
            if (bytes[0] == 198 && bytes[1] == 18) return true;
            if (bytes[0] >= 224) return true;
        }

        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (IPAddress.IsLoopback(ip)) return true;
            if (ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal) return true;
            if (bytes[0] == 0xfc || bytes[0] == 0xfd) return true;
        }

        return false;
    }

    /// <summary>
    ///     Queries a Java Edition Minecraft server using the Server List Ping protocol.
    /// </summary>
    /// <param name="address">The server address.</param>
    /// <param name="port">The server port.</param>
    /// <returns>The server status, or null if the server is unreachable.</returns>
    public async Task<McServerStatus?> QueryJavaServerAsync(string address, int port = 25565)
    {
        try
        {
            using var tcpClient = new TcpClient();
            tcpClient.ReceiveTimeout = 5000;
            tcpClient.SendTimeout = 5000;
            await tcpClient.ConnectAsync(address, port).WaitAsync(TimeSpan.FromSeconds(5));

            await using var stream = tcpClient.GetStream();

            var handshake = BuildHandshakePacket(address, port);
            await stream.WriteAsync(handshake);

            byte[] statusRequest = [0x01, 0x00];
            await stream.WriteAsync(statusRequest);

            var responseLength = await ReadVarIntAsync(stream);
            if (responseLength <= 0)
                return null;

            var responseData = new byte[responseLength];
            var totalRead = 0;
            while (totalRead < responseLength)
            {
                var read = await stream.ReadAsync(responseData.AsMemory(totalRead, responseLength - totalRead));
                if (read == 0) break;
                totalRead += read;
            }

            await ReadVarIntAsync(new MemoryStream(responseData));
            var jsonLength = await ReadVarIntAsync(new MemoryStream(responseData[GetVarIntSize(responseData)..]));
            ;

            var jsonStartOffset =
                GetVarIntSize(responseData) + GetVarIntSize(responseData[GetVarIntSize(responseData)..]);
            var json = Encoding.UTF8.GetString(responseData, jsonStartOffset, jsonLength);

            var status = JsonSerializer.Deserialize<McJavaResponse>(json);
            if (status == null) return null;

            var pingStart = DateTime.UtcNow;
            byte[] pingPacket = [0x09, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00];
            await stream.WriteAsync(pingPacket);
            try
            {
                var pongLength = await ReadVarIntAsync(stream);
                if (pongLength > 0)
                {
                    var pongData = new byte[pongLength];
                    await stream.ReadExactlyAsync(pongData);
                }
            }
            catch
            {
                // ignored
            }

            var latency = (int)(DateTime.UtcNow - pingStart).TotalMilliseconds;

            return new McServerStatus
            {
                IsOnline = true,
                Motd = status.Description?.Text ?? status.Description?.ToString() ?? "",
                PlayersOnline = status.Players?.Online ?? 0,
                PlayersMax = status.Players?.Max ?? 0,
                PlayerList = status.Players?.Sample?.Select(p => p.Name).ToList() ?? [],
                PlayerUuids = status.Players?.Sample?
                    .Where(p => !string.IsNullOrEmpty(p.Id) && p.Id != "00000000-0000-0000-0000-000000000000")
                    .ToDictionary(p => p.Name, p => p.Id.Replace("-", "")) ?? new Dictionary<string, string>(),
                Version = status.Version?.Name ?? "Unknown",
                Favicon = status.Favicon,
                Latency = latency
            };
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to query Java server {Address}:{Port}", address, port);
            return new McServerStatus
            {
                IsOnline = false
            };
        }
    }

    /// <summary>
    ///     Queries a Bedrock Edition Minecraft server using the Unconnected Ping protocol.
    /// </summary>
    /// <param name="address">The server address.</param>
    /// <param name="port">The server port.</param>
    /// <returns>The server status, or null if the server is unreachable.</returns>
    public async Task<McServerStatus?> QueryBedrockServerAsync(string address, int port = 19132)
    {
        try
        {
            using var udpClient = new UdpClient();
            udpClient.Client.ReceiveTimeout = 5000;
            udpClient.Client.SendTimeout = 5000;

            var pingStart = DateTime.UtcNow;

            var unconnectedPing = BuildBedrockPingPacket();
            await udpClient.SendAsync(unconnectedPing, unconnectedPing.Length, address, port);

            var receiveTask = udpClient.ReceiveAsync();
            if (await Task.WhenAny(receiveTask, Task.Delay(5000)) != receiveTask)
                return new McServerStatus
                {
                    IsOnline = false
                };

            var result = await receiveTask;
            var latency = (int)(DateTime.UtcNow - pingStart).TotalMilliseconds;

            var responseStr = Encoding.UTF8.GetString(result.Buffer, 35, result.Buffer.Length - 35);
            var parts = responseStr.Split(';');

            if (parts.Length < 6)
                return new McServerStatus
                {
                    IsOnline = false
                };

            return new McServerStatus
            {
                IsOnline = true,
                Motd = parts[1],
                PlayersOnline = int.TryParse(parts[4], out var online) ? online : 0,
                PlayersMax = int.TryParse(parts[5], out var max) ? max : 0,
                Version = parts[3],
                Latency = latency,
                PlayerList = []
            };
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to query Bedrock server {Address}:{Port}", address, port);
            return new McServerStatus
            {
                IsOnline = false
            };
        }
    }

    /// <summary>
    ///     Queries a Java Edition server using the GameSpy4 Query protocol (requires enable-query=true).
    /// </summary>
    /// <param name="address">The server address.</param>
    /// <param name="queryPort">The query port (defaults to server port).</param>
    /// <returns>The server status with extended info, or null if query is disabled/unreachable.</returns>
    public async Task<McServerStatus?> QueryFullStatusAsync(string address, int queryPort = 25565)
    {
        try
        {
            using var udpClient = new UdpClient();
            udpClient.Client.ReceiveTimeout = 3000;
            udpClient.Client.SendTimeout = 3000;
            udpClient.Connect(address, queryPort);

            var pingStart = DateTime.UtcNow;

            byte[] handshake = [0xFE, 0xFD, 0x09, 0x00, 0x00, 0x00, 0x01];
            await udpClient.SendAsync(handshake, handshake.Length);

            var handshakeReceive = udpClient.ReceiveAsync();
            if (await Task.WhenAny(handshakeReceive, Task.Delay(3000)) != handshakeReceive)
                return null;

            var handshakeResponse = await handshakeReceive;
            var challengeStr =
                Encoding.ASCII.GetString(handshakeResponse.Buffer, 5, handshakeResponse.Buffer.Length - 6);
            var challengeToken = int.Parse(challengeStr.Trim('\0'));

            var tokenBytes = BitConverter.GetBytes(challengeToken);
            if (BitConverter.IsLittleEndian) Array.Reverse(tokenBytes);

            byte[] fullStatRequest = [0xFE, 0xFD, 0x00, 0x00, 0x00, 0x00, 0x01, ..tokenBytes, 0x00, 0x00, 0x00, 0x00];
            await udpClient.SendAsync(fullStatRequest, fullStatRequest.Length);

            var statReceive = udpClient.ReceiveAsync();
            if (await Task.WhenAny(statReceive, Task.Delay(3000)) != statReceive)
                return null;

            var statResponse = await statReceive;
            var latency = (int)(DateTime.UtcNow - pingStart).TotalMilliseconds;

            return ParseFullStatResponse(statResponse.Buffer, latency);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Query protocol failed for {Address}:{Port}", address, queryPort);
            return null;
        }
    }

    /// <summary>
    ///     Queries a server based on its type, attempting Query protocol first for Java servers.
    /// </summary>
    /// <param name="server">The server database entry.</param>
    /// <returns>The server status.</returns>
    public async Task<McServerStatus?> QueryServerAsync(MinecraftServer server)
    {
        if (server.ServerType == (int)McServerType.Bedrock)
            return await QueryBedrockServerAsync(server.Address, server.Port);

        var queryPort = server.QueryPort > 0 ? server.QueryPort : server.Port;
        var queryResult = await QueryFullStatusAsync(server.Address, queryPort);
        if (queryResult is { IsOnline: true })
        {
            if (server.ServerType == (int)McServerType.Geyser)
                queryResult.IsGeyser = true;
            return queryResult;
        }

        var javaResult = await QueryJavaServerAsync(server.Address, server.Port);
        if (javaResult is { IsOnline: true } && server.ServerType == (int)McServerType.Geyser)
            javaResult.IsGeyser = true;

        return javaResult;
    }

    /// <summary>
    ///     Looks up a Minecraft player's UUID and skin by username using the Mojang API.
    /// </summary>
    /// <param name="username">The player's username.</param>
    /// <returns>The player profile, or null if not found.</returns>
    public async Task<McPlayerProfile?> LookupPlayerAsync(string username)
    {
        try
        {
            var http = httpFactory.CreateClient();
            var response = await http.GetStringAsync($"https://api.mojang.com/users/profiles/minecraft/{username}");
            var profile = JsonSerializer.Deserialize<MojangProfile>(response);
            if (profile == null) return null;

            return new McPlayerProfile
            {
                Username = profile.Name,
                Uuid = profile.Id,
                SkinUrl = $"https://minotar.net/armor/body/{profile.Name}/256",
                AvatarUrl = $"https://minotar.net/avatar/{profile.Name}/128"
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    ///     Adds a Minecraft server to a guild's configuration.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="name">The label for the server.</param>
    /// <param name="address">The server address.</param>
    /// <param name="port">The server port.</param>
    /// <param name="serverType">The server type (Java or Bedrock).</param>
    /// <returns>The created server entry.</returns>
    public async Task<MinecraftServer> AddServerAsync(ulong guildId, string name, string address, int port,
        McServerType serverType)
    {
        if (!IsAddressSafe(address))
            throw new InvalidOperationException(
                "That address is not allowed. Private, reserved, and local addresses are blocked.");

        await using var db = await dbFactory.CreateConnectionAsync();

        var existing = await db.MinecraftServers
            .FirstOrDefaultAsync(s => s.GuildId == guildId && s.Name == name.ToLowerInvariant());

        if (existing != null)
            throw new InvalidOperationException("A server with that name already exists.");

        var hasAny = await db.MinecraftServers.AnyAsync(s => s.GuildId == guildId);

        var server = new MinecraftServer
        {
            GuildId = guildId,
            Name = name.ToLowerInvariant(),
            Address = address,
            Port = port,
            ServerType = (int)serverType,
            IsDefault = !hasAny,
            WatchInterval = 5,
            DateAdded = DateTime.UtcNow
        };

        server.Id = await db.InsertWithInt32IdentityAsync(server);
        InvalidateCache(guildId);
        return server;
    }

    /// <summary>
    ///     Removes a Minecraft server from a guild's configuration.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="name">The label of the server to remove.</param>
    /// <returns>True if the server was removed.</returns>
    public async Task<bool> RemoveServerAsync(ulong guildId, string name)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var server = await db.MinecraftServers
            .FirstOrDefaultAsync(s => s.GuildId == guildId && s.Name == name.ToLowerInvariant());

        if (server == null) return false;

        StopWatchTimer(server.Id);

        await db.MinecraftServers.DeleteAsync(s => s.Id == server.Id);

        if (server.IsDefault)
        {
            var next = await db.MinecraftServers
                .Where(s => s.GuildId == guildId)
                .OrderBy(s => s.Id)
                .FirstOrDefaultAsync();

            if (next != null)
            {
                next.IsDefault = true;
                await db.UpdateAsync(next);
            }
        }

        InvalidateCache(guildId);
        return true;
    }

    /// <summary>
    ///     Gets all Minecraft servers for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>The list of servers.</returns>
    public async Task<List<MinecraftServer>> GetServersAsync(ulong guildId)
    {
        var cacheKey = string.Format(ServersCacheKey, guildId);

        if (cache.TryGetValue(cacheKey, out List<MinecraftServer>? cached) && cached != null)
            return cached;

        await using var db = await dbFactory.CreateConnectionAsync();
        var servers = await db.MinecraftServers
            .Where(s => s.GuildId == guildId)
            .OrderBy(s => s.Name)
            .ToListAsync();

        cache.Set(cacheKey, servers, TimeSpan.FromMinutes(10));
        return servers;
    }

    /// <summary>
    ///     Gets the default server for a guild, or a specific server by name.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="name">The server name, or null to get the default.</param>
    /// <returns>The server entry, or null if not found.</returns>
    public async Task<MinecraftServer?> GetServerAsync(ulong guildId, string? name = null)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        if (string.IsNullOrWhiteSpace(name))
            return await db.MinecraftServers.FirstOrDefaultAsync(s => s.GuildId == guildId && s.IsDefault);

        return await db.MinecraftServers
            .FirstOrDefaultAsync(s => s.GuildId == guildId && s.Name == name.ToLowerInvariant());
    }

    /// <summary>
    ///     Sets a server as the default for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="name">The name of the server to set as default.</param>
    /// <returns>True if the server was found and set as default.</returns>
    public async Task<bool> SetDefaultServerAsync(ulong guildId, string name)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        await using var tx = await db.BeginTransactionAsync();

        var server = await db.MinecraftServers
            .FirstOrDefaultAsync(s => s.GuildId == guildId && s.Name == name.ToLowerInvariant());

        if (server == null) return false;

        await db.MinecraftServers
            .Where(s => s.GuildId == guildId)
            .Set(s => s.IsDefault, false)
            .UpdateAsync();

        server.IsDefault = true;
        await db.UpdateAsync(server);
        await tx.CommitAsync();

        InvalidateCache(guildId);
        return true;
    }

    /// <summary>
    ///     Configures a watch channel for a server, posting periodic status updates.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="serverName">The server name.</param>
    /// <param name="channelId">The channel ID to post updates in, or null to disable.</param>
    /// <returns>The updated server, or null if not found.</returns>
    public async Task<MinecraftServer?> SetWatchChannelAsync(ulong guildId, string serverName, ulong? channelId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var server = await db.MinecraftServers
            .FirstOrDefaultAsync(s => s.GuildId == guildId && s.Name == serverName.ToLowerInvariant());

        if (server == null) return null;

        StopWatchTimer(server.Id);

        server.WatchChannelId = channelId;
        server.WatchMessageId = null;
        await db.UpdateAsync(server);

        if (channelId.HasValue)
            StartWatchTimer(server);

        InvalidateCache(guildId);
        return server;
    }

    /// <summary>
    ///     Sets a custom embed template for a watched server's status messages.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="serverName">The server name.</param>
    /// <param name="template">The embed template JSON, or null to use the default.</param>
    /// <returns>The updated server, or null if not found.</returns>
    public async Task<MinecraftServer?> SetCustomEmbedAsync(ulong guildId, string serverName, string? template)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var server = await db.MinecraftServers
            .FirstOrDefaultAsync(s => s.GuildId == guildId && s.Name == serverName.ToLowerInvariant());

        if (server == null) return null;

        server.CustomEmbedTemplate = template;
        await db.UpdateAsync(server);

        InvalidateCache(guildId);
        return server;
    }

    /// <summary>
    ///     Sets the watch mode for a server (Embed, ChannelTopic, or Both).
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="serverName">The server name.</param>
    /// <param name="mode">The watch mode.</param>
    /// <returns>The updated server, or null if not found.</returns>
    public async Task<MinecraftServer?> SetWatchModeAsync(ulong guildId, string serverName, McWatchMode mode)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var server = await db.MinecraftServers
            .FirstOrDefaultAsync(s => s.GuildId == guildId && s.Name == serverName.ToLowerInvariant());

        if (server == null) return null;

        server.WatchMode = (int)mode;
        await db.UpdateAsync(server);

        InvalidateCache(guildId);
        return server;
    }

    /// <summary>
    ///     Gets the cached last status for a server without querying.
    /// </summary>
    /// <param name="serverId">The server database ID.</param>
    /// <returns>The cached status, or null if not available.</returns>
    public async Task<McServerStatus?> GetCachedStatusAsync(int serverId)
    {
        return await fusionCache.GetOrDefaultAsync<McServerStatus>(string.Format(StatusCacheKey, serverId));
    }

    /// <summary>
    ///     Records a status snapshot and updates the cached last status.
    /// </summary>
    /// <param name="server">The server entry.</param>
    /// <param name="status">The status to record.</param>
    public async Task RecordSnapshotAsync(MinecraftServer server, McServerStatus status)
    {
        var cacheKey = string.Format(StatusCacheKey, server.Id);
        await fusionCache.SetAsync(cacheKey, status, TimeSpan.FromHours(1));

        await using var db = await dbFactory.CreateConnectionAsync();

        await db.InsertAsync(new MinecraftServerSnapshot
        {
            ServerId = server.Id,
            IsOnline = status.IsOnline,
            PlayersOnline = status.PlayersOnline,
            PlayersMax = status.PlayersMax,
            Latency = status.Latency,
            Version = status.Version,
            Timestamp = DateTime.UtcNow
        });

        var json = JsonSerializer.Serialize(status);
        await db.MinecraftServers
            .Where(s => s.Id == server.Id)
            .Set(s => s.LastStatusJson, json)
            .UpdateAsync();
    }

    /// <summary>
    ///     Gets historical snapshots for a server within a time range.
    /// </summary>
    /// <param name="serverId">The server database ID.</param>
    /// <param name="hours">How many hours of history to fetch.</param>
    /// <returns>The list of snapshots.</returns>
    public async Task<List<MinecraftServerSnapshot>> GetSnapshotsAsync(int serverId, int hours = 24)
    {
        var cacheKey = string.Format(SnapshotsCacheKey, serverId, hours);
        var cached = await fusionCache.GetOrDefaultAsync<List<MinecraftServerSnapshot>>(cacheKey);
        if (cached != null)
            return cached;

        await using var db = await dbFactory.CreateConnectionAsync();
        var since = DateTime.UtcNow.AddHours(-hours);
        var snapshots = await db.MinecraftServerSnapshots
            .Where(s => s.ServerId == serverId && s.Timestamp >= since)
            .OrderBy(s => s.Timestamp)
            .ToListAsync();

        await fusionCache.SetAsync(cacheKey, snapshots, TimeSpan.FromMinutes(2));
        return snapshots;
    }

    /// <summary>
    ///     Cleans up old snapshots beyond a retention period.
    /// </summary>
    /// <param name="retentionDays">Number of days to retain.</param>
    public async Task CleanupSnapshotsAsync(int retentionDays = 30)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var cutoff = DateTime.UtcNow.AddDays(-retentionDays);
        var deleted = await db.MinecraftServerSnapshots
            .Where(s => s.Timestamp < cutoff)
            .DeleteAsync();

        if (deleted > 0)
            logger.LogInformation("Cleaned up {Count} old Minecraft server snapshots", deleted);
    }

    /// <summary>
    ///     Builds the default status embed for a Minecraft server.
    /// </summary>
    /// <param name="server">The server database entry.</param>
    /// <param name="status">The queried server status.</param>
    /// <param name="guildId">The guild ID for localization.</param>
    /// <returns>The built embed.</returns>
    public Embed BuildStatusEmbed(MinecraftServer server, McServerStatus status, ulong guildId)
    {
        if (status.IsOnline)
        {
            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithTitle($"{server.Name} — {server.Address}:{server.Port}")
                .AddField(strings.McVersion(guildId), status.Version, true)
                .AddField(strings.McPlayers(guildId), $"{status.PlayersOnline}/{status.PlayersMax}", true)
                .AddField(strings.McLatency(guildId), $"{status.Latency}ms", true)
                .WithFooter(strings.McLastChecked(guildId))
                .WithCurrentTimestamp();

            if (!string.IsNullOrWhiteSpace(status.Motd))
                eb.WithDescription(CleanMotd(status.Motd));

            if (!string.IsNullOrWhiteSpace(status.Map))
                eb.AddField(strings.McMap(guildId), status.Map, true);

            if (!string.IsNullOrWhiteSpace(status.GameMode))
                eb.AddField(strings.McGameMode(guildId), status.GameMode, true);

            if (!string.IsNullOrWhiteSpace(status.Software))
                eb.AddField(strings.McSoftware(guildId), status.Software, true);

            if (status.PlayerList.Count > 0)
                eb.AddField(strings.McPlayerList(guildId), string.Join(", ", status.PlayerList.Take(20)));

            if (status.Plugins.Count > 0)
                eb.AddField(strings.McPlugins(guildId), string.Join(", ", status.Plugins.Take(15)));

            if (status.Favicon != null && status.Favicon.StartsWith("data:image"))
                eb.WithThumbnailUrl($"https://mc-api.net/v3/server/favicon/{server.Address}:{server.Port}");

            return eb.Build();
        }

        return new EmbedBuilder()
            .WithErrorColor()
            .WithTitle($"{server.Name} — {server.Address}:{server.Port}")
            .WithDescription(strings.McServerOffline(guildId))
            .WithFooter(strings.McLastChecked(guildId))
            .WithCurrentTimestamp()
            .Build();
    }

    /// <summary>
    ///     Builds a status embed using a custom template with placeholder replacement.
    /// </summary>
    /// <param name="server">The server database entry.</param>
    /// <param name="status">The queried server status.</param>
    /// <param name="guild">The guild for context.</param>
    /// <returns>The parsed embed components, or null if parsing fails.</returns>
    public (Embed[]? Embeds, string? PlainText, ComponentBuilder? Components)? BuildCustomStatusEmbed(
        MinecraftServer server, McServerStatus status, IGuild guild)
    {
        if (string.IsNullOrWhiteSpace(server.CustomEmbedTemplate))
            return null;

        var replacer = BuildMcReplacer(server, status, guild).Build();
        var parsed = replacer.Replace(server.CustomEmbedTemplate);

        if (SmartEmbed.TryParse(parsed, guild.Id, out var embeds, out var plainText, out var components))
            return (embeds, plainText, components);

        return null;
    }

    /// <summary>
    ///     Cleans Minecraft formatting codes from a MOTD string.
    /// </summary>
    /// <summary>
    ///     Sends an RCON command to a server.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="serverName">The server name.</param>
    /// <param name="command">The command to execute.</param>
    /// <returns>The server's response, or an error message.</returns>
    public async Task<(bool Success, string Response, string? RawResponse)> SendRconCommandAsync(ulong guildId,
        string serverName, string command)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var server = await db.MinecraftServers
            .FirstOrDefaultAsync(s => s.GuildId == guildId && s.Name == serverName.ToLowerInvariant());

        if (server == null)
            return (false, "Server not found.", null);

        if (!server.RconEnabled || string.IsNullOrWhiteSpace(server.RconPassword))
            return (false, "RCON is not configured for this server.", null);

        var rconPort = server.RconPort > 0 ? server.RconPort : 25575;

        try
        {
            using var rcon = await RconClient.ConnectAsync(server.Address, rconPort, server.RconPassword);
            var raw = await rcon.SendCommandAsync(command);
            var cleaned = CleanFormatting(raw);
            var display = string.IsNullOrWhiteSpace(cleaned) ? "Command executed (no output)." : cleaned;
            return (true, display, raw);
        }
        catch (InvalidOperationException ex)
        {
            return (false, ex.Message, null);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "RCON command failed for {ServerName}", serverName);
            return (false, "Failed to connect to RCON. Check the server is running and RCON is enabled.", null);
        }
    }

    /// <summary>
    ///     Configures RCON settings for a server. Only available via API/dashboard.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="serverName">The server name.</param>
    /// <param name="enabled">Whether RCON is enabled.</param>
    /// <param name="port">The RCON port.</param>
    /// <param name="password">The RCON password.</param>
    /// <returns>The updated server, or null if not found.</returns>
    public async Task<MinecraftServer?> SetRconConfigAsync(ulong guildId, string serverName, bool enabled, int port,
        string? password)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var server = await db.MinecraftServers
            .FirstOrDefaultAsync(s => s.GuildId == guildId && s.Name == serverName.ToLowerInvariant());

        if (server == null) return null;

        server.RconEnabled = enabled;
        server.RconPort = port;
        if (password != null)
            server.RconPassword = password;

        await db.UpdateAsync(server);
        InvalidateCache(guildId);
        return server;
    }

    /// <summary>
    ///     Generates a new plugin API key for a server. Replaces any existing key.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="serverName">The server name.</param>
    /// <returns>The new API key, or null if server not found.</returns>
    public async Task<string?> GeneratePluginApiKeyAsync(ulong guildId, string serverName)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var server = await db.MinecraftServers
            .FirstOrDefaultAsync(s => s.GuildId == guildId && s.Name == serverName.ToLowerInvariant());

        if (server == null) return null;

        var key = $"mcp_{Guid.NewGuid():N}";
        server.PluginApiKey = key;
        await db.UpdateAsync(server);
        InvalidateCache(guildId);
        return key;
    }

    /// <summary>
    ///     Revokes the plugin API key for a server.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="serverName">The server name.</param>
    /// <returns>True if successful.</returns>
    public async Task<bool> RevokePluginApiKeyAsync(ulong guildId, string serverName)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var server = await db.MinecraftServers
            .FirstOrDefaultAsync(s => s.GuildId == guildId && s.Name == serverName.ToLowerInvariant());

        if (server == null) return false;

        server.PluginApiKey = null;
        await db.UpdateAsync(server);
        InvalidateCache(guildId);
        return true;
    }

    /// <summary>
    ///     Validates a plugin API key and returns the associated server if valid.
    /// </summary>
    /// <param name="apiKey">The API key to validate.</param>
    /// <returns>The server entry if the key is valid, null otherwise.</returns>
    public async Task<MinecraftServer?> ValidatePluginApiKeyAsync(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return null;

        await using var db = await dbFactory.CreateConnectionAsync();
        return await db.MinecraftServers
            .FirstOrDefaultAsync(s => s.PluginApiKey == apiKey);
    }

    /// <param name="motd">The raw MOTD string.</param>
    /// <returns>The cleaned string.</returns>
    /// <summary>
    ///     Sets the custom online alert message for a server.
    /// </summary>
    public async Task<MinecraftServer?> SetCustomOnlineMessageAsync(ulong guildId, string serverName, string? message)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var server = await db.MinecraftServers
            .FirstOrDefaultAsync(s => s.GuildId == guildId && s.Name == serverName.ToLowerInvariant());
        if (server == null) return null;

        server.CustomOnlineMessage = message;
        await db.UpdateAsync(server);
        InvalidateCache(guildId);
        return server;
    }

    /// <summary>
    ///     Sets the custom offline alert message for a server.
    /// </summary>
    public async Task<MinecraftServer?> SetCustomOfflineMessageAsync(ulong guildId, string serverName, string? message)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var server = await db.MinecraftServers
            .FirstOrDefaultAsync(s => s.GuildId == guildId && s.Name == serverName.ToLowerInvariant());
        if (server == null) return null;

        server.CustomOfflineMessage = message;
        await db.UpdateAsync(server);
        InvalidateCache(guildId);
        return server;
    }

    /// <summary>
    ///     Sends a whitelist RCON command and returns the response.
    /// </summary>
    public async Task<(bool Success, string Response)> WhitelistCommandAsync(ulong guildId, string serverName,
        string action, string? playerName = null)
    {
        var command = action.ToLowerInvariant() switch
        {
            "add" when !string.IsNullOrWhiteSpace(playerName) => $"whitelist add {playerName}",
            "remove" when !string.IsNullOrWhiteSpace(playerName) => $"whitelist remove {playerName}",
            "list" => "whitelist list",
            "on" => "whitelist on",
            "off" => "whitelist off",
            "reload" => "whitelist reload",
            _ => null
        };

        if (command == null)
            return (false, "Invalid whitelist action. Use: add, remove, list, on, off, reload.");

        var (success, response, _) = await SendRconCommandAsync(guildId, serverName, command);
        return (success, response);
    }

    private async Task SendAlertMessageAsync(ITextChannel channel, MinecraftServer server, McServerStatus status,
        IGuild guild, bool isOnline)
    {
        var customMessage = isOnline ? server.CustomOnlineMessage : server.CustomOfflineMessage;

        if (!string.IsNullOrWhiteSpace(customMessage))
        {
            var replacer = BuildMcReplacer(server, status, guild).Build();
            var parsed = replacer.Replace(customMessage);

            if (SmartEmbed.TryParse(parsed, guild.Id, out var embeds, out var plainText, out var components))
            {
                await channel.SendMessageAsync(
                    plainText,
                    embeds: embeds,
                    components: components?.Build());
                return;
            }

            await channel.SendMessageAsync(parsed);
            return;
        }

        var eb = new EmbedBuilder()
            .WithDescription(isOnline
                ? strings.McServerCameOnline(guild.Id, server.Name)
                : strings.McServerWentOffline(guild.Id, server.Name));

        if (isOnline)
            eb.WithOkColor();
        else
            eb.WithErrorColor();

        await channel.SendMessageAsync(embed: eb.Build());
    }

    private ReplacementBuilder BuildMcReplacer(MinecraftServer server, McServerStatus status, IGuild guild)
    {
        return new ReplacementBuilder()
            .WithOverride("%mc.server.name%", () => server.Name)
            .WithOverride("%mc.server.address%", () => server.Address)
            .WithOverride("%mc.server.port%", () => server.Port.ToString())
            .WithOverride("%mc.online%", () => status.IsOnline ? "Online" : "Offline")
            .WithOverride("%mc.players.online%", () => status.PlayersOnline.ToString())
            .WithOverride("%mc.players.max%", () => status.PlayersMax.ToString())
            .WithOverride("%mc.motd%", () => CleanMotd(status.Motd))
            .WithOverride("%mc.version%", () => status.Version)
            .WithOverride("%mc.latency%", () => $"{status.Latency}ms")
            .WithOverride("%mc.player.list%", () =>
                status.PlayerList.Count > 0 ? string.Join(", ", status.PlayerList.Take(20)) : "None")
            .WithOverride("%mc.favicon%", () =>
                $"https://mc-api.net/v3/server/favicon/{server.Address}:{server.Port}")
            .WithOverride("%mc.map%", () => status.Map ?? "Unknown")
            .WithOverride("%mc.gamemode%", () => status.GameMode ?? "Unknown")
            .WithOverride("%mc.software%", () => status.Software ?? "Unknown")
            .WithOverride("%mc.plugins%", () =>
                status.Plugins.Count > 0 ? string.Join(", ", status.Plugins.Take(15)) : "None")
            .WithOverride("%mc.query%", () => status.IsQueryResponse ? "Yes" : "No")
            .WithOverride("%mc.geyser%", () => status.IsGeyser ? "Yes" : "No");
    }

    /// <summary>
    ///     Cleans Minecraft formatting codes from a MOTD string.
    /// </summary>
    public static string CleanMotd(string motd)
    {
        return CleanFormatting(motd);
    }

    /// <summary>
    ///     Strips all Minecraft formatting codes including §x hex color sequences.
    /// </summary>
    /// <param name="text">The raw text.</param>
    /// <returns>The cleaned string.</returns>
    public static string CleanFormatting(string text)
    {
        var cleaned = Regex.Replace(text, @"§x(§[0-9a-fA-F]){6}", "");
        return Regex.Replace(cleaned, @"§[0-9a-fk-orA-FK-OR]", "");
    }

    private void StartWatchTimer(MinecraftServer server)
    {
        var interval = TimeSpan.FromMinutes(Math.Max(1, server.WatchInterval));
        var timer = new Timer(_ => _ = UpdateWatchMessageAsync(server.Id), null, TimeSpan.FromSeconds(10), interval);
        watchTimers[server.Id] = timer;
    }

    private void StopWatchTimer(int serverId)
    {
        if (watchTimers.TryRemove(serverId, out var timer))
            timer.Dispose();
    }

    private async Task UpdateWatchMessageAsync(int serverId)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();
            var server = await db.MinecraftServers.FirstOrDefaultAsync(s => s.Id == serverId);
            if (server?.WatchChannelId == null) return;

            var guild = client.GetGuild(server.GuildId);
            if (guild == null) return;

            var channel = guild.GetTextChannel(server.WatchChannelId.Value);
            if (channel == null) return;

            var status = await QueryServerAsync(server);
            if (status == null) return;

            await RecordSnapshotAsync(server, status);

            var wasOnline = server.LastOnline;
            server.LastOnline = status.IsOnline;
            await db.UpdateAsync(server);

            var watchMode = (McWatchMode)server.WatchMode;

            if (watchMode is McWatchMode.Embed or McWatchMode.Both)
                await UpdateWatchEmbedAsync(server, status, guild, channel, db);

            if (watchMode is McWatchMode.ChannelTopic or McWatchMode.Both)
                await UpdateWatchTopicAsync(server, status, guild, channel);

            if (wasOnline.HasValue && wasOnline.Value && !status.IsOnline)
                await SendAlertMessageAsync(channel, server, status, guild, false);
            else if (wasOnline.HasValue && !wasOnline.Value && status.IsOnline)
                await SendAlertMessageAsync(channel, server, status, guild, true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating watch message for server {ServerId}", serverId);
        }
    }

    private async Task UpdateWatchEmbedAsync(MinecraftServer server, McServerStatus status, IGuild guild,
        ITextChannel channel, MewdekoDb db)
    {
        Embed embed;
        string? plainText = null;
        ComponentBuilder? components = null;

        var customResult = BuildCustomStatusEmbed(server, status, guild);
        if (customResult?.Embeds != null && customResult.Value.Embeds.Length > 0)
        {
            embed = customResult.Value.Embeds[0];
            plainText = customResult.Value.PlainText;
            components = customResult.Value.Components;
        }
        else
        {
            embed = BuildStatusEmbed(server, status, guild.Id);
        }

        if (server.WatchMessageId.HasValue)
        {
            try
            {
                if (await channel.GetMessageAsync(server.WatchMessageId.Value) is IUserMessage msg)
                {
                    await msg.ModifyAsync(m =>
                    {
                        m.Embed = embed;
                        m.Content = plainText ?? Optional<string>.Unspecified;
                        m.Components = components?.Build() ?? new ComponentBuilder().Build();
                    });
                    return;
                }
            }
            catch
            {
                // Message was deleted or inaccessible, send a new one
            }
        }

        var newMsg = await channel.SendMessageAsync(
            plainText,
            embed: embed,
            components: components?.Build());

        server.WatchMessageId = newMsg.Id;
        await db.MinecraftServers
            .Where(s => s.Id == server.Id)
            .Set(s => s.WatchMessageId, newMsg.Id)
            .UpdateAsync();
    }

    private async Task UpdateWatchTopicAsync(MinecraftServer server, McServerStatus status, IGuild guild,
        ITextChannel channel)
    {
        try
        {
            string topic;
            if (status.IsOnline)
            {
                var parts = new List<string>
                {
                    $"{server.Name}: Online", $"{status.PlayersOnline}/{status.PlayersMax} players", status.Version
                };
                if (!string.IsNullOrWhiteSpace(status.Map))
                    parts.Add($"Map: {status.Map}");
                if (status.Latency > 0)
                    parts.Add($"{status.Latency}ms");
                topic = string.Join(" | ", parts);
            }
            else
            {
                topic = $"{server.Name}: Offline";
            }

            if (channel.Topic != topic)
                await channel.ModifyAsync(c => c.Topic = topic);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to update channel topic for server {ServerName}", server.Name);
        }
    }

    private McServerStatus ParseFullStatResponse(byte[] data, int latency)
    {
        logger.LogInformation("Query full stat response ({Length} bytes): {Hex}",
            data.Length, Convert.ToHexString(data));
        logger.LogInformation("Query full stat response (ASCII): {Ascii}",
            Encoding.ASCII.GetString(data.Select(b => b is >= 32 and <= 126 ? b : (byte)'.').ToArray()));
        var status = new McServerStatus
        {
            IsOnline = true, Latency = latency, IsQueryResponse = true
        };
        var kvPairs = new Dictionary<string, string>();

        var offset = 11;
        while (offset < data.Length)
        {
            if (data[offset] == 0x00 && offset + 1 < data.Length && data[offset + 1] == 0x00)
                break;

            var key = ReadNullTerminatedString(data, ref offset);
            var value = ReadNullTerminatedString(data, ref offset);
            if (!string.IsNullOrEmpty(key))
                kvPairs[key] = value;
        }

        offset += 2;

        if (kvPairs.TryGetValue("hostname", out var motd))
            status.Motd = motd;
        if (kvPairs.TryGetValue("numplayers", out var numPlayers) && int.TryParse(numPlayers, out var online))
            status.PlayersOnline = online;
        if (kvPairs.TryGetValue("maxplayers", out var maxPlayers) && int.TryParse(maxPlayers, out var max))
            status.PlayersMax = max;
        if (kvPairs.TryGetValue("version", out var version))
            status.Version = version;
        if (kvPairs.TryGetValue("map", out var map))
            status.Map = map;
        if (kvPairs.TryGetValue("gametype", out var gameType))
            status.GameMode = gameType;
        if (kvPairs.TryGetValue("plugins", out var plugins) && !string.IsNullOrWhiteSpace(plugins))
        {
            var colonIndex = plugins.IndexOf(':');
            if (colonIndex >= 0)
            {
                status.Software = plugins[..colonIndex].Trim();
                status.Plugins = plugins[(colonIndex + 1)..]
                    .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToList();
            }
            else
            {
                status.Software = plugins.Trim();
            }
        }

        if (kvPairs.TryGetValue("software", out var software) && status.Software == null)
            status.Software = software;

        byte[] playerMarker = [0x01, 0x70, 0x6C, 0x61, 0x79, 0x65, 0x72, 0x5F, 0x00, 0x00];
        var markerIndex = -1;
        for (var i = 0; i <= data.Length - playerMarker.Length; i++)
        {
            var found = true;
            for (var j = 0; j < playerMarker.Length; j++)
            {
                if (data[i + j] != playerMarker[j])
                {
                    found = false;
                    break;
                }
            }

            if (found)
            {
                markerIndex = i + playerMarker.Length;
                break;
            }
        }

        if (markerIndex >= 0)
        {
            offset = markerIndex;
            while (offset < data.Length)
            {
                var playerName = ReadNullTerminatedString(data, ref offset);
                if (string.IsNullOrEmpty(playerName))
                    break;
                status.PlayerList.Add(playerName);
            }
        }

        return status;
    }

    private static string ReadNullTerminatedString(byte[] data, ref int offset)
    {
        var start = offset;
        while (offset < data.Length && data[offset] != 0x00)
            offset++;
        var result = Encoding.UTF8.GetString(data, start, offset - start);
        if (offset < data.Length) offset++;
        return result;
    }

    private void InvalidateCache(ulong guildId)
    {
        cache.Remove(string.Format(ServersCacheKey, guildId));
    }

    private static byte[] BuildHandshakePacket(string address, int port)
    {
        using var ms = new MemoryStream();
        WriteVarInt(ms, 0x00);
        WriteVarInt(ms, 767);
        WriteVarInt(ms, address.Length);
        ms.Write(Encoding.UTF8.GetBytes(address));
        ms.WriteByte((byte)(port >> 8));
        ms.WriteByte((byte)(port & 0xFF));
        WriteVarInt(ms, 1);

        var data = ms.ToArray();
        using var packet = new MemoryStream();
        WriteVarInt(packet, data.Length);
        packet.Write(data);
        return packet.ToArray();
    }

    private static byte[] BuildBedrockPingPacket()
    {
        using var ms = new MemoryStream();
        ms.WriteByte(0x01);
        var timestamp = BitConverter.GetBytes(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        if (BitConverter.IsLittleEndian) Array.Reverse(timestamp);
        ms.Write(timestamp);
        byte[] magic = [0x00, 0xFF, 0xFF, 0x00, 0xFE, 0xFE, 0xFE, 0xFE, 0xFD, 0xFD, 0xFD, 0xFD, 0x12, 0x34, 0x56, 0x78];
        ms.Write(magic);
        ms.Write(new byte[8]);
        return ms.ToArray();
    }

    private static void WriteVarInt(Stream stream, int value)
    {
        var unsigned = (uint)value;
        while (unsigned > 127)
        {
            stream.WriteByte((byte)((unsigned & 0x7F) | 0x80));
            unsigned >>= 7;
        }

        stream.WriteByte((byte)unsigned);
    }

    private static async Task<int> ReadVarIntAsync(Stream stream)
    {
        var result = 0;
        var shift = 0;
        byte current;
        var buffer = new byte[1];
        do
        {
            var read = await stream.ReadAsync(buffer);
            if (read == 0) return -1;
            current = buffer[0];
            result |= (current & 0x7F) << shift;
            shift += 7;
            if (shift > 35) throw new InvalidDataException("VarInt too large");
        } while ((current & 0x80) != 0);

        return result;
    }

    private static int GetVarIntSize(ReadOnlySpan<byte> data)
    {
        for (var i = 0; i < Math.Min(5, data.Length); i++)
        {
            if ((data[i] & 0x80) == 0)
                return i + 1;
        }

        return 5;
    }
}