using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Controllers.Common.Minecraft;
using Mewdeko.Modules.Minecraft.Common;
using Mewdeko.Modules.Minecraft.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     API controller for managing Minecraft server configurations and querying server status.
/// </summary>
[ApiController]
[Route("botapi/[controller]/{guildId}")]
[Authorize("ApiKeyPolicy")]
public class MinecraftController(
    MinecraftService minecraftService,
    MinecraftBridgeService bridgeService,
    IDataConnectionFactory dbFactory,
    DiscordShardedClient client,
    ILogger<MinecraftController> logger) : Controller
{
    /// <summary>
    ///     Gets all registered Minecraft servers for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>A list of registered servers.</returns>
    [HttpGet("servers")]
    public async Task<IActionResult> GetServers(ulong guildId)
    {
        var servers = await minecraftService.GetServersAsync(guildId);
        return Ok(servers.Select(MapToResponse));
    }

    /// <summary>
    ///     Gets a specific registered Minecraft server by name.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="name">The server name.</param>
    /// <returns>The server configuration.</returns>
    [HttpGet("servers/{name}")]
    public async Task<IActionResult> GetServer(ulong guildId, string name)
    {
        var server = await minecraftService.GetServerAsync(guildId, name);
        if (server == null)
            return NotFound("Server not found");

        return Ok(MapToResponse(server));
    }

    /// <summary>
    ///     Adds a new Minecraft server to a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="request">The server details.</param>
    /// <returns>The created server.</returns>
    [HttpPost("servers")]
    public async Task<IActionResult> AddServer(ulong guildId, [FromBody] AddMinecraftServerRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest("Server name is required");

        if (string.IsNullOrWhiteSpace(request.Address))
            return BadRequest("Server address is required");

        try
        {
            var server = await minecraftService.AddServerAsync(
                guildId, request.Name, request.Address, request.Port, (McServerType)request.ServerType);

            if (request.QueryPort > 0)
            {
                await using var db = await dbFactory.CreateConnectionAsync();
                await db.MinecraftServers
                    .Where(s => s.Id == server.Id)
                    .Set(s => s.QueryPort, request.QueryPort)
                    .UpdateAsync();
            }

            var created = await minecraftService.GetServerAsync(guildId, request.Name);
            return CreatedAtAction(nameof(GetServer), new
            {
                guildId, name = request.Name
            }, MapToResponse(created!));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }

    /// <summary>
    ///     Updates an existing Minecraft server's configuration.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="name">The server name.</param>
    /// <param name="request">The fields to update.</param>
    /// <returns>The updated server.</returns>
    [HttpPut("servers/{name}")]
    public async Task<IActionResult> UpdateServer(ulong guildId, string name,
        [FromBody] UpdateMinecraftServerRequest request)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var server = await db.MinecraftServers
            .FirstOrDefaultAsync(s => s.GuildId == guildId && s.Name == name.ToLowerInvariant());

        if (server == null)
            return NotFound("Server not found");

        if (request.Address != null)
            server.Address = request.Address;
        if (request.Port.HasValue)
            server.Port = request.Port.Value;
        if (request.ServerType.HasValue)
            server.ServerType = request.ServerType.Value;
        if (request.QueryPort.HasValue)
            server.QueryPort = request.QueryPort.Value;

        if (request.IsDefault == true)
        {
            await db.MinecraftServers
                .Where(s => s.GuildId == guildId)
                .Set(s => s.IsDefault, false)
                .UpdateAsync();
            server.IsDefault = true;
        }

        await db.UpdateAsync(server);
        return Ok(MapToResponse(server));
    }

    /// <summary>
    ///     Removes a registered Minecraft server from a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="name">The server name to remove.</param>
    /// <returns>Success or not found.</returns>
    [HttpDelete("servers/{name}")]
    public async Task<IActionResult> RemoveServer(ulong guildId, string name)
    {
        var removed = await minecraftService.RemoveServerAsync(guildId, name);
        if (!removed)
            return NotFound("Server not found");

        return Ok(new
        {
            success = true
        });
    }

    /// <summary>
    ///     Configures the watch channel and interval for a server.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="name">The server name.</param>
    /// <param name="request">The watch configuration.</param>
    /// <returns>The updated server.</returns>
    [HttpPut("servers/{name}/watch")]
    public async Task<IActionResult> SetWatch(ulong guildId, string name, [FromBody] SetWatchRequest request)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound("Guild not found");

        if (request.ChannelId.HasValue)
        {
            var channel = guild.GetTextChannel(request.ChannelId.Value);
            if (channel == null)
                return BadRequest("Channel not found in this guild");
        }

        var server = await minecraftService.SetWatchChannelAsync(guildId, name, request.ChannelId);
        if (server == null)
            return NotFound("Server not found");

        if (request.Interval.HasValue && request.Interval.Value >= 1 || request.WatchMode.HasValue)
        {
            await using var db = await dbFactory.CreateConnectionAsync();

            if (request.Interval.HasValue && request.Interval.Value >= 1)
            {
                await db.MinecraftServers
                    .Where(s => s.Id == server.Id)
                    .Set(s => s.WatchInterval, request.Interval.Value)
                    .UpdateAsync();
                server.WatchInterval = request.Interval.Value;
            }

            if (request.WatchMode.HasValue)
            {
                await db.MinecraftServers
                    .Where(s => s.Id == server.Id)
                    .Set(s => s.WatchMode, request.WatchMode.Value)
                    .UpdateAsync();
                server.WatchMode = request.WatchMode.Value;
            }
        }

        return Ok(MapToResponse(server));
    }

    /// <summary>
    ///     Sets or clears a custom embed template for a server's watch messages.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="name">The server name.</param>
    /// <param name="request">The embed template.</param>
    /// <returns>The updated server.</returns>
    [HttpPut("servers/{name}/embed")]
    public async Task<IActionResult> SetCustomEmbed(ulong guildId, string name,
        [FromBody] SetCustomEmbedRequest request)
    {
        var server = await minecraftService.SetCustomEmbedAsync(guildId, name, request.Template);
        if (server == null)
            return NotFound("Server not found");

        return Ok(MapToResponse(server));
    }

    /// <summary>
    ///     Queries the live status of a registered server.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="name">The server name.</param>
    /// <returns>The live server status.</returns>
    [HttpGet("servers/{name}/status")]
    public async Task<IActionResult> GetServerStatus(ulong guildId, string name)
    {
        var server = await minecraftService.GetServerAsync(guildId, name);
        if (server == null)
            return NotFound("Server not found");

        var status = await minecraftService.QueryServerAsync(server);
        if (status == null)
            return Ok(new MinecraftStatusResponse
            {
                IsOnline = false
            });

        await minecraftService.RecordSnapshotAsync(server, status);
        return Ok(MapStatusToResponse(status));
    }

    /// <summary>
    ///     Queries the live status of an arbitrary Minecraft server address.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="address">The server address (e.g. play.example.com or play.example.com:25565).</param>
    /// <returns>The live server status.</returns>
    [HttpGet("query/{address}")]
    public async Task<IActionResult> QueryDirectStatus(ulong guildId, string address)
    {
        var parts = address.Split(':');
        var host = parts[0];
        var port = parts.Length > 1 && int.TryParse(parts[1], out var p) ? p : 25565;

        if (!minecraftService.IsAddressSafe(host))
            return BadRequest("That address is not allowed. Private, reserved, and local addresses are blocked.");

        var status = await minecraftService.QueryJavaServerAsync(host, port);
        if (status == null)
            return Ok(new MinecraftStatusResponse
            {
                IsOnline = false
            });

        return Ok(MapStatusToResponse(status));
    }

    /// <summary>
    ///     Gets the cached last-known status for a server without live querying.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="name">The server name.</param>
    /// <returns>The cached status or null.</returns>
    [HttpGet("servers/{name}/cached-status")]
    public async Task<IActionResult> GetCachedStatus(ulong guildId, string name)
    {
        var server = await minecraftService.GetServerAsync(guildId, name);
        if (server == null)
            return NotFound("Server not found");

        var status = await minecraftService.GetCachedStatusAsync(server.Id);
        if (status == null)
            return Ok(new MinecraftStatusResponse
            {
                IsOnline = false
            });

        return Ok(MapStatusToResponse(status));
    }

    /// <summary>
    ///     Gets historical snapshots for a server.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="name">The server name.</param>
    /// <param name="hours">How many hours of history (default 24, max 720).</param>
    /// <returns>The list of snapshots.</returns>
    [HttpGet("servers/{name}/history")]
    public async Task<IActionResult> GetServerHistory(ulong guildId, string name, [FromQuery] int hours = 24)
    {
        var server = await minecraftService.GetServerAsync(guildId, name);
        if (server == null)
            return NotFound("Server not found");

        hours = Math.Clamp(hours, 1, 720);
        var snapshots = await minecraftService.GetSnapshotsAsync(server.Id, hours);

        return Ok(snapshots.Select(s => new
        {
            s.IsOnline,
            s.PlayersOnline,
            s.PlayersMax,
            s.Latency,
            s.Version,
            timestamp = s.Timestamp.ToString("o")
        }));
    }

    /// <summary>
    ///     Configures RCON settings for a server (enable, port, password).
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <summary>
    ///     Sets the custom online alert message for a server.
    /// </summary>
    [HttpPut("servers/{name}/online-message")]
    public async Task<IActionResult> SetOnlineMessage(ulong guildId, string name,
        [FromBody] SetCustomEmbedRequest request)
    {
        var server = await minecraftService.SetCustomOnlineMessageAsync(guildId, name, request.Template);
        if (server == null)
            return NotFound("Server not found");
        return Ok(MapToResponse(server));
    }

    /// <summary>
    ///     Sets the custom offline alert message for a server.
    /// </summary>
    [HttpPut("servers/{name}/offline-message")]
    public async Task<IActionResult> SetOfflineMessage(ulong guildId, string name,
        [FromBody] SetCustomEmbedRequest request)
    {
        var server = await minecraftService.SetCustomOfflineMessageAsync(guildId, name, request.Template);
        if (server == null)
            return NotFound("Server not found");
        return Ok(MapToResponse(server));
    }

    /// <param name="name">The server name.</param>
    /// <param name="request">The RCON configuration.</param>
    /// <returns>The updated server.</returns>
    [HttpPut("servers/{name}/rcon")]
    public async Task<IActionResult> SetRconConfig(ulong guildId, string name,
        [FromBody] SetRconConfigRequest request)
    {
        var server =
            await minecraftService.SetRconConfigAsync(guildId, name, request.Enabled, request.Port, request.Password);
        if (server == null)
            return NotFound("Server not found");

        return Ok(MapToResponse(server));
    }

    /// <summary>
    ///     Generates a new plugin API key for a server. Used for the companion MC plugin.
    /// </summary>
    [HttpPost("servers/{name}/plugin-key")]
    public async Task<IActionResult> GeneratePluginKey(ulong guildId, string name)
    {
        var key = await minecraftService.GeneratePluginApiKeyAsync(guildId, name);
        if (key == null)
            return NotFound("Server not found");

        return Ok(new
        {
            key
        });
    }

    /// <summary>
    ///     Revokes the plugin API key for a server.
    /// </summary>
    [HttpDelete("servers/{name}/plugin-key")]
    public async Task<IActionResult> RevokePluginKey(ulong guildId, string name)
    {
        var success = await minecraftService.RevokePluginApiKeyAsync(guildId, name);
        if (!success)
            return NotFound("Server not found");

        return Ok(new
        {
            success = true
        });
    }

    /// <summary>
    ///     Sends an RCON command to a server and returns the response.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="name">The server name.</param>
    /// <param name="request">The command to execute.</param>
    /// <returns>The command response.</returns>
    [HttpPost("servers/{name}/rcon")]
    public async Task<IActionResult> SendRconCommand(ulong guildId, string name,
        [FromBody] RconCommandRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Command))
            return BadRequest("Command is required");

        var (success, response, rawResponse) =
            await minecraftService.SendRconCommandAsync(guildId, name, request.Command);

        return Ok(new
        {
            success, response, rawResponse
        });
    }

    private static MinecraftServerResponse MapToResponse(MinecraftServer server)
    {
        return new MinecraftServerResponse
        {
            Id = server.Id,
            Name = server.Name,
            Address = server.Address,
            Port = server.Port,
            ServerType = server.ServerType,
            QueryPort = server.QueryPort,
            IsDefault = server.IsDefault,
            WatchChannelId = server.WatchChannelId,
            WatchMessageId = server.WatchMessageId,
            WatchInterval = server.WatchInterval,
            WatchMode = server.WatchMode,
            CustomEmbedTemplate = server.CustomEmbedTemplate,
            CustomOnlineMessage = server.CustomOnlineMessage,
            CustomOfflineMessage = server.CustomOfflineMessage,
            LastOnline = server.LastOnline,
            RconEnabled = server.RconEnabled,
            RconPort = server.RconPort,
            HasRconPassword = !string.IsNullOrWhiteSpace(server.RconPassword),
            HasPluginKey = !string.IsNullOrWhiteSpace(server.PluginApiKey),
            DateAdded = server.DateAdded
        };
    }

    private static MinecraftStatusResponse MapStatusToResponse(McServerStatus status)
    {
        return new MinecraftStatusResponse
        {
            IsOnline = status.IsOnline,
            Motd = MinecraftService.CleanMotd(status.Motd),
            PlayersOnline = status.PlayersOnline,
            PlayersMax = status.PlayersMax,
            PlayerList = status.PlayerList,
            PlayerUuids = status.PlayerUuids,
            Version = status.Version,
            Latency = status.Latency,
            Map = status.Map,
            GameMode = status.GameMode,
            Software = status.Software,
            Plugins = status.Plugins,
            IsQueryResponse = status.IsQueryResponse
        };
    }
}