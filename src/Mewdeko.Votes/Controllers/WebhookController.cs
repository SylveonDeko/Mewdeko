using Discord;
using Discord.Webhook;
using Mewdeko.Votes.Common;
using Mewdeko.Votes.Services;
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Mewdeko.Votes.Controllers;

[ApiController]
public class WebhookController : ControllerBase
{
    private readonly ILogger<WebhookController> _logger;
    public readonly WebhookEvents Events;

    private readonly FileVotesCache _votesCache;
    private readonly IConfiguration _conf;

    public WebhookController(ILogger<WebhookController> logger, FileVotesCache votesCache, IConfiguration conf,
        WebhookEvents events)
    {
        _logger = logger;
        _votesCache = votesCache;
        _conf = conf;
        this.Events = events;
    }

    [HttpPost("/discordswebhook")]
    [Authorize(Policy = Policies.DISCORDS_AUTH)]
    public async Task<IActionResult> DiscordsWebhook([FromBody] DiscordsVoteWebhookModel data)
    {
        _logger.LogInformation("User {UserId} has voted for Bot {BotId} on {Platform}",
            data.User,
            data.Bot,
            "discords.com");

        await _votesCache.AddNewDiscordsVote(data.User);
        await Events.InvokeDiscords(data);
        return Ok();
    }

    [HttpPost("/topggwebhook")]
    [Authorize(Policy = Policies.TOPGG_AUTH)]
    public Task<IActionResult> TopggWebhook([FromBody] TopggVoteWebhookModel data)
    {
        _logger.LogInformation("User {UserId} has voted for Bot {BotId} on {Platform}",
            data.User,
            data.Bot,
            "top.gg");
        _ = Task.Run(async () =>
        {
            await _votesCache.AddNewTopggVote(data.User);
            await Events.InvokeTopGg(data);
            await SendWebhook(ulong.Parse(data.User), "Top.GG");
        });
        return Task.FromResult<IActionResult>(Ok());
    }

    private async Task SendWebhook(ulong userId, string platform)
    {
        DiscordWebhookClient webhookClient;
        try
        {
            webhookClient = new DiscordWebhookClient(_conf.GetSection("WebhookURL").Value);
        }
        catch
        {
            Console.Write("The webhook url is potentially misformatted or is incorrect.");
            return;
        }
        var eb = new EmbedBuilder().WithColor(new Color(222, 173, 74))
                                   .WithDescription("Thanks for voting! This will help mewdeko be listed higher on topgg so people will recognize its awesomness!")
                                   .WithThumbnailUrl("https://cdn.discordapp.com/emojis/914307922287276052.gif");

        await webhookClient.SendMessageAsync($"<@{userId}> Has voted for mewdeko on {platform}!", embeds: new[] { eb.Build() });
    }
}