using Mewdeko.Votes.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace Mewdeko.Votes.Controllers;

[ApiController]
public class WebhookController : ControllerBase
{
    private readonly ILogger<WebhookController> _logger;
    public readonly WebhookEvents Events;

    public WebhookController(ILogger<WebhookController> logger,
        WebhookEvents events)
    {
        _logger = logger;
        Events = events;
    }

    [HttpPost("/")]
    [Authorize(Policy = Policies.TOPGG_AUTH)]
    public Task<IActionResult> TopggWebhook([FromBody] VoteModel data)
    {
        _logger.LogInformation("User {UserId} has voted for Bot {BotId} on {Platform}",
            data.User,
            data.Bot,
            "top.gg");
        _ = Task.Factory.StartNew(async () =>
        {
            await Events.InvokeTopGg(data);
        }, TaskCreationOptions.LongRunning);
        return Task.FromResult<IActionResult>(Ok());
    }
    
}