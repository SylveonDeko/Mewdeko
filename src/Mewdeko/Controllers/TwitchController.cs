using System.IO;
using System.Text;
using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.AuthHandlers;
using Mewdeko.Controllers.Common.Twitch;
using Mewdeko.Database.DbContextStuff;
using Mewdeko.Modules.Twitch.Common;
using Mewdeko.Modules.Twitch.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Mewdeko.Controllers;

/// <summary>
///     Controller for dashboard Twitch OAuth and chat-bot configuration.
/// </summary>
[ApiController]
[Route("botapi/twitch")]
[Authorize("ApiKeyPolicy")]
[EnableRateLimiting("BasicPolicy")]
public class TwitchController : ControllerBase
{
    private readonly IBotCredentials credentials;
    private readonly IDataConnectionFactory dbFactory;
    private readonly ILogger<TwitchController> logger;
    private readonly TwitchApiClient twitchApiClient;
    private readonly TwitchCommandHandler twitchCommandHandler;
    private readonly TwitchEventSubService twitchEventSubService;
    private readonly TwitchService twitchService;

    /// <summary>
    ///     Initializes a new instance of the <see cref="TwitchController" /> class.
    /// </summary>
    public TwitchController(
        IBotCredentials credentials,
        IDataConnectionFactory dbFactory,
        TwitchApiClient twitchApiClient,
        TwitchService twitchService,
        TwitchEventSubService twitchEventSubService,
        TwitchCommandHandler twitchCommandHandler,
        ILogger<TwitchController> logger)
    {
        this.credentials = credentials;
        this.dbFactory = dbFactory;
        this.twitchApiClient = twitchApiClient;
        this.twitchService = twitchService;
        this.twitchEventSubService = twitchEventSubService;
        this.twitchCommandHandler = twitchCommandHandler;
        this.logger = logger;
    }

    /// <summary>Receives and verifies Twitch EventSub webhook deliveries.</summary>
    [HttpPost("eventsub")]
    [AllowAnonymous]
    [DisableRateLimiting]
    public async Task<IActionResult> EventSubWebhook()
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync();
        var messageId = Request.Headers["Twitch-Eventsub-Message-Id"].ToString();
        var timestamp = Request.Headers["Twitch-Eventsub-Message-Timestamp"].ToString();
        var signature = Request.Headers["Twitch-Eventsub-Message-Signature"].ToString();
        var messageType = Request.Headers["Twitch-Eventsub-Message-Type"].ToString();

        if (string.IsNullOrWhiteSpace(messageId) ||
            !twitchEventSubService.VerifySignature(messageId, timestamp, signature, body))
            return StatusCode(403);

        if (!twitchEventSubService.TryRegisterMessage(messageId))
            return NoContent();

        if (messageType == "webhook_callback_verification")
        {
            var (challenge, subscriptionId) = TwitchEventSubService.GetVerificationInfo(body);
            if (challenge is null)
                return BadRequest();

            if (subscriptionId is not null)
                await twitchEventSubService.MarkSubscriptionEnabledAsync(subscriptionId);

            return Content(challenge, "text/plain", Encoding.UTF8);
        }

        if (messageType == "revocation")
            await twitchEventSubService.ProcessRevocationAsync(body);
        else if (messageType == "notification")
            _ = twitchEventSubService.ProcessNotificationAsync(body);

        return NoContent();
    }

    /// <summary>
    ///     Generates a Twitch OAuth authorization URL for bot or channel authorization.
    ///     The bot-account mode is restricted to bot owners: <c>TwitchBotAccounts</c> is a single
    ///     row shared by every guild, so any non-owner authorizing it would silently hijack the
    ///     chat identity used by the entire installation.
    /// </summary>
    [HttpGet("oauth/url")]
    public async Task<IActionResult> GetOAuthUrl([FromQuery] ulong guildId,
        [FromQuery] string mode = TwitchOAuthModes.Channel)
    {
        mode = NormalizeMode(mode);
        if (mode is not TwitchOAuthModes.Bot and not TwitchOAuthModes.Channel)
            return BadRequest(new
            {
                error = "Invalid Twitch OAuth mode"
            });

        if (mode == TwitchOAuthModes.Bot && !await IsCallerBotOwnerAsync())
            return Forbid();

        if (string.IsNullOrWhiteSpace(credentials.TwitchClientId))
            return BadRequest(new
            {
                error = "TwitchClientId is not configured"
            });

        var state = $"{guildId}:{mode}:{Guid.NewGuid():N}";
        var redirectUri = GetRedirectUri();
        var authUrl = twitchApiClient.GetAuthorizationUrl(credentials.TwitchClientId, redirectUri, state, mode);

        return Ok(new TwitchOAuthResponse
        {
            AuthorizationUrl = authUrl, State = state, Mode = mode
        });
    }

    /// <summary>
    ///     Handles a Twitch OAuth callback after the dashboard receives the code/state query params.
    /// </summary>
    [HttpGet("oauth/callback")]
    [EnableRateLimiting("AuthPolicy")]
    public async Task<IActionResult> OAuthCallback(
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? error)
    {
        if (!string.IsNullOrWhiteSpace(error))
            return BadRequest(new
            {
                error = $"OAuth error: {error}"
            });

        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
            return BadRequest(new
            {
                error = "Missing required OAuth parameters"
            });

        if (!TryParseState(state, out var guildId, out var mode))
            return BadRequest(new
            {
                error = "Invalid OAuth state"
            });

        if (mode == TwitchOAuthModes.Bot && !await IsCallerBotOwnerAsync())
            return Forbid();

        if (string.IsNullOrWhiteSpace(credentials.TwitchClientId) ||
            string.IsNullOrWhiteSpace(credentials.TwitchClientSecret))
            return StatusCode(500, new
            {
                error = "Twitch client credentials are not configured"
            });

        var token = await twitchApiClient.ExchangeCodeAsync(
            code,
            credentials.TwitchClientId,
            credentials.TwitchClientSecret,
            GetRedirectUri());

        if (token is null)
            return StatusCode(500, new
            {
                error = "Failed to exchange Twitch authorization code"
            });

        var validation = await twitchApiClient.ValidateTokenAsync(token.AccessToken);
        if (validation is null)
            return StatusCode(500, new
            {
                error = "Failed to validate Twitch access token"
            });

        var user = await twitchApiClient.GetUserAsync(
            credentials.TwitchClientId,
            token.AccessToken,
            validation.UserId);

        var username = user?.Login ?? validation.Login;
        var displayName = user?.DisplayName ?? username;
        var tokenExpiry = DateTime.UtcNow.AddSeconds(Math.Max(0, token.ExpiresIn));
        var discordUserId = await GetDashboardUserIdAsync();

        await using var db = await dbFactory.CreateConnectionAsync();
        if (mode == TwitchOAuthModes.Bot)
            await UpsertBotAccountAsync(db, validation.UserId, username, displayName, token, tokenExpiry);
        else
            await UpsertChannelAuthorizationAsync(db, guildId, validation.UserId, username, displayName, token,
                tokenExpiry, discordUserId);

        await twitchEventSubService.ReconcileSubscriptionsAsync();

        logger.LogInformation("Completed Twitch OAuth mode {Mode} for guild {GuildId} as {TwitchUser}",
            mode, guildId, username);

        return Ok(new TwitchOAuthCallbackResponse
        {
            Success = true,
            Message = mode == TwitchOAuthModes.Bot
                ? "Twitch bot account connected."
                : "Twitch channel authorized.",
            GuildId = guildId,
            Mode = mode,
            TwitchUserId = validation.UserId,
            TwitchUsername = username,
            DisplayName = displayName
        });
    }

    /// <summary>
    ///     Gets Twitch OAuth and bot configuration status for a guild.
    /// </summary>
    [HttpGet("oauth/status")]
    public async Task<IActionResult> GetOAuthStatus([FromQuery] ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var config = await db.TwitchGuildConfigs.FirstOrDefaultAsync(x => x.GuildId == guildId);
        var bot = await db.TwitchBotAccounts.OrderByDescending(x => x.Id).FirstOrDefaultAsync();
        var channel = await db.TwitchChannelAuthorizations.FirstOrDefaultAsync(x => x.GuildId == guildId);

        return Ok(new TwitchOAuthStatusResponse
        {
            IsConfigured = bot is not null && channel is not null && config is { Enabled: true },
            HasBotAccount = bot is not null,
            HasChannelAuthorization = channel is not null,
            UseEventSub = config?.UseEventSub ?? true,
            BotUsername = bot?.TwitchUsername,
            BotDisplayName = bot?.DisplayName,
            ChannelUsername = channel?.TwitchUsername ?? config?.TwitchChannel,
            ChannelDisplayName = channel?.DisplayName ?? config?.TwitchDisplayName,
            TwitchUserId = channel?.TwitchUserId ?? config?.TwitchUserId,
            CommandPrefix = config?.CommandPrefix,
            Language = config?.Language,
            GoLiveChannelId = config?.GoLiveChannelId,
            GoLiveMessage = config?.GoLiveMessage,
            SubNotificationChannelId = config?.SubNotificationChannelId,
            SubNotificationMessage = config?.SubNotificationMessage,
            RaidNotificationChannelId = config?.RaidNotificationChannelId,
            RaidNotificationMessage = config?.RaidNotificationMessage,
            BotTokenExpiry = bot?.TokenExpiresAt,
            ChannelTokenExpiry = channel?.TokenExpiresAt,
            LastAuthorizedAt = config?.LastAuthorizedAt,
            LastEventAt = config?.LastEventAt
        });
    }

    /// <summary>
    ///     Gets Twitch bot configuration for a guild.
    /// </summary>
    [HttpGet("config")]
    public async Task<IActionResult> GetConfig([FromQuery] ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var config = await db.TwitchGuildConfigs.FirstOrDefaultAsync(x => x.GuildId == guildId);
        if (config is null)
            return NotFound(new
            {
                error = "No Twitch configuration exists for this guild"
            });

        return Ok(ToConfigResponse(config));
    }

    /// <summary>
    ///     Updates Twitch bot configuration for a guild.
    /// </summary>
    [HttpPost("config")]
    public async Task<IActionResult> UpdateConfig([FromQuery] ulong guildId,
        [FromBody] TwitchConfigUpdateRequest request)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var config = await db.TwitchGuildConfigs.FirstOrDefaultAsync(x => x.GuildId == guildId);
        if (config is null)
        {
            config = new TwitchGuildConfig
            {
                GuildId = guildId,
                TwitchChannel = request.TwitchChannel?.TrimStart('#').ToLowerInvariant() ?? "",
                CommandPrefix = string.IsNullOrWhiteSpace(request.CommandPrefix) ? "!" : request.CommandPrefix,
                Enabled = request.Enabled ?? true,
                UseEventSub = request.UseEventSub ?? true,
                DateAdded = DateTime.UtcNow
            };
            ApplyConfigUpdate(config, request);
            config.Id = await db.InsertWithInt32IdentityAsync(config);
        }
        else
        {
            ApplyConfigUpdate(config, request);
            await db.UpdateAsync(config);
        }

        return Ok(ToConfigResponse(config));
    }

    /// <summary>
    ///     Disconnects a guild's Twitch channel authorization.
    /// </summary>
    [HttpDelete("oauth/disconnect")]
    public async Task<IActionResult> Disconnect([FromQuery] ulong guildId,
        [FromQuery] string mode = TwitchOAuthModes.Channel)
    {
        mode = NormalizeMode(mode);

        if (mode == TwitchOAuthModes.Bot && !await IsCallerBotOwnerAsync())
            return Forbid();

        await using var db = await dbFactory.CreateConnectionAsync();

        if (mode == TwitchOAuthModes.Bot)
            await db.TwitchBotAccounts.DeleteAsync();
        else
        {
            await db.TwitchChannelAuthorizations.Where(x => x.GuildId == guildId).DeleteAsync();
            var config = await db.TwitchGuildConfigs.FirstOrDefaultAsync(x => x.GuildId == guildId);
            if (config is not null)
            {
                config.Enabled = false;
                await db.UpdateAsync(config);
            }
        }

        return Ok(new
        {
            message = mode == TwitchOAuthModes.Bot
                ? "Twitch bot account disconnected."
                : "Twitch channel disconnected."
        });
    }

    /// <summary>
    ///     Lists all built-in Twitch chat commands and the permission level required to run each one.
    /// </summary>
    [HttpGet("chat-commands")]
    public IActionResult GetChatCommands()
    {
        var commands = twitchCommandHandler.GetRegisteredCommands()
            .Select(c => new TwitchChatCommandResponse
            {
                Name = c.Name, Permission = c.Permission.ToString()
            });

        return Ok(commands);
    }

    /// <summary>
    ///     Lists all dashboard-managed custom Twitch chat commands for a guild.
    /// </summary>
    [HttpGet("custom-commands")]
    public async Task<IActionResult> GetCustomCommands([FromQuery] ulong guildId)
    {
        var commands = await twitchService.GetCustomCommandsAsync(guildId);
        return Ok(commands.Select(ToCustomCommandResponse));
    }

    /// <summary>
    ///     Creates or updates a dashboard-managed custom Twitch chat command.
    /// </summary>
    [HttpPost("custom-commands")]
    public async Task<IActionResult> UpsertCustomCommand(
        [FromQuery] ulong guildId,
        [FromBody] TwitchCustomCommandRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Response))
            return BadRequest(new
            {
                error = "name and response are required"
            });

        if (!Enum.TryParse<TwitchCommandPermission>(request.Permission, true, out var permission))
            permission = TwitchCommandPermission.Everyone;

        var command = await twitchService.UpsertCustomCommandAsync(
            guildId,
            request.Name,
            request.Response,
            permission,
            request.CooldownSeconds,
            request.Enabled);

        return Ok(ToCustomCommandResponse(command));
    }

    /// <summary>
    ///     Removes a dashboard-managed custom Twitch chat command.
    /// </summary>
    [HttpDelete("custom-commands")]
    public async Task<IActionResult> RemoveCustomCommand([FromQuery] ulong guildId, [FromQuery] string name)
    {
        var removed = await twitchService.RemoveCustomCommandAsync(guildId, name);
        return Ok(new
        {
            removed
        });
    }

    /// <summary>
    ///     Renders a custom Twitch command response without sending it to chat.
    /// </summary>
    [HttpPost("custom-commands/preview")]
    public async Task<IActionResult> PreviewCustomCommand(
        [FromQuery] ulong guildId,
        [FromBody] TwitchCommandPreviewRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new
            {
                error = "name is required"
            });

        var response = await twitchService.PreviewCustomCommandAsync(guildId, request.Name, request.Args);
        if (response is null)
            return NotFound(new
            {
                error = "No custom Twitch command found with that name"
            });

        await twitchService.RecordEventHistoryAsync(guildId, "command.preview", "dashboard",
            $"Previewed custom command {request.Name}", true);

        return Ok(new TwitchCommandPreviewResponse
        {
            Response = response
        });
    }

    /// <summary>
    ///     Lists channel point redemption action templates for a guild.
    /// </summary>
    [HttpGet("redemptions")]
    public async Task<IActionResult> GetRedemptionActions([FromQuery] ulong guildId)
    {
        var actions = await twitchService.GetRedemptionActionsAsync(guildId);
        return Ok(actions.Select(ToRedemptionActionResponse));
    }

    /// <summary>
    ///     Creates or updates a channel point redemption action template.
    /// </summary>
    [HttpPost("redemptions")]
    public async Task<IActionResult> UpsertRedemptionAction(
        [FromQuery] ulong guildId,
        [FromBody] TwitchRedemptionActionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RewardTitle))
            return BadRequest(new
            {
                error = "rewardTitle is required"
            });

        var action = await twitchService.UpsertRedemptionActionAsync(
            guildId,
            request.RewardTitle,
            request.TwitchResponse,
            request.DiscordChannelId,
            request.DiscordMessage);

        return Ok(ToRedemptionActionResponse(action));
    }

    /// <summary>
    ///     Removes a channel point redemption action template.
    /// </summary>
    [HttpDelete("redemptions")]
    public async Task<IActionResult> RemoveRedemptionAction([FromQuery] ulong guildId, [FromQuery] string rewardTitle)
    {
        var removed = await twitchService.RemoveRedemptionActionAsync(guildId, rewardTitle);
        return Ok(new
        {
            removed
        });
    }

    /// <summary>
    ///     Lists repeating Twitch chat message timers for a guild.
    /// </summary>
    [HttpGet("timers")]
    public async Task<IActionResult> GetTimers([FromQuery] ulong guildId)
    {
        var timers = await twitchService.GetTimersAsync(guildId);
        return Ok(timers.Select(ToTimerResponse));
    }

    /// <summary>
    ///     Creates or updates a repeating Twitch chat message timer.
    /// </summary>
    [HttpPost("timers")]
    public async Task<IActionResult> UpsertTimer([FromQuery] ulong guildId, [FromBody] TwitchTimerRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Messages))
            return BadRequest(new
            {
                error = "name and messages are required"
            });

        var timer = await twitchService.UpsertTimerAsync(
            guildId,
            request.Name,
            request.Messages,
            request.IntervalMinutes,
            request.MinChatMessages,
            request.OnlineOnly,
            request.RandomizeMessages,
            request.Enabled);

        return Ok(ToTimerResponse(timer));
    }

    /// <summary>
    ///     Changes whether a repeating Twitch chat message timer is enabled.
    /// </summary>
    [HttpPost("timers/state")]
    public async Task<IActionResult> SetTimerState(
        [FromQuery] ulong guildId,
        [FromQuery] string name,
        [FromBody] TwitchTimerStateRequest request)
    {
        var updated = await twitchService.SetTimerEnabledAsync(guildId, name, request.Enabled);
        return Ok(new
        {
            updated
        });
    }

    /// <summary>
    ///     Removes a repeating Twitch chat message timer.
    /// </summary>
    [HttpDelete("timers")]
    public async Task<IActionResult> RemoveTimer([FromQuery] ulong guildId, [FromQuery] string name)
    {
        var removed = await twitchService.RemoveTimerAsync(guildId, name);
        return Ok(new
        {
            removed
        });
    }

    /// <summary>
    ///     Sends a repeating Twitch chat message timer immediately for testing.
    /// </summary>
    [HttpPost("timers/test")]
    public async Task<IActionResult> TestTimer([FromQuery] ulong guildId, [FromQuery] string name)
    {
        var sent = await twitchService.TestTimerAsync(guildId, name);
        if (sent is null)
            return NotFound(new
            {
                error = "No Twitch timer found with that name"
            });

        return Ok(new TwitchTimerTestResponse
        {
            Message = sent
        });
    }

    /// <summary>
    ///     Sends a dashboard-generated test event through the configured Twitch event templates.
    /// </summary>
    [HttpPost("test/{eventType}")]
    public async Task<IActionResult> SendTestEvent([FromQuery] ulong guildId, [FromRoute] string eventType)
    {
        try
        {
            switch (eventType.Trim().ToLowerInvariant())
            {
                case "golive":
                    await twitchService.TestGoLiveNotificationAsync(guildId);
                    break;
                case "sub":
                    await twitchService.TestSubNotificationAsync(guildId);
                    break;
                case "raid":
                    await twitchService.TestRaidNotificationAsync(guildId);
                    break;
                default:
                    return BadRequest(new
                    {
                        error = "eventType must be golive, sub, or raid"
                    });
            }

            await twitchService.RecordEventHistoryAsync(guildId, $"test.{eventType}", "dashboard",
                $"Sent dashboard test {eventType} event", true);
            return Ok(new
            {
                message = $"Sent test {eventType} event."
            });
        }
        catch (Exception ex)
        {
            await twitchService.RecordEventHistoryAsync(guildId, $"test.{eventType}", "dashboard",
                $"Failed dashboard test {eventType} event", false, ex.Message);
            return BadRequest(new
            {
                error = ex.Message
            });
        }
    }

    /// <summary>
    ///     Lists recent EventSub and dashboard test history for a guild.
    /// </summary>
    [HttpGet("event-history")]
    public async Task<IActionResult> GetEventHistory([FromQuery] ulong guildId, [FromQuery] int limit = 50)
    {
        var events = await twitchService.GetEventHistoryAsync(guildId, limit);
        return Ok(events.Select(ToEventHistoryResponse));
    }

    /// <summary>
    ///     Gets Twitch OAuth scope and EventSub subscription health for a guild.
    /// </summary>
    [HttpGet("health")]
    public async Task<IActionResult> GetHealth([FromQuery] ulong guildId)
    {
        var health = await twitchService.GetHealthSnapshotAsync(guildId);
        return Ok(ToHealthResponse(health));
    }

    /// <summary>
    ///     Gets supported Twitch template variables grouped by feature area.
    /// </summary>
    [HttpGet("variables")]
    public IActionResult GetVariables()
    {
        return Ok(new TwitchVariableDocsResponse
        {
            Groups = TwitchService.GetVariableDocs()
        });
    }

    /// <summary>
    ///     Lists saved Twitch quotes for a guild.
    /// </summary>
    [HttpGet("quotes")]
    public async Task<IActionResult> GetQuotes(
        [FromQuery] ulong guildId,
        [FromQuery] string? search = null,
        [FromQuery] int limit = 50)
    {
        var quotes = await twitchService.GetQuotesAsync(guildId, search, limit);
        return Ok(quotes.Select(ToQuoteResponse));
    }

    /// <summary>
    ///     Adds a saved Twitch quote for a guild.
    /// </summary>
    [HttpPost("quotes")]
    public async Task<IActionResult> AddQuote([FromQuery] ulong guildId, [FromBody] TwitchQuoteRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
            return BadRequest(new
            {
                error = "text is required"
            });

        var quote = await twitchService.AddQuoteAsync(guildId, request.Text, request.Author, request.AddedBy);
        await twitchService.RecordEventHistoryAsync(guildId, "quote.add", "dashboard",
            $"Added quote #{quote.Id}", true);
        return Ok(ToQuoteResponse(quote));
    }

    /// <summary>
    ///     Removes a saved Twitch quote from a guild.
    /// </summary>
    [HttpDelete("quotes")]
    public async Task<IActionResult> RemoveQuote([FromQuery] ulong guildId, [FromQuery] int quoteId)
    {
        var removed = await twitchService.RemoveQuoteAsync(guildId, quoteId);
        await twitchService.RecordEventHistoryAsync(guildId, "quote.remove", "dashboard",
            $"Removed quote #{quoteId}", removed, removed ? null : "Quote not found");
        return Ok(new
        {
            removed
        });
    }

    /// <summary>
    ///     Sends a dashboard-authored Twitch chat message.
    /// </summary>
    [HttpPost("chat/send")]
    public async Task<IActionResult> SendChatMessage([FromQuery] ulong guildId,
        [FromBody] TwitchChatSendRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(new
            {
                error = "message is required"
            });

        var sent = await twitchService.SendDashboardChatMessageAsync(guildId, request.Message);
        return Ok(new TwitchActionResponse
        {
            Success = sent, Message = sent ? "Sent Twitch chat message." : "Failed to send Twitch chat message."
        });
    }

    /// <summary>
    ///     Creates a Twitch stream marker for the configured channel.
    /// </summary>
    [HttpPost("marker")]
    public async Task<IActionResult> CreateMarker([FromQuery] ulong guildId, [FromBody] TwitchMarkerRequest request)
    {
        var created = await twitchService.CreateStreamMarkerAsync(guildId, request.Description);
        return Ok(new TwitchActionResponse
        {
            Success = created,
            Message = created ? "Created Twitch stream marker." : "Failed to create Twitch stream marker."
        });
    }

    /// <summary>
    ///     Creates a Twitch clip for the configured channel.
    /// </summary>
    [HttpPost("clip")]
    public async Task<IActionResult> CreateClip([FromQuery] ulong guildId)
    {
        var url = await twitchService.CreateClipAsync(guildId);
        var success = !string.IsNullOrWhiteSpace(url);
        await twitchService.RecordEventHistoryAsync(guildId, "clip.create", "dashboard",
            success ? "Created Twitch clip" : "Failed to create Twitch clip", success);
        return Ok(new TwitchActionResponse
        {
            Success = success, Message = success ? "Created Twitch clip." : "Failed to create Twitch clip.", Url = url
        });
    }

    /// <summary>
    ///     Creates a Twitch poll for the configured channel.
    /// </summary>
    [HttpPost("poll")]
    public async Task<IActionResult> CreatePoll([FromQuery] ulong guildId, [FromBody] TwitchPollRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || request.Choices.Count < 2)
            return BadRequest(new
            {
                error = "title and at least two choices are required"
            });

        var created =
            await twitchService.CreatePollAsync(guildId, request.Title, request.Choices, request.DurationSeconds);
        return Ok(new TwitchActionResponse
        {
            Success = created, Message = created ? "Created Twitch poll." : "Failed to create Twitch poll."
        });
    }

    /// <summary>
    ///     Bans or times out a Twitch user.
    /// </summary>
    [HttpPost("moderation/ban")]
    public async Task<IActionResult> ModerateUser([FromQuery] ulong guildId, [FromBody] TwitchModerationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username))
            return BadRequest(new
            {
                error = "username is required"
            });

        var moderated = await twitchService.ModerateUserAsync(
            guildId,
            request.Username,
            request.DurationSeconds,
            request.Reason);
        return Ok(new TwitchActionResponse
        {
            Success = moderated,
            Message = moderated ? "Applied Twitch moderation action." : "Failed to apply Twitch moderation action."
        });
    }

    /// <summary>
    ///     Removes a ban or timeout from a Twitch user.
    /// </summary>
    [HttpPost("moderation/unban")]
    public async Task<IActionResult> UnmoderateUser([FromQuery] ulong guildId,
        [FromBody] TwitchModerationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username))
            return BadRequest(new
            {
                error = "username is required"
            });

        var removed = await twitchService.UnmoderateUserAsync(guildId, request.Username);
        return Ok(new TwitchActionResponse
        {
            Success = removed,
            Message = removed ? "Removed Twitch ban or timeout." : "Failed to remove Twitch ban or timeout."
        });
    }

    /// <summary>
    ///     Deletes a Twitch chat message by message ID.
    /// </summary>
    [HttpPost("moderation/delete-message")]
    public async Task<IActionResult> DeleteChatMessage(
        [FromQuery] ulong guildId,
        [FromBody] TwitchDeleteMessageRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.MessageId))
            return BadRequest(new
            {
                error = "messageId is required"
            });

        var deleted = await twitchService.DeleteChatMessageAsync(guildId, request.MessageId);
        return Ok(new TwitchActionResponse
        {
            Success = deleted,
            Message = deleted ? "Deleted Twitch chat message." : "Failed to delete Twitch chat message."
        });
    }

    /// <summary>
    ///     Lists all Discord-to-Twitch account links for a guild.
    /// </summary>
    [HttpGet("links")]
    public async Task<IActionResult> GetLinks([FromQuery] ulong guildId)
    {
        var links = await twitchService.GetAllLinksAsync(guildId);
        return Ok(links.Select(l => new TwitchAccountLinkResponse
        {
            DiscordUserId = l.DiscordUserId, TwitchUsername = l.TwitchUsername
        }));
    }

    /// <summary>
    ///     Links a Discord user to a Twitch username for a guild.
    /// </summary>
    [HttpPost("links")]
    public async Task<IActionResult> UpsertLink([FromQuery] ulong guildId, [FromBody] TwitchAccountLinkRequest request)
    {
        if (request.DiscordUserId == 0 || string.IsNullOrWhiteSpace(request.TwitchUsername))
            return BadRequest(new
            {
                error = "discordUserId and twitchUsername are required"
            });

        await twitchService.LinkAccountAsync(guildId, request.DiscordUserId, request.TwitchUsername);
        return Ok(new TwitchAccountLinkResponse
        {
            DiscordUserId = request.DiscordUserId, TwitchUsername = request.TwitchUsername.ToLowerInvariant()
        });
    }

    /// <summary>
    ///     Removes a Discord user's Twitch account link for a guild.
    /// </summary>
    [HttpDelete("links")]
    public async Task<IActionResult> RemoveLink([FromQuery] ulong guildId, [FromQuery] ulong discordUserId)
    {
        await twitchService.UnlinkAccountAsync(guildId, discordUserId);
        return Ok(new
        {
            message = "Twitch account link removed."
        });
    }

    private static TwitchConfigResponse ToConfigResponse(TwitchGuildConfig config)
    {
        return new TwitchConfigResponse
        {
            GuildId = config.GuildId,
            TwitchChannel = config.TwitchChannel,
            CommandPrefix = config.CommandPrefix,
            Enabled = config.Enabled,
            UseEventSub = config.UseEventSub,
            Language = config.Language,
            GoLiveChannelId = config.GoLiveChannelId,
            GoLiveMessage = config.GoLiveMessage,
            SubNotificationChannelId = config.SubNotificationChannelId,
            SubNotificationMessage = config.SubNotificationMessage,
            RaidNotificationChannelId = config.RaidNotificationChannelId,
            RaidNotificationMessage = config.RaidNotificationMessage,
            TwitchUserId = config.TwitchUserId,
            TwitchDisplayName = config.TwitchDisplayName,
            LastAuthorizedAt = config.LastAuthorizedAt,
            LastEventAt = config.LastEventAt
        };
    }

    private static void ApplyConfigUpdate(TwitchGuildConfig config, TwitchConfigUpdateRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.TwitchChannel))
            config.TwitchChannel = request.TwitchChannel.TrimStart('#').ToLowerInvariant();

        if (!string.IsNullOrWhiteSpace(request.CommandPrefix))
            config.CommandPrefix = request.CommandPrefix;

        if (request.Enabled.HasValue)
            config.Enabled = request.Enabled.Value;

        if (request.UseEventSub.HasValue)
            config.UseEventSub = request.UseEventSub.Value;

        if (request.Language is not null)
            config.Language = string.IsNullOrWhiteSpace(request.Language) ? null : request.Language;

        if (request.GoLiveChannelId.HasValue)
            config.GoLiveChannelId = request.GoLiveChannelId == 0 ? null : request.GoLiveChannelId;

        if (request.GoLiveMessage is not null)
            config.GoLiveMessage = string.IsNullOrWhiteSpace(request.GoLiveMessage) ? null : request.GoLiveMessage;

        if (request.SubNotificationChannelId.HasValue)
            config.SubNotificationChannelId =
                request.SubNotificationChannelId == 0 ? null : request.SubNotificationChannelId;

        if (request.SubNotificationMessage is not null)
            config.SubNotificationMessage = string.IsNullOrWhiteSpace(request.SubNotificationMessage)
                ? null
                : request.SubNotificationMessage;

        if (request.RaidNotificationChannelId.HasValue)
            config.RaidNotificationChannelId =
                request.RaidNotificationChannelId == 0 ? null : request.RaidNotificationChannelId;

        if (request.RaidNotificationMessage is not null)
            config.RaidNotificationMessage = string.IsNullOrWhiteSpace(request.RaidNotificationMessage)
                ? null
                : request.RaidNotificationMessage;
    }

    private static TwitchCustomCommandResponse ToCustomCommandResponse(TwitchCustomCommand command)
    {
        return new TwitchCustomCommandResponse
        {
            Id = command.Id,
            Name = command.Name,
            Response = command.Response,
            Permission = ((TwitchCommandPermission)command.PermissionLevel).ToString(),
            CooldownSeconds = command.CooldownSeconds,
            Enabled = command.Enabled,
            UseCount = command.UseCount,
            LastUsedAt = command.LastUsedAt,
            LastUpdatedAt = command.LastUpdatedAt
        };
    }

    private static TwitchRedemptionActionResponse ToRedemptionActionResponse(TwitchRedemptionAction action)
    {
        return new TwitchRedemptionActionResponse
        {
            Id = action.Id,
            RewardTitle = action.RewardTitle,
            TwitchResponse = action.TwitchResponse,
            DiscordChannelId = action.DiscordChannelId,
            DiscordMessage = action.DiscordMessage,
            Enabled = action.Enabled,
            LastUpdatedAt = action.LastUpdatedAt
        };
    }

    private static TwitchEventHistoryResponse ToEventHistoryResponse(TwitchEventHistory entry)
    {
        return new TwitchEventHistoryResponse
        {
            Id = entry.Id,
            EventType = entry.EventType,
            Source = entry.Source,
            Succeeded = entry.Succeeded,
            Message = entry.Message,
            Error = entry.Error,
            RawPayload = entry.RawPayload,
            DateAdded = entry.DateAdded
        };
    }

    private static TwitchQuoteResponse ToQuoteResponse(TwitchQuote quote)
    {
        return new TwitchQuoteResponse
        {
            Id = quote.Id,
            Text = quote.Text,
            Author = quote.Author,
            AddedBy = quote.AddedBy,
            DateAdded = quote.DateAdded
        };
    }

    private static TwitchHealthResponse ToHealthResponse(TwitchService.TwitchHealthSnapshot snapshot)
    {
        return new TwitchHealthResponse
        {
            HasConfig = snapshot.HasConfig,
            Enabled = snapshot.Enabled,
            TwitchChannel = snapshot.TwitchChannel,
            EventSubEnabled = snapshot.EventSubEnabled,
            HasBotAccount = snapshot.HasBotAccount,
            HasChannelAuthorization = snapshot.HasChannelAuthorization,
            BotMissingScopes = snapshot.BotMissingScopes,
            ChannelMissingScopes = snapshot.ChannelMissingScopes,
            BotTokenExpiresAt = snapshot.BotTokenExpiresAt,
            ChannelTokenExpiresAt = snapshot.ChannelTokenExpiresAt,
            LastEventAt = snapshot.LastEventAt,
            Subscriptions = snapshot.Subscriptions.Select(x => new TwitchEventSubSubscriptionHealthResponse
            {
                TwitchSubscriptionId = x.TwitchSubscriptionId,
                Type = x.SubscriptionType,
                Status = x.Status,
                SessionId = x.SessionId,
                LastUpdatedAt = x.LastUpdatedAt ?? x.DateAdded ?? DateTime.MinValue
            }).ToList()
        };
    }

    private static TwitchTimerResponse ToTimerResponse(TwitchTimer timer)
    {
        return new TwitchTimerResponse
        {
            Id = timer.Id,
            Name = timer.Name,
            Messages = timer.Messages,
            IntervalMinutes = timer.IntervalMinutes,
            MinChatMessages = timer.MinChatMessages,
            OnlineOnly = timer.OnlineOnly,
            RandomizeMessages = timer.RandomizeMessages,
            Enabled = timer.Enabled,
            LastSentAt = timer.LastSentAt,
            LastUpdatedAt = timer.LastUpdatedAt
        };
    }

    private async Task UpsertBotAccountAsync(
        MewdekoDb db,
        string twitchUserId,
        string username,
        string displayName,
        TwitchTokenResponse token,
        DateTime tokenExpiry)
    {
        var existing = await db.TwitchBotAccounts.FirstOrDefaultAsync(x => x.TwitchUserId == twitchUserId);
        if (existing is null)
        {
            await db.InsertAsync(new TwitchBotAccount
            {
                TwitchUserId = twitchUserId,
                TwitchUsername = username,
                DisplayName = displayName,
                AccessToken = token.AccessToken,
                RefreshToken = token.RefreshToken,
                Scopes = string.Join(' ', token.Scopes),
                TokenExpiresAt = tokenExpiry,
                DateAdded = DateTime.UtcNow
            });
            return;
        }

        existing.TwitchUsername = username;
        existing.DisplayName = displayName;
        existing.AccessToken = token.AccessToken;
        existing.RefreshToken = token.RefreshToken;
        existing.Scopes = string.Join(' ', token.Scopes);
        existing.TokenExpiresAt = tokenExpiry;
        existing.LastRefreshedAt = DateTime.UtcNow;
        await db.UpdateAsync(existing);
    }

    private async Task UpsertChannelAuthorizationAsync(
        MewdekoDb db,
        ulong guildId,
        string twitchUserId,
        string username,
        string displayName,
        TwitchTokenResponse token,
        DateTime tokenExpiry,
        ulong? discordUserId)
    {
        var existing = await db.TwitchChannelAuthorizations.FirstOrDefaultAsync(x => x.GuildId == guildId);
        if (existing is null)
        {
            await db.InsertAsync(new TwitchChannelAuthorization
            {
                GuildId = guildId,
                TwitchUserId = twitchUserId,
                TwitchUsername = username,
                DisplayName = displayName,
                AccessToken = token.AccessToken,
                RefreshToken = token.RefreshToken,
                Scopes = string.Join(' ', token.Scopes),
                TokenExpiresAt = tokenExpiry,
                AuthorizedByDiscordUserId = discordUserId,
                DateAdded = DateTime.UtcNow
            });
        }
        else
        {
            existing.TwitchUserId = twitchUserId;
            existing.TwitchUsername = username;
            existing.DisplayName = displayName;
            existing.AccessToken = token.AccessToken;
            existing.RefreshToken = token.RefreshToken;
            existing.Scopes = string.Join(' ', token.Scopes);
            existing.TokenExpiresAt = tokenExpiry;
            existing.AuthorizedByDiscordUserId = discordUserId;
            existing.LastRefreshedAt = DateTime.UtcNow;
            await db.UpdateAsync(existing);
        }

        var config = await db.TwitchGuildConfigs.FirstOrDefaultAsync(x => x.GuildId == guildId);
        if (config is null)
        {
            await db.InsertAsync(new TwitchGuildConfig
            {
                GuildId = guildId,
                TwitchChannel = username,
                TwitchUserId = twitchUserId,
                TwitchDisplayName = displayName,
                CommandPrefix = "!",
                Enabled = true,
                UseEventSub = true,
                AuthorizedByDiscordUserId = discordUserId,
                LastAuthorizedAt = DateTime.UtcNow,
                DateAdded = DateTime.UtcNow
            });
            return;
        }

        config.TwitchChannel = username;
        config.TwitchUserId = twitchUserId;
        config.TwitchDisplayName = displayName;
        config.Enabled = true;
        config.UseEventSub = true;
        config.AuthorizedByDiscordUserId = discordUserId;
        config.LastAuthorizedAt = DateTime.UtcNow;
        await db.UpdateAsync(config);
    }

    private string GetRedirectUri()
    {
        var baseUrl = string.IsNullOrWhiteSpace(credentials.DashboardUrl)
            ? $"{Request.Scheme}://{Request.Host}"
            : credentials.DashboardUrl.TrimEnd('/');

        return $"{baseUrl}/dashboard/twitch";
    }

    /// <summary>
    ///     Checks whether the dashboard user making the current request is a configured bot owner.
    ///     Used to restrict operations on the single, instance-wide Twitch bot account.
    /// </summary>
    private async Task<bool> IsCallerBotOwnerAsync()
    {
        var userId = await GetDashboardUserIdAsync();
        return userId.HasValue && credentials.OwnerIds.Contains(userId.Value);
    }

    private async Task<ulong?> GetDashboardUserIdAsync()
    {
        if (!Request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return null;

        var result = await HttpContext.AuthenticateAsync(DashJwtConstants.SchemeName);
        if (result.Succeeded &&
            ulong.TryParse(result.Principal?.FindFirst(DashJwtConstants.UserIdClaim)?.Value, out var userId))
            return userId;

        return null;
    }

    private static bool TryParseState(string state, out ulong guildId, out string mode)
    {
        guildId = 0;
        mode = "";
        var parts = state.Split(':', 3);
        if (parts.Length != 3 || !ulong.TryParse(parts[0], out guildId))
            return false;

        mode = NormalizeMode(parts[1]);
        return mode is TwitchOAuthModes.Bot or TwitchOAuthModes.Channel;
    }

    private static string NormalizeMode(string mode)
    {
        return mode.Trim().ToLowerInvariant();
    }
}