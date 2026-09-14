using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Common.TypeReaders;
using Mewdeko.Modules.Administration.Common;
using Mewdeko.Modules.Nsfw;
using Mewdeko.Modules.Server_Management.Services;
using Mewdeko.Modules.Utility.Common;
using Mewdeko.Modules.Utility.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     Controller for utility features that previously had no API surface:
///     command aliases, quotes, auto publish, stream roles, NSFW tag blacklist,
///     the AI assistant, and role monitoring.
/// </summary>
[ApiController]
[Route("botapi/[controller]/{guildId}")]
[Authorize("ApiKeyPolicy")]
public class UtilityController(
    DiscordShardedClient client,
    IDataConnectionFactory dbFactory,
    CommandMapService commandMapService,
    AutoPublishService autoPublishService,
    StreamRoleService streamRoleService,
    SearchImagesService searchImagesService,
    AiService aiService,
    RoleMonitorService roleMonitorService,
    IDashboardAuditContext auditContext,
    ILogger<UtilityController> logger) : Controller
{
    #region Command aliases

    /// <summary>
    ///     Lists command aliases for the guild
    /// </summary>
    [HttpGet("aliases")]
    public async Task<IActionResult> GetAliases(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var aliases = await db.CommandAliases
            .Where(a => a.GuildId == guildId)
            .OrderBy(a => a.Trigger)
            .Select(a => new
            {
                a.Id, a.Trigger, a.Mapping, a.DateAdded
            })
            .ToListAsync();
        return Ok(aliases);
    }

    /// <summary>
    ///     Adds or replaces a command alias
    /// </summary>
    [HttpPost("aliases")]
    public async Task<IActionResult> AddAlias(ulong guildId, [FromBody] AliasRequest request)
    {
        var trigger = request.Trigger?.Trim().ToLowerInvariant();
        var mapping = request.Mapping?.Trim();
        if (string.IsNullOrWhiteSpace(trigger) || string.IsNullOrWhiteSpace(mapping))
            return BadRequest("Both trigger and mapping are required");
        if (trigger.Contains(' '))
            return BadRequest("Trigger cannot contain spaces");

        await using var db = await dbFactory.CreateConnectionAsync();
        auditContext.RecordBefore(await commandMapService.GetCommandMap(guildId));
        await db.CommandAliases
            .Where(a => a.GuildId == guildId && a.Trigger == trigger)
            .DeleteAsync();
        await db.InsertAsync(new CommandAlias
        {
            GuildId = guildId, Trigger = trigger, Mapping = mapping, DateAdded = DateTime.UtcNow
        });
        auditContext.RecordAfter(await commandMapService.GetCommandMap(guildId));
        return Ok(new
        {
            trigger, mapping
        });
    }

    /// <summary>
    ///     Removes a command alias
    /// </summary>
    [HttpDelete("aliases/{trigger}")]
    public async Task<IActionResult> RemoveAlias(ulong guildId, string trigger)
    {
        trigger = trigger.Trim().ToLowerInvariant();
        await using var db = await dbFactory.CreateConnectionAsync();
        auditContext.RecordBefore(await commandMapService.GetCommandMap(guildId));
        var removed = await db.CommandAliases
            .Where(a => a.GuildId == guildId && a.Trigger == trigger)
            .DeleteAsync();
        if (removed == 0)
            return NotFound("Alias not found");
        auditContext.RecordAfter(await commandMapService.GetCommandMap(guildId));
        return Ok();
    }

    /// <summary>
    ///     Removes every command alias
    /// </summary>
    [HttpDelete("aliases")]
    public async Task<IActionResult> ClearAliases(ulong guildId)
    {
        auditContext.RecordBefore(await commandMapService.GetCommandMap(guildId));
        var count = await commandMapService.ClearAliases(guildId);
        return Ok(new
        {
            removed = count
        });
    }

    #endregion

    #region Quotes

    /// <summary>
    ///     Lists quotes for the guild
    /// </summary>
    [HttpGet("quotes")]
    public async Task<IActionResult> GetQuotes(ulong guildId, [FromQuery] string? search = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        await using var db = await dbFactory.CreateConnectionAsync();
        var query = db.Quotes.Where(q => q.GuildId == guildId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            var upper = term.ToUpperInvariant();
            query = query.Where(q => q.Keyword.Contains(upper) || q.Text.Contains(term));
        }

        var total = await query.CountAsync();
        var quotes = await query
            .OrderBy(q => q.Keyword)
            .ThenBy(q => q.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new
        {
            total, page, pageSize, quotes
        });
    }

    /// <summary>
    ///     Adds a quote
    /// </summary>
    [HttpPost("quotes")]
    public async Task<IActionResult> AddQuote(ulong guildId, [FromBody] QuoteRequest request)
    {
        var keyword = request.Keyword?.Trim().ToUpperInvariant();
        var text = request.Text?.Trim();
        if (string.IsNullOrWhiteSpace(keyword) || string.IsNullOrWhiteSpace(text))
            return BadRequest("Keyword and text are required");

        IUser? author = client.GetUser(request.AuthorId);
        author ??= await client.Rest.GetUserAsync(request.AuthorId);
        var quote = new Quote
        {
            GuildId = guildId,
            Keyword = keyword,
            Text = text,
            AuthorId = request.AuthorId,
            AuthorName = author?.Username ?? request.AuthorId.ToString(),
            DateAdded = DateTime.UtcNow
        };

        await using var db = await dbFactory.CreateConnectionAsync();
        quote.Id = await db.InsertWithInt32IdentityAsync(quote);
        auditContext.RecordAfter(quote);
        return Ok(quote);
    }

    /// <summary>
    ///     Edits a quote's keyword or text
    /// </summary>
    [HttpPut("quotes/{quoteId:int}")]
    public async Task<IActionResult> UpdateQuote(ulong guildId, int quoteId, [FromBody] QuoteRequest request)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var quote = await db.Quotes.FirstOrDefaultAsync(q => q.Id == quoteId && q.GuildId == guildId);
        if (quote == null)
            return NotFound("Quote not found");

        auditContext.RecordBefore(quote);
        if (!string.IsNullOrWhiteSpace(request.Keyword))
            quote.Keyword = request.Keyword.Trim().ToUpperInvariant();
        if (!string.IsNullOrWhiteSpace(request.Text))
            quote.Text = request.Text.Trim();
        await db.UpdateAsync(quote);
        auditContext.RecordAfter(quote);
        return Ok(quote);
    }

    /// <summary>
    ///     Deletes a quote
    /// </summary>
    [HttpDelete("quotes/{quoteId:int}")]
    public async Task<IActionResult> DeleteQuote(ulong guildId, int quoteId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var quote = await db.Quotes.FirstOrDefaultAsync(q => q.Id == quoteId && q.GuildId == guildId);
        if (quote == null)
            return NotFound("Quote not found");

        auditContext.RecordBefore(quote);
        await db.Quotes.Where(q => q.Id == quoteId).DeleteAsync();
        return Ok();
    }

    #endregion

    #region Auto publish

    /// <summary>
    ///     Lists auto publish channels with their blacklists
    /// </summary>
    [HttpGet("autopublish")]
    public async Task<IActionResult> GetAutoPublish(ulong guildId)
    {
        var entries = await autoPublishService.GetAutoPublishes(guildId);
        var guild = client.GetGuild(guildId);
        var result = entries
            .Where(e => e.AutoPublish != null)
            .Select(e => new
            {
                e.AutoPublish!.ChannelId,
                ChannelName = guild?.GetChannel(e.AutoPublish.ChannelId)?.Name,
                BlacklistedUsers = e.UserBlacklists.Where(u => u != null).Select(u => u!.User).ToList(),
                BlacklistedWords = e.WordBlacklists.Where(w => w != null && w.Word != null).Select(w => w!.Word)
                    .ToList()
            });
        return Ok(result);
    }

    /// <summary>
    ///     Enables auto publishing for a news channel
    /// </summary>
    [HttpPost("autopublish/{channelId}")]
    public async Task<IActionResult> AddAutoPublish(ulong guildId, ulong channelId)
    {
        var guild = client.GetGuild(guildId);
        if (guild?.GetChannel(channelId) is not INewsChannel channel)
            return BadRequest("Channel must be an announcement channel");
        if (!await autoPublishService.PermCheck(channel))
            return BadRequest("The bot needs Manage Messages in that channel");

        var added = await autoPublishService.AddAutoPublish(guildId, channelId);
        if (!added)
            return Conflict("Auto publish is already enabled for that channel");
        auditContext.RecordAfter(new
        {
            channelId
        });
        return Ok();
    }

    /// <summary>
    ///     Disables auto publishing for a channel
    /// </summary>
    [HttpDelete("autopublish/{channelId}")]
    public async Task<IActionResult> RemoveAutoPublish(ulong guildId, ulong channelId)
    {
        auditContext.RecordBefore(new
        {
            channelId
        });
        var removed = await autoPublishService.RemoveAutoPublish(guildId, channelId);
        return removed ? Ok() : NotFound("Auto publish is not enabled for that channel");
    }

    /// <summary>
    ///     Stops publishing messages from a user in a channel
    /// </summary>
    [HttpPost("autopublish/{channelId}/users/{userId}")]
    public async Task<IActionResult> BlacklistPublishUser(ulong guildId, ulong channelId, ulong userId)
    {
        if (!await autoPublishService.CheckIfExists(channelId))
            return NotFound("Auto publish is not enabled for that channel");
        var added = await autoPublishService.AddUserToBlacklist(channelId, userId);
        return added ? Ok() : Conflict("User is already blacklisted");
    }

    /// <summary>
    ///     Allows a user's messages to be published again
    /// </summary>
    [HttpDelete("autopublish/{channelId}/users/{userId}")]
    public async Task<IActionResult> UnblacklistPublishUser(ulong guildId, ulong channelId, ulong userId)
    {
        var removed = await autoPublishService.RemoveUserFromBlacklist(channelId, userId);
        return removed ? Ok() : NotFound("User is not blacklisted");
    }

    /// <summary>
    ///     Stops publishing messages containing a word
    /// </summary>
    [HttpPost("autopublish/{channelId}/words")]
    public async Task<IActionResult> BlacklistPublishWord(ulong guildId, ulong channelId,
        [FromBody] WordRequest request)
    {
        var word = request.Word?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(word))
            return BadRequest("Word is required");
        if (!await autoPublishService.CheckIfExists(channelId))
            return NotFound("Auto publish is not enabled for that channel");
        var added = await autoPublishService.AddWordToBlacklist(channelId, word);
        return added ? Ok() : Conflict("Word is already blacklisted");
    }

    /// <summary>
    ///     Removes a word from the publish blacklist
    /// </summary>
    [HttpDelete("autopublish/{channelId}/words/{word}")]
    public async Task<IActionResult> UnblacklistPublishWord(ulong guildId, ulong channelId, string word)
    {
        var removed = await autoPublishService.RemoveWordFromBlacklist(channelId, word.Trim().ToLowerInvariant());
        return removed ? Ok() : NotFound("Word is not blacklisted");
    }

    #endregion

    #region Stream role

    /// <summary>
    ///     Gets stream role settings
    /// </summary>
    [HttpGet("streamrole")]
    public async Task<IActionResult> GetStreamRole(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var settings = await db.StreamRoleSettings.FirstOrDefaultAsync(s => s.GuildId == guildId);
        if (settings == null)
        {
            return Ok(new
            {
                enabled = false,
                addRoleId = 0UL,
                fromRoleId = 0UL,
                keyword = (string?)null,
                whitelist = new List<object>(),
                blacklist = new List<object>()
            });
        }

        var whitelist = await db.StreamRoleWhitelistedUsers
            .Where(u => u.StreamRoleSettingsId == settings.Id)
            .Select(u => new
            {
                u.UserId, u.Username
            })
            .ToListAsync();
        var blacklist = await db.StreamRoleBlacklistedUsers
            .Where(u => u.StreamRoleSettingsId == settings.Id)
            .Select(u => new
            {
                u.UserId, u.Username
            })
            .ToListAsync();

        return Ok(new
        {
            enabled = settings.Enabled,
            addRoleId = settings.AddRoleId,
            fromRoleId = settings.FromRoleId,
            keyword = settings.Keyword,
            whitelist,
            blacklist
        });
    }

    /// <summary>
    ///     Enables the stream role: members with the from role who stream get the add role
    /// </summary>
    [HttpPost("streamrole")]
    public async Task<IActionResult> SetStreamRole(ulong guildId, [FromBody] StreamRoleRequest request)
    {
        var guild = client.GetGuild(guildId);
        var fromRole = guild?.GetRole(request.FromRoleId);
        var addRole = guild?.GetRole(request.AddRoleId);
        if (fromRole == null || addRole == null)
            return NotFound("Role not found");

        await streamRoleService.SetStreamRole(fromRole, addRole);
        auditContext.RecordAfter(request);
        return Ok();
    }

    /// <summary>
    ///     Disables the stream role
    /// </summary>
    [HttpDelete("streamrole")]
    public async Task<IActionResult> StopStreamRole(ulong guildId)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound("Guild not found");
        await streamRoleService.StopStreamRole(guild);
        return Ok();
    }

    /// <summary>
    ///     Sets the keyword a stream title must contain
    /// </summary>
    [HttpPost("streamrole/keyword")]
    public async Task<IActionResult> SetStreamRoleKeyword(ulong guildId, [FromBody] WordRequest request)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound("Guild not found");
        var keyword = await streamRoleService.SetKeyword(guild, request.Word);
        return Ok(new
        {
            keyword
        });
    }

    /// <summary>
    ///     Adds or removes a user from the stream role whitelist or blacklist
    /// </summary>
    [HttpPost("streamrole/{list}/{userId}")]
    public async Task<IActionResult> AddStreamRoleListUser(ulong guildId, string list, ulong userId)
    {
        return await ApplyStreamRoleList(guildId, list, userId, AddRemove.Add);
    }

    /// <summary>
    ///     Removes a user from the stream role whitelist or blacklist
    /// </summary>
    [HttpDelete("streamrole/{list}/{userId}")]
    public async Task<IActionResult> RemoveStreamRoleListUser(ulong guildId, string list, ulong userId)
    {
        return await ApplyStreamRoleList(guildId, list, userId, AddRemove.Rem);
    }

    private async Task<IActionResult> ApplyStreamRoleList(ulong guildId, string list, ulong userId, AddRemove action)
    {
        if (!Enum.TryParse<StreamRoleListType>(list, true, out var listType))
            return BadRequest("List must be whitelist or blacklist");
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound("Guild not found");
        var user = guild.GetUser(userId) ?? (IUser?)await client.Rest.GetUserAsync(userId);
        var name = user?.Username ?? userId.ToString();
        var success = await streamRoleService.ApplyListAction(listType, guild, action, userId, name);
        return success ? Ok() : BadRequest("No change was made");
    }

    #endregion

    #region NSFW blacklist

    /// <summary>
    ///     Gets blacklisted NSFW tags
    /// </summary>
    [HttpGet("nsfw/blacklist")]
    public async Task<IActionResult> GetNsfwBlacklist(ulong guildId)
    {
        var tags = await searchImagesService.GetBlacklistedTags(guildId);
        return Ok(tags ?? []);
    }

    /// <summary>
    ///     Toggles a tag on the NSFW blacklist
    /// </summary>
    [HttpPost("nsfw/blacklist/{tag}")]
    public async Task<IActionResult> ToggleNsfwTag(ulong guildId, string tag)
    {
        tag = tag.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(tag))
            return BadRequest("Tag is required");
        auditContext.RecordBefore(await searchImagesService.GetBlacklistedTags(guildId));
        var added = await searchImagesService.ToggleBlacklistTag(guildId, tag);
        auditContext.RecordAfter(await searchImagesService.GetBlacklistedTags(guildId));
        return Ok(new
        {
            added, tag
        });
    }

    #endregion

    #region AI assistant

    /// <summary>
    ///     Gets the AI assistant configuration with the API key masked
    /// </summary>
    [HttpGet("ai")]
    public async Task<IActionResult> GetAiConfig(ulong guildId)
    {
        var config = await aiService.GetOrCreateConfig(guildId);
        return Ok(ToAiResponse(config));
    }

    /// <summary>
    ///     Updates the AI assistant configuration
    /// </summary>
    [HttpPut("ai")]
    public async Task<IActionResult> UpdateAiConfig(ulong guildId, [FromBody] AiConfigRequest request)
    {
        var config = await aiService.GetOrCreateConfig(guildId);
        auditContext.RecordBefore(ToAiResponse(config));

        if (request.Enabled.HasValue) config.Enabled = request.Enabled.Value;
        if (request.ChannelId.HasValue) config.ChannelId = request.ChannelId.Value;
        if (request.Provider.HasValue)
        {
            if (!Enum.IsDefined(typeof(AiService.AiProvider), request.Provider.Value))
                return BadRequest("Unknown provider");
            config.Provider = request.Provider.Value;
        }

        if (request.Model != null)
            config.Model = string.IsNullOrWhiteSpace(request.Model) ? null : request.Model.Trim();
        if (request.SystemPrompt != null)
            config.SystemPrompt = string.IsNullOrWhiteSpace(request.SystemPrompt) ? null : request.SystemPrompt;
        if (request.WebSearchEnabled.HasValue) config.WebSearchEnabled = request.WebSearchEnabled.Value;
        if (request.HideWebSearchMessages.HasValue) config.HideWebSearchMessages = request.HideWebSearchMessages.Value;
        if (!string.IsNullOrWhiteSpace(request.ApiKey)) config.ApiKey = request.ApiKey.Trim();
        if (request.ClearApiKey == true) config.ApiKey = null;

        if (config.Enabled && config.ChannelId == 0)
            return BadRequest("Pick a channel before enabling the assistant");

        await aiService.UpdateConfig(config);

        if (request.CustomEmbed != null)
            await aiService.SetCustomEmbed(guildId, request.CustomEmbed);
        if (request.WebhookUrl != null)
            await aiService.SetWebhook(guildId,
                string.IsNullOrWhiteSpace(request.WebhookUrl) ? null : request.WebhookUrl);

        var updated = await aiService.GetOrCreateConfig(guildId);
        auditContext.RecordAfter(ToAiResponse(updated));
        return Ok(ToAiResponse(updated));
    }

    /// <summary>
    ///     Lists models available for a provider using the stored API key
    /// </summary>
    [HttpGet("ai/models")]
    public async Task<IActionResult> GetAiModels(ulong guildId, [FromQuery] int provider)
    {
        if (!Enum.IsDefined(typeof(AiService.AiProvider), provider))
            return BadRequest("Unknown provider");
        var config = await aiService.GetOrCreateConfig(guildId);
        if (string.IsNullOrWhiteSpace(config.ApiKey))
            return BadRequest("Set an API key first");

        try
        {
            var models = await aiService.GetSupportedModels((AiService.AiProvider)provider, config.ApiKey);
            return Ok(models.Select(m => new
            {
                m.Id, m.Name
            }));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to list AI models for guild {GuildId}", guildId);
            return BadRequest("Could not fetch models. Check the API key.");
        }
    }

    private static object ToAiResponse(GuildAiConfig config)
    {
        return new
        {
            config.Enabled,
            config.ChannelId,
            config.Provider,
            ProviderName = ((AiService.AiProvider)config.Provider).ToString(),
            config.Model,
            config.SystemPrompt,
            config.WebSearchEnabled,
            config.HideWebSearchMessages,
            HasApiKey = !string.IsNullOrWhiteSpace(config.ApiKey),
            ApiKeyHint = string.IsNullOrWhiteSpace(config.ApiKey) || config.ApiKey.Length < 8
                ? null
                : $"{config.ApiKey[..4]}…{config.ApiKey[^4..]}",
            config.CustomEmbed,
            HasWebhook = !string.IsNullOrWhiteSpace(config.WebhookUrl),
            config.TokensUsed
        };
    }

    #endregion

    #region Role monitor

    /// <summary>
    ///     Gets role monitor configuration
    /// </summary>
    [HttpGet("rolemonitor")]
    public async Task<IActionResult> GetRoleMonitor(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var settings = await db.RoleMonitoringSettings.FirstOrDefaultAsync(s => s.GuildId == guildId);
        var blacklistedRoles = await db.BlacklistedRoles.Where(r => r.GuildId == guildId).ToListAsync();
        var blacklistedPermissions = await db.BlacklistedPermissions.Where(p => p.GuildId == guildId).ToListAsync();
        var whitelistedRoles = await db.WhitelistedRoles.Where(r => r.GuildId == guildId).Select(r => r.RoleId)
            .ToListAsync();
        var whitelistedUsers = await db.WhitelistedUsers.Where(u => u.GuildId == guildId).Select(u => u.UserId)
            .ToListAsync();

        return Ok(new
        {
            defaultPunishment = settings?.DefaultPunishmentAction ?? (int)PunishmentAction.None,
            blacklistedRoles = blacklistedRoles.Select(r => new
            {
                r.RoleId, punishment = r.PunishmentAction
            }),
            blacklistedPermissions = blacklistedPermissions.Select(p => new
            {
                p.Permission,
                permissionName = ((GuildPermission)p.Permission).ToString(),
                punishment = p.PunishmentAction
            }),
            whitelistedRoles,
            whitelistedUsers
        });
    }

    /// <summary>
    ///     Sets the default punishment for role monitor violations
    /// </summary>
    [HttpPost("rolemonitor/default")]
    public async Task<IActionResult> SetRoleMonitorDefault(ulong guildId, [FromBody] PunishmentRequest request)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound("Guild not found");
        if (!Enum.IsDefined(typeof(PunishmentAction), request.Punishment))
            return BadRequest("Unknown punishment");
        await roleMonitorService.SetDefaultPunishmentAsync(guild, (PunishmentAction)request.Punishment);
        return Ok();
    }

    /// <summary>
    ///     Blacklists a role so it cannot be handed out
    /// </summary>
    [HttpPost("rolemonitor/roles")]
    public async Task<IActionResult> AddBlacklistedRole(ulong guildId, [FromBody] RoleMonitorRoleRequest request)
    {
        var guild = client.GetGuild(guildId);
        var role = guild?.GetRole(request.RoleId);
        if (role == null)
            return NotFound("Role not found");
        PunishmentAction? punishment = request.Punishment.HasValue ? (PunishmentAction)request.Punishment.Value : null;
        await roleMonitorService.AddBlacklistedRoleAsync(guild!, role, punishment);
        return Ok();
    }

    /// <summary>
    ///     Removes a role from the blacklist
    /// </summary>
    [HttpDelete("rolemonitor/roles/{roleId}")]
    public async Task<IActionResult> RemoveBlacklistedRole(ulong guildId, ulong roleId)
    {
        var guild = client.GetGuild(guildId);
        var role = guild?.GetRole(roleId);
        if (role == null)
            return NotFound("Role not found");
        await roleMonitorService.RemoveBlacklistedRoleAsync(guild!, role);
        return Ok();
    }

    /// <summary>
    ///     Blacklists a permission so roles granting it are reverted
    /// </summary>
    [HttpPost("rolemonitor/permissions")]
    public async Task<IActionResult> AddBlacklistedPermission(ulong guildId,
        [FromBody] RoleMonitorPermissionRequest request)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound("Guild not found");
        if (!Enum.IsDefined(typeof(GuildPermission), request.Permission))
            return BadRequest("Unknown permission");
        PunishmentAction? punishment = request.Punishment.HasValue ? (PunishmentAction)request.Punishment.Value : null;
        await roleMonitorService.AddBlacklistedPermissionAsync(guild, (GuildPermission)request.Permission, punishment);
        return Ok();
    }

    /// <summary>
    ///     Removes a permission from the blacklist
    /// </summary>
    [HttpDelete("rolemonitor/permissions/{permission}")]
    public async Task<IActionResult> RemoveBlacklistedPermission(ulong guildId, ulong permission)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound("Guild not found");
        await roleMonitorService.RemoveBlacklistedPermissionAsync(guild, (GuildPermission)permission);
        return Ok();
    }

    /// <summary>
    ///     Whitelists a role from role monitoring
    /// </summary>
    [HttpPost("rolemonitor/whitelist/roles/{roleId}")]
    public async Task<IActionResult> WhitelistRole(ulong guildId, ulong roleId)
    {
        var guild = client.GetGuild(guildId);
        var role = guild?.GetRole(roleId);
        if (role == null)
            return NotFound("Role not found");
        await roleMonitorService.AddWhitelistedRoleAsync(guild!, role);
        return Ok();
    }

    /// <summary>
    ///     Removes a role from the whitelist
    /// </summary>
    [HttpDelete("rolemonitor/whitelist/roles/{roleId}")]
    public async Task<IActionResult> UnwhitelistRole(ulong guildId, ulong roleId)
    {
        var guild = client.GetGuild(guildId);
        var role = guild?.GetRole(roleId);
        if (role == null)
            return NotFound("Role not found");
        await roleMonitorService.RemoveWhitelistedRoleAsync(guild!, role);
        return Ok();
    }

    /// <summary>
    ///     Whitelists a user from role monitoring
    /// </summary>
    [HttpPost("rolemonitor/whitelist/users/{userId}")]
    public async Task<IActionResult> WhitelistUser(ulong guildId, ulong userId)
    {
        var guild = client.GetGuild(guildId);
        var user = guild?.GetUser(userId);
        if (user == null)
            return NotFound("Member not found");
        await roleMonitorService.AddWhitelistedUserAsync(guild!, user);
        return Ok();
    }

    /// <summary>
    ///     Removes a user from the whitelist
    /// </summary>
    [HttpDelete("rolemonitor/whitelist/users/{userId}")]
    public async Task<IActionResult> UnwhitelistUser(ulong guildId, ulong userId)
    {
        var guild = client.GetGuild(guildId);
        var user = guild?.GetUser(userId);
        if (user == null)
            return NotFound("Member not found");
        await roleMonitorService.RemoveWhitelistedUserAsync(guild!, user);
        return Ok();
    }

    #endregion
}

/// <summary>
///     Command alias request
/// </summary>
public class AliasRequest
{
    /// <summary>
    ///     The word members type
    /// </summary>
    public string Trigger { get; set; } = string.Empty;

    /// <summary>
    ///     The command it expands to
    /// </summary>
    public string Mapping { get; set; } = string.Empty;
}

/// <summary>
///     Quote create or update request
/// </summary>
public class QuoteRequest
{
    /// <summary>
    ///     The keyword the quote is retrieved with
    /// </summary>
    public string? Keyword { get; set; }

    /// <summary>
    ///     The quote text
    /// </summary>
    public string? Text { get; set; }

    /// <summary>
    ///     The dashboard user creating the quote
    /// </summary>
    public ulong AuthorId { get; set; }
}

/// <summary>
///     Request carrying a single word
/// </summary>
public class WordRequest
{
    /// <summary>
    ///     The word
    /// </summary>
    public string? Word { get; set; }
}

/// <summary>
///     Stream role configuration request
/// </summary>
public class StreamRoleRequest
{
    /// <summary>
    ///     Members must have this role to be eligible
    /// </summary>
    public ulong FromRoleId { get; set; }

    /// <summary>
    ///     Role added while streaming
    /// </summary>
    public ulong AddRoleId { get; set; }
}

/// <summary>
///     AI assistant configuration request; null fields are left unchanged
/// </summary>
public class AiConfigRequest
{
    /// <summary>Whether the assistant responds</summary>
    public bool? Enabled { get; set; }

    /// <summary>Channel the assistant listens in</summary>
    public ulong? ChannelId { get; set; }

    /// <summary>Provider as an <see cref="AiService.AiProvider" /> value</summary>
    public int? Provider { get; set; }

    /// <summary>Model id</summary>
    public string? Model { get; set; }

    /// <summary>System prompt</summary>
    public string? SystemPrompt { get; set; }

    /// <summary>Whether web search is allowed</summary>
    public bool? WebSearchEnabled { get; set; }

    /// <summary>Whether web search status messages are hidden</summary>
    public bool? HideWebSearchMessages { get; set; }

    /// <summary>New API key, ignored when empty</summary>
    public string? ApiKey { get; set; }

    /// <summary>Set to true to remove the stored API key</summary>
    public bool? ClearApiKey { get; set; }

    /// <summary>Custom embed template JSON</summary>
    public string? CustomEmbed { get; set; }

    /// <summary>Webhook url used for responses, empty string clears it</summary>
    public string? WebhookUrl { get; set; }
}

/// <summary>
///     Punishment selection request
/// </summary>
public class PunishmentRequest
{
    /// <summary>
    ///     The punishment as a <see cref="PunishmentAction" /> value
    /// </summary>
    public int Punishment { get; set; }
}

/// <summary>
///     Role monitor blacklisted role request
/// </summary>
public class RoleMonitorRoleRequest
{
    /// <summary>The role id</summary>
    public ulong RoleId { get; set; }

    /// <summary>Optional punishment override</summary>
    public int? Punishment { get; set; }
}

/// <summary>
///     Role monitor blacklisted permission request
/// </summary>
public class RoleMonitorPermissionRequest
{
    /// <summary>The permission as a <see cref="GuildPermission" /> value</summary>
    public ulong Permission { get; set; }

    /// <summary>Optional punishment override</summary>
    public int? Punishment { get; set; }
}