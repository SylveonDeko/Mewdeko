using System.Threading.Tasks;
using Mewdeko.Votes.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Mewdeko.Votes.Controllers;

[ApiController]
public class WebhookController : ControllerBase
{
    private readonly ILogger<WebhookController> logger;
    public readonly WebhookEvents Events;

    public WebhookController(ILogger<WebhookController> logger,
        WebhookEvents events)
    {
        this.logger = logger;
        Events = events;
    }

    [HttpPost("/")]
    public Task<IActionResult> TopggWebhook([FromBody] VoteModel data)
    {
        logger.LogInformation("User {UserId} has voted for Bot {BotId} on {Platform}",
            data.User,
            data.Bot,
            "top.gg");
        _ = Task.Run(async () =>
        {
            await Events.InvokeTopGg(data, Request.Headers.Authorization);
        });
        return Task.FromResult<IActionResult>(Ok());
    }
}