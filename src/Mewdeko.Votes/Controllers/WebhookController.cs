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
    public async Task<IActionResult> DiscordsWebhook([FromBody]DiscordsVoteWebhookModel data)
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
    public async Task<IActionResult> TopggWebhook([FromBody] TopggVoteWebhookModel data)
    {
        Console.Write("test");
        _logger.LogInformation("User {UserId} has voted for Bot {BotId} on {Platform}",
            data.User,
            data.Bot,
            "top.gg");
        
        await _votesCache.AddNewTopggVote(data.User);
        await Events.InvokeTopGg(data);
        return Ok();
    }
}