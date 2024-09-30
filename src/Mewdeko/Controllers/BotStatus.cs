using System.Diagnostics;
using System.Reflection;
using Discord.Commands;
using Discord.Rest;
using Mewdeko.Common.Attributes.ASPNET;
using Mewdeko.Services.Impl;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
/// Endpoint for getting status such as guild count, bot version, etc
/// </summary>
[ApiController]
[Route("botapi/[controller]")]
[Authorize("ApiKeyPolicy")]
public class BotStatus(DiscordShardedClient client, StatsService statsService, CommandService commandService) : Controller
{
    /// <summary>
    /// Actual definition for getting bot status
    /// </summary>
    /// <returns>A BotStatus model</returns>
    [HttpGet]
    public async Task<IActionResult> GetStatus()
    {
        var rest = client.Rest;
        var curUser = await rest.GetUserAsync(client.CurrentUser.Id);
        var guilds = await rest.GetGuildsAsync();
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
            UserCount = client.Guilds.Select(x => x.Users.Count).Sum(),
            CommitHash = GetCommitHash(),
            BotId = client.CurrentUser.Id
        };

        return Ok(toReturn);
    }

    /// <summary>
    /// Gets a list of guildIds for the bot
    /// </summary>
    /// <returns>A list of guildIds for the bot</returns>
    [HttpGet("guilds")]
    public async Task<IActionResult> GetGuilds()
    {
        await Task.CompletedTask;
        return Ok(client.Guilds.Select(x => x.Id));
    }

    private string GetCommitHash()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var gitHashAttribute = assembly.GetCustomAttribute<GitHashAttribute>();

        if (gitHashAttribute != null)
        {
            return gitHashAttribute.Hash;
        }

        // Fallback method if attribute is not available
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "rev-parse HEAD",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
            string output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();

            return output.Length == 40 ? output : "Unknown";
        }
        catch
        {
            return "Unknown";
        }
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
        /// The current commit hash of the bot
        /// </summary>
        public string CommitHash { get; set; }

        /// <summary>
        /// The latency to discord
        /// </summary>
        public int BotLatency { get; set; }

        /// <summary>
        /// The name of the bot
        /// </summary>
        public string BotName { get; set; }

        /// <summary>
        /// The bots avatar.
        /// </summary>
        public string BotAvatar { get; set; }

        /// <summary>
        /// The bots banner, if any
        /// </summary>
        public string BotBanner { get; set; }

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

        /// <summary>
        /// The bots userId
        /// </summary>
        public ulong BotId { get; set; }
    }
}