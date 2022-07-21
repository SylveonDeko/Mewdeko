using Discord;
using Discord.Webhook;
using Discord.WebSocket;
using Mewdeko.Votes.Common;
using Mewdeko.Votes.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Mewdeko.Votes.Controllers;

[ApiController]
public class WebhookController : ControllerBase
{
    private readonly ILogger<WebhookController> _logger;
    public readonly WebhookEvents Events;

    private readonly FileVotesCache _votesCache;
    private readonly IConfiguration _conf;
    private readonly DiscordSocketClient _client;

    public WebhookController(ILogger<WebhookController> logger, FileVotesCache votesCache, IConfiguration conf,
        WebhookEvents events,
        DiscordSocketClient client)
    {
        _logger = logger;
        _votesCache = votesCache;
        _conf = conf;
        Events = events;
        _client = client;
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
        await Events.InvokeDiscords(data).ConfigureAwait(false);
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
        _ = Task.Factory.StartNew(async () =>
        {
            await _votesCache.AddNewTopggVote(data.User);
            await Events.InvokeTopGg(data).ConfigureAwait(false);
            await SendWebhook(ulong.Parse(data.User), "Top.GG").ConfigureAwait(false);
        }, TaskCreationOptions.LongRunning);
        return Task.FromResult<IActionResult>(Ok());
    }

    private async Task SendWebhook(ulong userId, string platform)
    {
        var user = await _client.Rest.GetUserAsync(userId);
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

        var eb = new EmbedBuilder()
                 .WithColor(new Color(222, 173, 74))
                 .WithTitle("Thank you for voting for Anime Cafe!")
                 .AddField("_ _", $"**Your total votes**: {(await FileVotesCache.GetVotesAsync(FileVotesCache.TOPGG_FILE)).Count(x => x == user.Id.ToString())}")
                 .WithThumbnailUrl(user.GetAvatarUrl() ?? user.GetDefaultAvatarUrl())
                 .WithFooter(new EmbedFooterBuilder().WithIconUrl(user.GetAvatarUrl() ?? user.GetDefaultAvatarUrl())
                                                     .WithText($"{user} | UserId: {user.Id}"));

        await webhookClient.SendMessageAsync($"{user.Mention} Has voted for mewdeko on {platform}!", embeds: new[] { eb.Build() }).ConfigureAwait(false);
    }
}