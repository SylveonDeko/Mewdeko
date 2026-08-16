using System.IO;
using Discord.Net;
using Mewdeko.AuthHandlers;
using Mewdeko.Controllers.Common.AuditLog;
using Mewdeko.Controllers.Common.DashboardAccess;
using Mewdeko.Controllers.Common.Embeds;
using Mewdeko.Database.Enums;
using Mewdeko.Modules.Utility.Services;
using Mewdeko.Services.Impl;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Embed = DataModel.Embed;
using EmbedWebhookPersona = DataModel.EmbedWebhookPersona;
using Image = Discord.Image;

namespace Mewdeko.Controllers;

/// <summary>
///     Controller for managing saved embed templates (personal and guild-shared). Since a single route can
///     resolve to either a personal or a guild-shared embed (and the guild ID for mutations often lives in
///     the request body rather than the route), this controller is exempt from the generic
///     <see cref="DashboardAccessEnforcementFilter" /> and enforces guild-shared access itself.
/// </summary>
[ApiController]
[Route("botapi/[controller]")]
[Authorize("ApiKeyPolicy")]
[SkipDashboardAccess]
public class EmbedsController(
    EmbedService embedService,
    EmbedPersonaService personaService,
    CdnStorageService cdn,
    IBotCredentials creds,
    IDashboardAuditContext auditContext,
    DiscordShardedClient client,
    DashboardAccessService dashboardAccessService) : Controller
{
    private const string Section = "Embeds";

    /// <summary>
    ///     The name given to webhooks the embed builder creates so they can be reused instead of
    ///     accumulating one webhook per send.
    /// </summary>
    private const string WebhookName = "Mewdeko Embed Builder";

    /// <summary>
    ///     The largest uploaded webhook avatar accepted, matching Discord's own upload ceiling.
    /// </summary>
    private const int MaxAvatarBytes = 8 * 1024 * 1024;

    /// <summary>
    ///     Discord's own limit on a webhook display name.
    /// </summary>
    private const int MaxPersonaNameLength = 80;

    /// <summary>
    ///     Discord's per-channel webhook cap, checked before creating a persona's webhook so the failure is
    ///     explained rather than surfacing as a raw API error.
    /// </summary>
    private const int MaxWebhooksPerChannel = 15;

    /// <summary>
    ///     The CDN folder uploaded avatars are published into for the dashboard to show as thumbnails.
    ///     Discord never reads these; it receives the image by upload onto the persona's webhook.
    /// </summary>
    private const string AvatarCdnFolder = "embedbuilder";

    /// <summary>
    ///     Gets all personal embed templates saved by a user.
    /// </summary>
    /// <param name="userId">The Discord user ID.</param>
    /// <returns>A list of the user's personal embed templates.</returns>
    [HttpGet("user/{userId}")]
    public async Task<IActionResult> GetUserEmbeds(ulong userId)
    {
        var embeds = await embedService.GetUserEmbedsAsync(userId);
        return Ok(embeds.Select(ToResponse));
    }

    /// <summary>
    ///     Gets all guild-shared embed templates for a guild.
    /// </summary>
    /// <param name="guildId">The Discord guild ID.</param>
    /// <returns>A list of the guild's shared embed templates.</returns>
    [HttpGet("guild/{guildId}")]
    public async Task<IActionResult> GetGuildEmbeds(ulong guildId)
    {
        if (!await HasGuildSectionAccessAsync(guildId, DashboardAccessLevel.View))
            return Forbid();

        var embeds = await embedService.GetGuildEmbedsAsync(guildId);
        return Ok(embeds.Select(ToResponse));
    }

    /// <summary>
    ///     Gets a single embed template by its ID.
    /// </summary>
    /// <param name="id">The embed template's database ID.</param>
    /// <returns>The embed template, if found.</returns>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetEmbed(int id)
    {
        var embed = await embedService.GetEmbedByIdAsync(id);
        if (embed == null)
            return NotFound("Embed template not found");

        if (embed.GuildId.HasValue && !await HasGuildSectionAccessAsync(embed.GuildId.Value, DashboardAccessLevel.View))
            return Forbid();

        return Ok(ToResponse(embed));
    }

    /// <summary>
    ///     Creates a new personal or guild-shared embed template.
    /// </summary>
    /// <param name="request">The embed template to create.</param>
    /// <returns>The created embed template.</returns>
    [HttpPost]
    public async Task<IActionResult> CreateEmbed([FromBody] CreateEmbedRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.EmbedName))
            return BadRequest("Embed name is required");

        if (string.IsNullOrWhiteSpace(request.JsonCode))
            return BadRequest("Embed JSON is required");

        if (!SmartEmbed.TryParse(request.JsonCode, request.GuildId, out _, out _, out _))
            return BadRequest("Invalid embed JSON");

        if (request.IsGuildShared)
        {
            if (!request.GuildId.HasValue)
                return BadRequest("A guild ID is required for guild-shared embeds");

            if (!await HasGuildSectionAccessAsync(request.GuildId.Value, DashboardAccessLevel.Manage))
                return Forbid();

            if (await embedService.GuildEmbedExistsAsync(request.GuildId.Value, request.EmbedName))
                return BadRequest($"A guild embed named '{request.EmbedName}' already exists");

            var created = await embedService.CreateGuildEmbedAsync(request.GuildId.Value, request.UserId,
                request.EmbedName, request.JsonCode);

            auditContext.RecordAfter(created);
            return Ok(ToResponse(created));
        }
        else
        {
            if (await embedService.UserEmbedExistsAsync(request.UserId, request.EmbedName))
                return BadRequest($"A personal embed named '{request.EmbedName}' already exists");

            var created = await embedService.CreateUserEmbedAsync(request.UserId, request.EmbedName,
                request.JsonCode);

            auditContext.RecordAfter(created);
            return Ok(ToResponse(created));
        }
    }

    /// <summary>
    ///     Updates an existing embed template's name and/or JSON.
    /// </summary>
    /// <param name="id">The embed template's database ID.</param>
    /// <param name="request">The fields to update.</param>
    /// <returns>The updated embed template.</returns>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateEmbed(int id, [FromBody] UpdateEmbedRequest request)
    {
        var existing = await embedService.GetEmbedByIdAsync(id);
        if (existing == null)
            return NotFound("Embed template not found");

        if (!await CanModifyAsync(existing, request.UserId))
            return Forbid();

        if (request.JsonCode != null &&
            !SmartEmbed.TryParse(request.JsonCode, existing.GuildId, out _, out _, out _))
            return BadRequest("Invalid embed JSON");

        auditContext.RecordBefore(existing);
        var updated = await embedService.UpdateEmbedAsync(id, request.EmbedName, request.JsonCode);
        if (updated == null)
            return NotFound("Embed template not found");

        auditContext.RecordAfter(updated);
        return Ok(ToResponse(updated));
    }

    /// <summary>
    ///     Deletes an embed template.
    /// </summary>
    /// <param name="id">The embed template's database ID.</param>
    /// <param name="userId">The ID of the user requesting the deletion, used for ownership verification.</param>
    /// <returns>Success or failure response.</returns>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteEmbed(int id, [FromQuery] ulong userId)
    {
        var existing = await embedService.GetEmbedByIdAsync(id);
        if (existing == null)
            return NotFound("Embed template not found");

        if (!await CanModifyAsync(existing, userId))
            return Forbid();

        auditContext.RecordBefore(existing);
        var success = await embedService.DeleteEmbedByIdAsync(id);

        if (success)
            return Ok("Embed template deleted successfully");
        return BadRequest("Failed to delete embed template");
    }

    /// <summary>
    ///     Lists the personal "send as" personas belonging to a user.
    /// </summary>
    /// <param name="userId">The Discord user ID.</param>
    /// <returns>The user's personal personas.</returns>
    [HttpGet("personas/user/{userId}")]
    public async Task<IActionResult> GetUserPersonas(ulong userId)
    {
        var personas = await personaService.GetUserPersonasAsync(userId);
        return Ok(personas.Select(ToResponse));
    }

    /// <summary>
    ///     Lists the "send as" personas shared with a guild.
    /// </summary>
    /// <param name="guildId">The Discord guild ID.</param>
    /// <returns>The guild's shared personas.</returns>
    [HttpGet("personas/guild/{guildId}")]
    public async Task<IActionResult> GetGuildPersonas(ulong guildId)
    {
        if (!await HasGuildSectionAccessAsync(guildId, DashboardAccessLevel.View))
            return Forbid();

        var personas = await personaService.GetGuildPersonasAsync(guildId);
        return Ok(personas.Select(ToResponse));
    }

    /// <summary>
    ///     Serves a persona's uploaded avatar bytes. Instances without a disk-backed CDN publish avatars
    ///     through the dashboard, which fetches them here and re-serves them on its own public origin so
    ///     Discord can reach them. Unauthenticated in effect, since the dashboard proxies it for anyone,
    ///     but an avatar is public by nature once it has been posted.
    /// </summary>
    /// <param name="id">The persona's database ID.</param>
    /// <returns>The raw image bytes, or 404 when the persona has no uploaded avatar.</returns>
    [HttpGet("personas/{id:int}/avatar")]
    [SkipAudit]
    public async Task<IActionResult> GetPersonaAvatar(int id)
    {
        var persona = await personaService.GetByIdAsync(id);
        if (persona?.AvatarData is not { Length: > 0 })
            return NotFound();

        return File(persona.AvatarData, "application/octet-stream");
    }

    /// <summary>
    ///     Creates a personal or guild-shared "send as" persona.
    /// </summary>
    /// <param name="request">The persona to create.</param>
    /// <returns>The created persona.</returns>
    [HttpPost("personas")]
    public async Task<IActionResult> CreatePersona([FromBody] CreateEmbedPersonaRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest("A persona name is required");

        var name = request.Name.Trim();
        if (name.Length > MaxPersonaNameLength)
            return BadRequest($"A persona name must be {MaxPersonaNameLength} characters or fewer");

        byte[]? avatarBytes = null;
        var avatarExtension = "png";
        if (!string.IsNullOrWhiteSpace(request.AvatarData))
        {
            if (!TryDecodeAvatar(request.AvatarData, out avatarBytes, out avatarExtension, out var avatarProblem))
                return BadRequest(avatarProblem);
        }

        ulong? guildId = null;
        if (request.IsGuildShared)
        {
            if (!request.GuildId.HasValue)
                return BadRequest("A guild ID is required for guild-shared personas");

            if (!await HasGuildSectionAccessAsync(request.GuildId.Value, DashboardAccessLevel.Manage))
                return Forbid();

            guildId = request.GuildId.Value;
        }

        if (await personaService.NameExistsAsync(request.UserId, guildId, name))
            return BadRequest($"A persona named '{name}' already exists");

        var avatarUrl = avatarBytes == null && !string.IsNullOrWhiteSpace(request.AvatarUrl)
            ? request.AvatarUrl.Trim()
            : null;

        var created = await personaService.CreateAsync(request.UserId, guildId, name, avatarUrl, avatarBytes);

        // Publishing happens after the insert because the file name is keyed by the persona's ID.
        if (avatarBytes != null)
        {
            var published = await PublishPreviewAsync(created.Id, created.AvatarVersion, avatarExtension,
                avatarBytes);
            if (published != null)
            {
                await personaService.SetAvatarUrlAsync(created.Id, published);
                created.AvatarUrl = published;
            }
        }

        auditContext.RecordAfter(ToAuditSnapshot(created));
        return Ok(ToResponse(created));
    }

    /// <summary>
    ///     Updates a "send as" persona's name or avatar.
    /// </summary>
    /// <param name="id">The persona's database ID.</param>
    /// <param name="request">The fields to update.</param>
    /// <returns>The updated persona.</returns>
    [HttpPut("personas/{id:int}")]
    public async Task<IActionResult> UpdatePersona(int id, [FromBody] UpdateEmbedPersonaRequest request)
    {
        var existing = await personaService.GetByIdAsync(id);
        if (existing == null)
            return NotFound("Persona not found");

        if (!await CanModifyPersonaAsync(existing, request.UserId))
            return Forbid();

        string? name = null;
        if (request.Name != null)
        {
            name = request.Name.Trim();
            if (string.IsNullOrWhiteSpace(name))
                return BadRequest("A persona name is required");

            if (name.Length > MaxPersonaNameLength)
                return BadRequest($"A persona name must be {MaxPersonaNameLength} characters or fewer");

            if (await personaService.NameExistsAsync(existing.UserId, existing.GuildId, name, id))
                return BadRequest($"A persona named '{name}' already exists");
        }

        byte[]? avatarBytes = null;
        var avatarExtension = "png";
        if (!string.IsNullOrWhiteSpace(request.AvatarData))
        {
            if (!TryDecodeAvatar(request.AvatarData, out avatarBytes, out avatarExtension, out var avatarProblem))
                return BadRequest(avatarProblem);
        }

        auditContext.RecordBefore(ToAuditSnapshot(existing));
        var requestedUrl = string.IsNullOrWhiteSpace(request.AvatarUrl) ? null : request.AvatarUrl.Trim();
        var updated = await personaService.UpdateAsync(id, name, requestedUrl, avatarBytes, request.ClearAvatar);
        if (updated == null)
            return NotFound("Persona not found");

        // Replacing or clearing an avatar bumps the version, so the old CDN file is now orphaned.
        if (updated.AvatarVersion != existing.AvatarVersion)
            await cdn.DeleteByPrefixAsync(AvatarCdnFolder, PersonaAvatarPrefix(id));

        if (avatarBytes != null)
        {
            var published = await PublishPreviewAsync(id, updated.AvatarVersion, avatarExtension, avatarBytes);
            if (published != null)
            {
                await personaService.SetAvatarUrlAsync(id, published);
                updated.AvatarUrl = published;
            }
        }

        auditContext.RecordAfter(ToAuditSnapshot(updated));
        return Ok(ToResponse(updated));
    }

    /// <summary>
    ///     Deletes a "send as" persona. The webhooks it created in individual channels are left in place,
    ///     since removing them needs a call per channel and Discord reuses them harmlessly.
    /// </summary>
    /// <param name="id">The persona's database ID.</param>
    /// <param name="userId">The ID of the user requesting the deletion, used for ownership verification.</param>
    /// <returns>Success or failure response.</returns>
    [HttpDelete("personas/{id:int}")]
    public async Task<IActionResult> DeletePersona(int id, [FromQuery] ulong userId)
    {
        var existing = await personaService.GetByIdAsync(id);
        if (existing == null)
            return NotFound("Persona not found");

        if (!await CanModifyPersonaAsync(existing, userId))
            return Forbid();

        auditContext.RecordBefore(ToAuditSnapshot(existing));
        var success = await personaService.DeleteAsync(id);

        if (!success)
            return BadRequest("Failed to delete persona");

        await cdn.DeleteByPrefixAsync(AvatarCdnFolder, PersonaAvatarPrefix(id));
        return Ok("Persona deleted successfully");
    }

    /// <summary>
    ///     Lists the channels of a guild the requesting user can actually see, along with the effective
    ///     permissions both the user and the bot hold in each one. A user granted dashboard access without
    ///     matching Discord permissions therefore cannot discover or post to channels they cannot see.
    /// </summary>
    /// <param name="guildId">The guild to list channels for.</param>
    /// <param name="userId">
    ///     The user the permissions should be resolved for. Ignored when the request carries a dashboard
    ///     JWT, whose verified identity always wins.
    /// </param>
    /// <returns>The visible channels and their permission flags.</returns>
    [HttpGet("channels/{guildId}")]
    public async Task<IActionResult> GetSendableChannels(ulong guildId, [FromQuery] ulong userId)
    {
        if (!await HasGuildSectionAccessAsync(guildId, DashboardAccessLevel.View))
            return Forbid();

        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound("Guild not found");

        var actingUserId = await GetDashboardUserIdAsync() ?? userId;
        var user = guild.GetUser(actingUserId);
        if (user == null)
            return Forbid();

        var bot = guild.CurrentUser;

        var channels = guild.Channels
            .OfType<ITextChannel>()
            .Concat(guild.ThreadChannels)
            .DistinctBy(channel => channel.Id)
            .Select(channel => BuildChannelResponse(guild, user, bot, channel))
            .Where(response => response != null)
            .Select(response => response!)
            .OrderBy(response => response.CategoryName ?? "")
            .ThenBy(response => response.Position)
            .ThenBy(response => response.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Ok(channels);
    }

    /// <summary>
    ///     Sends a message built in the embed builder to a guild channel, either as the bot or through a
    ///     channel webhook. Both the requesting user and the bot must hold the Discord permissions the send
    ///     needs in that specific channel; dashboard access alone is not enough.
    /// </summary>
    /// <param name="guildId">The guild to send in.</param>
    /// <param name="request">The channel, payload, and delivery options.</param>
    /// <returns>Details of the sent message, including a jump link.</returns>
    [HttpPost("send/{guildId}")]
    public async Task<IActionResult> SendEmbed(ulong guildId, [FromBody] SendEmbedRequest request)
    {
        if (!await HasGuildSectionAccessAsync(guildId, DashboardAccessLevel.Manage))
            return Forbid();

        if (string.IsNullOrWhiteSpace(request.JsonCode))
            return BadRequest("Nothing to send");

        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound("Guild not found");

        var actingUserId = await GetDashboardUserIdAsync() ?? request.UserId;
        var user = guild.GetUser(actingUserId);
        if (user == null)
            return Forbid();

        var channel = guild.GetChannel(request.ChannelId) as ITextChannel
                      ?? guild.ThreadChannels.FirstOrDefault(thread => thread.Id == request.ChannelId);
        if (channel == null)
            return NotFound("Channel not found");

        var bot = guild.CurrentUser;
        var userPerms = user.GetPermissions(channel);
        var botPerms = bot.GetPermissions(channel);
        var isThread = channel is IThreadChannel;

        // Anything that isn't a recognised embed payload is sent as plain message content, matching how
        // the rest of the bot treats embed strings.
        if (!SmartEmbed.TryParse(request.JsonCode, guildId, out var embeds, out var plainText, out var components))
        {
            embeds = null;
            components = null;
            plainText = request.JsonCode;
        }

        if (string.IsNullOrWhiteSpace(plainText) && (embeds == null || embeds.Length == 0) &&
            components?.ActionRows is not { Count: > 0 })
            return BadRequest("Nothing to send");

        var userProblem = DescribeSendRestriction(userPerms, isThread, embeds?.Length > 0, request.UseWebhook, "You");
        if (userProblem != null)
            return StatusCode(403, userProblem);

        var botProblem = DescribeSendRestriction(botPerms, isThread, embeds?.Length > 0, request.UseWebhook, "The bot");
        if (botProblem != null)
            return BadRequest(botProblem);

        // A user without Mention Everyone in the target channel must not be able to smuggle an @everyone,
        // @here, or role ping through the builder, so those mention types are stripped rather than rejected.
        var mentionsSuppressed = !userPerms.MentionEveryone;
        var allowedMentions = mentionsSuppressed
            ? new AllowedMentions
            {
                AllowedTypes = AllowedMentionTypes.Users
            }
            : AllowedMentions.All;

        EmbedWebhookPersona? persona = null;
        if (request.UseWebhook && request.PersonaId.HasValue)
        {
            persona = await personaService.GetByIdAsync(request.PersonaId.Value);
            if (persona == null)
                return NotFound("Persona not found");

            if (!await CanUsePersonaAsync(persona, actingUserId, guildId))
                return Forbid();
        }

        // A persona whose avatar was uploaded carries the image on its own webhook, since Discord's
        // per-message avatar override only takes a URL and a self-hosted instance may have no way to publish
        // one. Everything else rides the shared webhook with a per-message avatar URL.
        var uploadedAvatarPersona = persona?.AvatarData is { Length: > 0 } ? persona : null;
        var avatarUrl = uploadedAvatarPersona != null ? null : persona?.AvatarUrl;
        if (persona == null && request.UseWebhook && !string.IsNullOrWhiteSpace(request.WebhookAvatarUrl))
            avatarUrl = request.WebhookAvatarUrl.Trim();

        var webhookUsername = persona?.Name
                              ?? (string.IsNullOrWhiteSpace(request.WebhookUsername)
                                  ? null
                                  : request.WebhookUsername.Trim());

        ulong messageId;
        try
        {
            messageId = request.UseWebhook
                ? await SendViaWebhookAsync(channel, plainText, embeds, components, allowedMentions,
                    webhookUsername, avatarUrl, uploadedAvatarPersona)
                : await SendAsBotAsync(channel, plainText, embeds, components, allowedMentions);
        }
        catch (WebhookLimitReachedException)
        {
            return BadRequest(
                "That channel already holds Discord's maximum of 15 webhooks, so this persona's avatar " +
                "could not be set up there. Delete an unused webhook, or give the persona an avatar URL " +
                "instead of an uploaded image.");
        }
        catch (HttpException ex)
        {
            return BadRequest($"Discord rejected the message: {ex.Reason ?? ex.Message}");
        }

        var response = new SendEmbedResponse
        {
            MessageId = messageId,
            ChannelId = channel.Id,
            ChannelName = channel.Name,
            MessageLink = $"https://discord.com/channels/{guildId}/{channel.Id}/{messageId}",
            SentViaWebhook = request.UseWebhook,
            WebhookUsername = request.UseWebhook ? webhookUsername : null,
            PersonaName = persona?.Name,
            MentionsSuppressed = mentionsSuppressed
        };

        // Recording an empty before pairs with the after snapshot so the audit entry stores the send
        // summary (who, where, how) rather than the raw embed JSON the user submitted.
        auditContext.RecordBefore(null);
        auditContext.RecordAfter(new
        {
            GuildId = guildId,
            Action = "Sent a message from the embed builder",
            ChannelId = channel.Id,
            ChannelName = channel.Name,
            SentByUserId = actingUserId,
            SentByUser = $"{user.Username} ({actingUserId})",
            DeliveredVia = request.UseWebhook ? "Webhook" : "Bot",
            WebhookUsername = request.UseWebhook ? webhookUsername : null,
            PersonaId = persona?.Id,
            PersonaName = persona?.Name,
            AvatarUrl = avatarUrl,
            EmbedCount = embeds?.Length ?? 0,
            HasContent = !string.IsNullOrWhiteSpace(plainText),
            ContentPreview = Preview(plainText),
            MentionsSuppressed = mentionsSuppressed,
            MessageId = messageId,
            response.MessageLink
        });

        return Ok(response);
    }

    /// <summary>
    ///     Sends the message as the bot itself.
    /// </summary>
    private static async Task<ulong> SendAsBotAsync(
        ITextChannel channel,
        string? plainText,
        Discord.Embed[]? embeds,
        ComponentBuilder? components,
        AllowedMentions allowedMentions)
    {
        var message = await channel.SendMessageAsync(plainText, embeds: embeds, allowedMentions: allowedMentions,
            components: components?.Build());
        return message.Id;
    }

    /// <summary>
    ///     Sends the message through the builder's webhook on the target channel, creating it if it does not
    ///     exist yet. Both the display name and the avatar are per-message overrides, so a single webhook
    ///     serves every persona and the webhook itself is never modified. Threads have no webhooks of their
    ///     own, so the parent channel's webhook is used with the thread as the target.
    /// </summary>
    /// <param name="channel">The channel or thread to post in.</param>
    /// <param name="plainText">The message content, or null for an embed only message.</param>
    /// <param name="embeds">The embeds to attach, if any.</param>
    /// <param name="components">The components to attach, if any.</param>
    /// <param name="allowedMentions">Which mentions the message is allowed to ping.</param>
    /// <param name="username">The display name to post under, or null to use the webhook's own name.</param>
    /// <param name="avatarUrl">The avatar to post with, or null to use the webhook's own avatar.</param>
    /// <param name="uploadedAvatarPersona">The persona whose uploaded avatar should be used, if any.</param>
    private static async Task<ulong> SendViaWebhookAsync(
        ITextChannel channel,
        string? plainText,
        Discord.Embed[]? embeds,
        ComponentBuilder? components,
        AllowedMentions allowedMentions,
        string? username,
        string? avatarUrl,
        EmbedWebhookPersona? uploadedAvatarPersona)
    {
        ulong? threadId = null;
        IChannel webhookChannel = channel;

        if (channel is SocketThreadChannel thread)
        {
            threadId = thread.Id;
            webhookChannel = thread.ParentChannel ??
                             throw new InvalidOperationException("The thread's parent channel could not be resolved.");
        }

        if (webhookChannel is not IIntegrationChannel integrationChannel)
            throw new InvalidOperationException("That channel does not support webhooks.");

        var webhooks = await integrationChannel.GetWebhooksAsync();

        var webhook = uploadedAvatarPersona == null
            ? await ResolveSharedWebhookAsync(integrationChannel, webhooks)
            : await ResolvePersonaWebhookAsync(integrationChannel, webhooks, uploadedAvatarPersona);

        using var webhookClient = new DiscordWebhookClient(webhook);

        return await webhookClient.SendMessageAsync(plainText, embeds: embeds, username: username,
            avatarUrl: avatarUrl, allowedMentions: allowedMentions, components: components?.Build(),
            threadId: threadId);
    }

    /// <summary>
    ///     The channel's general-purpose builder webhook, which is never modified. Personas whose avatar is
    ///     a URL, and one-off sends, ride on this one using per-message name and avatar overrides.
    /// </summary>
    private static async Task<IWebhook> ResolveSharedWebhookAsync(
        IIntegrationChannel channel, IReadOnlyCollection<IWebhook> webhooks)
    {
        return webhooks.FirstOrDefault(hook => hook.Name == WebhookName && hook.Token != null)
               ?? await channel.CreateWebhookAsync(WebhookName);
    }

    /// <summary>
    ///     The webhook that carries an uploaded avatar for one persona in one channel.
    ///     Discord's per-message avatar override only takes a URL, which a self-hosted instance may have no
    ///     way to publish. Uploading the image onto a webhook goes through the API instead and needs no
    ///     public address at all, so this is the path that works on every deployment. The cost is one
    ///     webhook per persona per channel, created lazily the first time that persona posts there.
    ///     The name carries the persona's ID and avatar version rather than its display name, so renaming a
    ///     persona costs nothing and replacing its avatar refreshes the webhook exactly once.
    /// </summary>
    private static async Task<IWebhook> ResolvePersonaWebhookAsync(
        IIntegrationChannel channel, IReadOnlyCollection<IWebhook> webhooks, EmbedWebhookPersona persona)
    {
        var targetName = PersonaWebhookName(persona);

        var current = webhooks.FirstOrDefault(hook => hook.Name == targetName && hook.Token != null);
        if (current != null)
            return current;

        // An earlier version of this persona's webhook, left behind by an avatar change.
        var stale = webhooks.FirstOrDefault(hook =>
            hook.Token != null && hook.Name.StartsWith(PersonaWebhookPrefix(persona), StringComparison.Ordinal));

        if (stale != null)
        {
            await ApplyAvatarAsync(stale, persona.AvatarData, targetName);
            return stale;
        }

        if (webhooks.Count >= MaxWebhooksPerChannel)
            throw new WebhookLimitReachedException();

        var created = await channel.CreateWebhookAsync(targetName);
        await ApplyAvatarAsync(created, persona.AvatarData, null);
        return created;
    }

    /// <summary>
    ///     Uploads an avatar onto a webhook, optionally renaming it at the same time so its stored name keeps
    ///     tracking the persona's current avatar version.
    /// </summary>
    private static async Task ApplyAvatarAsync(IWebhook webhook, byte[]? avatarBytes, string? newName)
    {
        if (avatarBytes == null || avatarBytes.Length == 0)
        {
            if (newName != null)
                await webhook.ModifyAsync(properties => properties.Name = newName);
            return;
        }

        using var avatarStream = new MemoryStream(avatarBytes);
        await webhook.ModifyAsync(properties =>
        {
            properties.Image = new Image(avatarStream);
            if (newName != null)
                properties.Name = newName;
        });
    }

    /// <summary>
    ///     The stable prefix identifying a persona's webhooks in a channel, independent of its avatar version.
    /// </summary>
    private static string PersonaWebhookPrefix(EmbedWebhookPersona persona)
    {
        return $"Mewdeko Persona #{persona.Id} ";
    }

    /// <summary>
    ///     The webhook name for a persona at its current avatar version.
    /// </summary>
    private static string PersonaWebhookName(EmbedWebhookPersona persona)
    {
        return $"{PersonaWebhookPrefix(persona)}v{persona.AvatarVersion}";
    }

    /// <summary>
    ///     Gives a persona's uploaded avatar a URL the dashboard can show as a thumbnail. This is a
    ///     convenience only: Discord receives the image by upload, not by URL, so a null here costs nothing
    ///     but a preview and must never fail the request. Instances with a disk-backed CDN get a file served
    ///     straight from it; everyone else falls back to the dashboard serving the bytes from the database,
    ///     which the user's own browser can always reach. The version is part of the URL so a replaced
    ///     avatar is never shown stale.
    /// </summary>
    private async Task<string?> PublishPreviewAsync(int personaId, int version, string extension, byte[] bytes)
    {
        var fileName = $"{PersonaAvatarPrefix(personaId)}v{version}.{extension}";

        // A configured CDN can still fail to accept the write, so this falls through rather than failing
        // the whole request. The bytes are in the database either way.
        if (cdn.IsConfigured)
        {
            var saved = await cdn.SaveAsync(AvatarCdnFolder, fileName, bytes);
            if (saved != null)
                return saved;
        }

        if (!string.IsNullOrWhiteSpace(creds.DashboardUrl))
            return $"{creds.DashboardUrl.TrimEnd('/')}/cdn/persona/{personaId}/v{version}.{extension}";

        return null;
    }

    /// <summary>
    ///     The CDN file name prefix for a persona's avatars, matching every version of them.
    /// </summary>
    private static string PersonaAvatarPrefix(int personaId)
    {
        return $"persona-{personaId}-";
    }

    /// <summary>
    ///     Describes the first permission the given channel permissions are missing for this send, or null
    ///     when the send is allowed.
    /// </summary>
    /// <param name="perms">The effective channel permissions of the user or bot.</param>
    /// <param name="isThread">Whether the target channel is a thread, which uses a different send permission.</param>
    /// <param name="hasEmbeds">Whether the message carries embeds, which need Embed Links.</param>
    /// <param name="useWebhook">Whether the message is delivered by webhook, which needs Manage Webhooks.</param>
    /// <param name="subject">How to refer to whoever is missing the permission, for the message.</param>
    private static string? DescribeSendRestriction(
        ChannelPermissions perms, bool isThread, bool hasEmbeds, bool useWebhook, string subject)
    {
        if (!perms.ViewChannel)
            return $"{subject} cannot see that channel.";

        if (isThread ? !perms.SendMessagesInThreads : !perms.SendMessages)
            return $"{subject} cannot send messages in that channel.";

        if (hasEmbeds && !perms.EmbedLinks)
            return $"{subject} cannot send embeds in that channel.";

        if (useWebhook && !perms.ManageWebhooks)
            return $"{subject} cannot manage webhooks in that channel.";

        return null;
    }

    /// <summary>
    ///     Builds the permission summary for a single channel, or null when the user cannot see it at all.
    /// </summary>
    private static SendableChannelResponse? BuildChannelResponse(
        SocketGuild guild, SocketGuildUser user, SocketGuildUser bot, ITextChannel channel)
    {
        var userPerms = user.GetPermissions(channel);
        if (!userPerms.ViewChannel)
            return null;

        var botPerms = bot.GetPermissions(channel);
        var isThread = channel is IThreadChannel;
        var canSend = isThread ? userPerms.SendMessagesInThreads : userPerms.SendMessages;
        var botCanSend = isThread ? botPerms.SendMessagesInThreads : botPerms.SendMessages;

        var categoryId = channel.CategoryId;
        var categoryName = categoryId.HasValue
            ? guild.GetCategoryChannel(categoryId.Value)?.Name
            : null;

        // Only hard blocks belong here. Missing Embed Links is surfaced through the permission flags
        // instead, because it stops embeds but not a plain-text message.
        string? restriction = null;
        if (!canSend)
            restriction = "You cannot send messages here";
        else if (!botPerms.ViewChannel)
            restriction = "The bot cannot see this channel";
        else if (!botCanSend)
            restriction = "The bot cannot send messages here";

        return new SendableChannelResponse
        {
            Id = channel.Id,
            Name = channel.Name,
            CategoryId = isThread ? null : categoryId,
            CategoryName = isThread ? "Threads" : categoryName,
            Position = channel.Position,
            IsThread = isThread,
            IsAnnouncement = channel is INewsChannel,
            CanSend = canSend,
            CanEmbed = userPerms.EmbedLinks,
            CanMentionEveryone = userPerms.MentionEveryone,
            CanUseWebhooks = userPerms.ManageWebhooks,
            BotCanSend = botPerms.ViewChannel && botCanSend,
            BotCanEmbed = botPerms.EmbedLinks,
            BotCanUseWebhooks = botPerms.ManageWebhooks,
            Restriction = restriction
        };
    }

    /// <summary>
    ///     Decodes an uploaded webhook avatar from raw base64 or a <c>data:</c> URI, rejecting anything that
    ///     is not a plausible image so a bad payload fails here rather than at Discord.
    /// </summary>
    /// <param name="data">The submitted avatar payload.</param>
    /// <param name="bytes">The decoded image bytes, when decoding succeeds.</param>
    /// <param name="extension">The file extension implied by the payload's media type.</param>
    /// <param name="problem">A message describing why the payload was rejected, when it was.</param>
    private static bool TryDecodeAvatar(string data, out byte[]? bytes, out string extension, out string? problem)
    {
        bytes = null;
        extension = "png";
        problem = null;

        var payload = data.Trim();
        if (payload.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            var comma = payload.IndexOf(',');
            if (comma < 0)
            {
                problem = "The avatar image could not be read.";
                return false;
            }

            var header = payload[..comma];
            if (!header.Contains("image/", StringComparison.OrdinalIgnoreCase))
            {
                problem = "The avatar must be an image.";
                return false;
            }

            extension = ExtensionFor(header);
            payload = payload[(comma + 1)..];
        }

        try
        {
            bytes = Convert.FromBase64String(payload);
        }
        catch (FormatException)
        {
            problem = "The avatar image could not be read.";
            return false;
        }

        if (bytes.Length == 0)
        {
            problem = "The avatar image is empty.";
            return false;
        }

        if (bytes.Length > MaxAvatarBytes)
        {
            problem = "The avatar image must be 8MB or smaller.";
            return false;
        }

        return true;
    }

    /// <summary>
    ///     Maps a data URI's media type onto a file extension, so the file the CDN serves carries the
    ///     right one. Anything unrecognised falls back to png, which browsers and Discord sniff anyway.
    /// </summary>
    private static string ExtensionFor(string header)
    {
        if (header.Contains("image/gif", StringComparison.OrdinalIgnoreCase)) return "gif";
        if (header.Contains("image/webp", StringComparison.OrdinalIgnoreCase)) return "webp";
        if (header.Contains("image/jpeg", StringComparison.OrdinalIgnoreCase) ||
            header.Contains("image/jpg", StringComparison.OrdinalIgnoreCase)) return "jpg";
        return "png";
    }

    /// <summary>
    ///     Trims message content down to a short excerpt for the audit log.
    /// </summary>
    private static string? Preview(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return null;

        var trimmed = content.Trim();
        return trimmed.Length <= 200 ? trimmed : $"{trimmed[..200]}...";
    }

    /// <summary>
    ///     Personal personas may only be modified by their owner. Guild-shared ones require Manage-level
    ///     dashboard access to the Embeds section for that guild.
    /// </summary>
    private async Task<bool> CanModifyPersonaAsync(EmbedWebhookPersona persona, ulong requestingUserId)
    {
        if (persona.GuildId.HasValue)
            return await HasGuildSectionAccessAsync(persona.GuildId.Value, DashboardAccessLevel.Manage);

        return persona.UserId == requestingUserId;
    }

    /// <summary>
    ///     A persona may be sent as when it belongs to the requesting user, or when it is shared with the
    ///     guild being sent in. A guild's shared persona cannot be borrowed to post in a different guild.
    /// </summary>
    private static async Task<bool> CanUsePersonaAsync(
        EmbedWebhookPersona persona, ulong requestingUserId, ulong guildId)
    {
        await Task.CompletedTask;

        if (persona.GuildId.HasValue)
            return persona.GuildId.Value == guildId;

        return persona.UserId == requestingUserId;
    }

    /// <summary>
    ///     Personal embeds may only be modified by their owner. Guild-shared embeds require Manage-level
    ///     dashboard access to the Embeds section for that guild.
    /// </summary>
    private async Task<bool> CanModifyAsync(Embed embed, ulong requestingUserId)
    {
        if (embed.GuildId.HasValue)
            return await HasGuildSectionAccessAsync(embed.GuildId.Value, DashboardAccessLevel.Manage);

        return embed.UserId == requestingUserId;
    }

    /// <summary>
    ///     Extracts the verified dashboard user ID from the request's dashboard JWT, if present. Returns
    ///     null for requests authenticated only by the shared API key (mobile/legacy callers), which retain
    ///     their existing unrestricted behavior.
    /// </summary>
    private async Task<ulong?> GetDashboardUserIdAsync()
    {
        if (!Request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return null;

        var authResult = await HttpContext.AuthenticateAsync(DashJwtConstants.SchemeName);
        if (!authResult.Succeeded ||
            !ulong.TryParse(authResult.Principal?.FindFirst(DashJwtConstants.UserIdClaim)?.Value, out var userId))
            return null;

        return userId;
    }

    /// <summary>
    ///     Whether the current dashboard user has at least the given access level to the Embeds section for a
    ///     guild. Guild owners and Administrator-permission holders always pass. Requests without a verified
    ///     dashboard identity are allowed through unchanged.
    /// </summary>
    private async Task<bool> HasGuildSectionAccessAsync(ulong guildId, DashboardAccessLevel required)
    {
        var userId = await GetDashboardUserIdAsync();
        if (userId == null)
            return true;

        var guild = client.GetGuild(guildId);
        var guildUser = guild?.GetUser(userId.Value);
        if (guild == null || guildUser == null)
            return false;

        if (guild.OwnerId == userId.Value || guildUser.GuildPermissions.Has(GuildPermission.Administrator))
            return true;

        var level = await dashboardAccessService.GetSectionAccessAsync(
            guildId, userId.Value, guildUser.Roles.Select(role => role.Id).ToList(), Section);
        return level >= required;
    }

    private static EmbedPersonaResponse ToResponse(EmbedWebhookPersona persona)
    {
        return new EmbedPersonaResponse
        {
            Id = persona.Id,
            Name = persona.Name,
            AvatarUrl = persona.AvatarUrl,
            HasUploadedAvatar = persona.AvatarData is { Length: > 0 },
            UserId = persona.UserId,
            GuildId = persona.GuildId,
            IsGuildShared = persona.IsGuildShared,
            DateAdded = persona.DateAdded
        };
    }

    /// <summary>
    ///     The audit view of a persona. The avatar bytes are deliberately excluded so a base64 image never
    ///     lands in the audit log.
    /// </summary>
    private static object ToAuditSnapshot(EmbedWebhookPersona persona)
    {
        return new
        {
            persona.Id,
            persona.GuildId,
            PersonaName = persona.Name,
            persona.AvatarUrl,
            HasUploadedAvatar = persona.AvatarData is { Length: > 0 },
            persona.AvatarVersion,
            persona.IsGuildShared
        };
    }

    private static EmbedResponse ToResponse(Embed embed)
    {
        return new EmbedResponse
        {
            Id = embed.Id,
            EmbedName = embed.EmbedName,
            JsonCode = embed.JsonCode,
            UserId = embed.UserId,
            DateAdded = embed.DateAdded,
            GuildId = embed.GuildId,
            IsGuildShared = embed.IsGuildShared
        };
    }
}

/// <summary>
///     Thrown when a channel already holds Discord's maximum number of webhooks, so a persona's webhook
///     cannot be created.
/// </summary>
public class WebhookLimitReachedException : Exception;