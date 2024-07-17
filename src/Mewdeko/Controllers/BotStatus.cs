using Discord.Commands;
using Mewdeko.Services.Impl;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
/// Endpoint for getting status such as guild count, bot version, etc
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class BotStatus(DiscordShardedClient client, StatsService statsService, CommandService commandService) : Controller
{
    /// <summary>
    /// Actual definition for getting bot status
    /// </summary>
    /// <returns>A BotStatus model</returns>
    [HttpGet]
    public async Task<IActionResult> GetStatus()
    {
        var toReturn = new BotStatusModel
        {
            BotLatency = client.Latency,
            BotVersion = StatsService.BotVersion,
            CommandsCount = commandService.Commands.Count(),
            ModulesCount = commandService.Modules.Count(x => !x.IsSubmodule),
            DNetVersion = statsService.Library,
            BotStatus = client.Status.ToString(),
            UserCount = client.Guilds.Select(x => x.Users).Distinct().Count()
        };

        return Ok(toReturn);
    }




    /// <summary>
    /// The model defining bot status info
    /// </summary>
    public class BotStatusModel
    {
        /// <summary>
        /// The version of the bot
        /// </summary>
        public string BotVersion { get; set; }

        /// <summary>
        /// The latency to discord
        /// </summary>
        public int BotLatency { get; set; }

        /// <summary>
        /// The number of commands
        /// </summary>
        public int CommandsCount { get; set; }

        /// <summary>
        /// The number of modules
        /// </summary>
        public int ModulesCount { get; set; }

        /// <summary>
        /// The version of Discord.Net the bot is using
        /// </summary>
        public string DNetVersion { get; set; }

        /// <summary>
        /// The bots current status (idle, afk, etc)
        /// </summary>
        public string BotStatus { get; set; }

        /// <summary>
        /// The number of users in every guild (separated by distinct)
        /// </summary>
        public int UserCount { get; set; }
    }
}