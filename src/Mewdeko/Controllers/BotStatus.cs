using System.Text.Json;
using System.Text.Json.Serialization;
using Discord.Commands;
using Mewdeko.Controllers.Common.Bot;
using Mewdeko.Services.Impl;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     Endpoint for getting status such as guild count, bot version, etc
/// </summary>
[ApiController]
[Route("botapi/[controller]")]
[Authorize("ApiKeyPolicy")]
public class BotStatus(DiscordShardedClient client, StatsService statsService, CommandService commandService)
    : Controller
{
    private static readonly JsonSerializerOptions Options = new()
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles
    };

    /// <summary>
    ///     Actual definition for getting bot status
    /// </summary>
    /// <returns>A BotStatus model</returns>
    [HttpGet]
    public async Task<IActionResult> GetStatus()
    {
        var creds = new BotCredentials();
        var clients = client.Shards;
        var rest = client.Rest;
        var curUser = await rest.GetUserAsync(client.CurrentUser.Id);
        var toReturn = new BotStatusModel
        {
            BotName = client.CurrentUser.GlobalName ?? client.CurrentUser.Username,
            BotAvatar = client.CurrentUser.GetAvatarUrl(size: 2048),
            BotBanner = curUser.GetBannerUrl(size: 4096),
            BotLatency = client.Latency,
            BotVersion = StatsService.BotVersion,
            CommandsCount = commandService.Commands.Distinct(x => x.Name).Count(),
            ModulesCount = commandService.Modules.Count(x => !x.IsSubmodule),
            DNetVersion = statsService.Library,
            BotStatus = client.Status.ToString(),
            UserCount = clients.Select(x => x.Guilds.Sum(g => g.Users.Count)).Sum(),
            CommitHash = GetCommitHash(),
            BotId = client.CurrentUser.Id,
            InstanceUrl = $"http://{creds.InstanceApiHost}:{creds.ApiPort}"
        };

        return Ok(toReturn);
    }

    /// <summary>
    ///     Gets a list of guildIds for the bot
    /// </summary>
    /// <returns>A list of guildIds for the bot</returns>
    [HttpGet("guilds")]
    public async Task<IActionResult> GetGuilds()
    {
        await Task.CompletedTask;
        return Ok(JsonSerializer.Serialize(client.Guilds.Select(x => x.Id), Options));
    }

    private static string GetCommitHash()
    {
        return BuildInfo.GitSha ?? "Unknown";
    }
}