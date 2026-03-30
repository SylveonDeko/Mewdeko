using Mewdeko.Modules.Minecraft.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     WebSocket endpoint for the Mewdeko companion Minecraft plugin.
///     Authenticates using per-server plugin API keys.
/// </summary>
[ApiController]
[Route("botapi/mc-bridge")]
public class MinecraftBridgeController(
    MinecraftService minecraftService,
    MinecraftBridgeService bridgeService,
    ILogger<MinecraftBridgeController> logger) : Controller
{
    /// <summary>
    ///     WebSocket endpoint for companion plugin connections.
    ///     Authenticate by passing the plugin API key as the "key" query parameter.
    /// </summary>
    /// <param name="key">The per-server plugin API key.</param>
    [HttpGet("ws")]
    [AllowAnonymous]
    public async Task Connect([FromQuery] string key)
    {
        if (!HttpContext.WebSockets.IsWebSocketRequest)
        {
            HttpContext.Response.StatusCode = 400;
            await HttpContext.Response.WriteAsync("WebSocket connection required");
            return;
        }

        if (string.IsNullOrWhiteSpace(key))
        {
            HttpContext.Response.StatusCode = 401;
            await HttpContext.Response.WriteAsync("Plugin API key is required");
            return;
        }

        var server = await minecraftService.ValidatePluginApiKeyAsync(key);
        if (server == null)
        {
            HttpContext.Response.StatusCode = 401;
            await HttpContext.Response.WriteAsync("Invalid plugin API key");
            return;
        }

        logger.LogInformation("Plugin WebSocket connecting for server {ServerName} (guild {GuildId})",
            server.Name, server.GuildId);

        using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
        await bridgeService.HandleConnectionAsync(server, webSocket, HttpContext);
    }
}