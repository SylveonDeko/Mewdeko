using DataModel;
using Mewdeko.Controllers.Common.DashboardAccess;
using Mewdeko.Controllers.Common.RoleMenus;
using Mewdeko.Modules.Administration.Services;
using Mewdeko.Modules.RoleMenus.Common;
using Mewdeko.Modules.RoleMenus.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     API controller for creating, editing, and posting role menus from the dashboard.
/// </summary>
[ApiController]
[Route("botapi/[controller]/{guildId}")]
[Authorize("ApiKeyPolicy")]
public class RoleMenusController : Controller
{
    private readonly IDashboardAuditContext auditContext;
    private readonly DiscordShardedClient client;
    private readonly ILogger<RoleMenusController> logger;
    private readonly RoleCommandsService roleCommands;
    private readonly RoleMenuService service;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RoleMenusController" /> class.
    /// </summary>
    /// <param name="service">The role menu service.</param>
    /// <param name="roleCommands">The older emoji role setup service.</param>
    /// <param name="client">The Discord client.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="auditContext">Records before/after state for the dashboard audit log.</param>
    public RoleMenusController(
        RoleMenuService service,
        RoleCommandsService roleCommands,
        DiscordShardedClient client,
        ILogger<RoleMenusController> logger,
        IDashboardAuditContext auditContext)
    {
        this.service = service;
        this.roleCommands = roleCommands;
        this.client = client;
        this.logger = logger;
        this.auditContext = auditContext;
    }

    /// <summary>
    ///     Lists a guild's role menus.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>The menus ordered by ID.</returns>
    [HttpGet]
    public async Task<IActionResult> GetMenus(ulong guildId)
    {
        var guild = client.GetGuild(guildId);
        if (guild is null)
            return NotFound("Guild not found.");

        var menus = await service.GetMenusAsync(guildId);
        if (menus.Any(m => m.Options.Any(o => o.MaxHolders > 0)))
            await RoleMenuService.EnsureMembersAsync(guild);
        return Ok(menus.Select(m => ToResponse(guild, m)).ToList());
    }

    /// <summary>
    ///     Gets one role menu.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="menuId">The menu ID.</param>
    /// <returns>The menu.</returns>
    [HttpGet("{menuId:int}")]
    public async Task<IActionResult> GetMenu(ulong guildId, int menuId)
    {
        var guild = client.GetGuild(guildId);
        if (guild is null)
            return NotFound("Guild not found.");

        var menu = await service.GetGuildMenuAsync(guildId, menuId);
        if (menu is null)
            return NotFound("Role menu not found.");
        if (menu.Options.Any(o => o.MaxHolders > 0))
            await RoleMenuService.EnsureMembersAsync(guild);
        return Ok(ToResponse(guild, menu));
    }

    /// <summary>
    ///     Creates a role menu and posts its message.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="request">The menu to create.</param>
    /// <returns>The created menu.</returns>
    [HttpPost]
    public async Task<IActionResult> CreateMenu(ulong guildId, [FromBody] RoleMenuRequest request)
    {
        var guild = client.GetGuild(guildId);
        if (guild is null)
            return NotFound("Guild not found.");

        var actor = await ResolveActorAsync(guild);
        if (actor is null)
            return StatusCode(403, "You are not a member of this server.");

        try
        {
            auditContext.RecordBefore(null);
            var result = await service.CreateAsync(guildId, actor, ToDraft(request));
            if (!result.Success || result.Menu is null)
                return Failure(result);

            var response = ToResponse(guild, result.Menu);
            auditContext.RecordAfter(response);
            return Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create a role menu in guild {GuildId}", guildId);
            return StatusCode(500, "Failed to save the role menu.");
        }
    }

    /// <summary>
    ///     Replaces a role menu and rebuilds its message in place.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="menuId">The menu ID.</param>
    /// <param name="request">The desired state.</param>
    /// <returns>The updated menu.</returns>
    [HttpPut("{menuId:int}")]
    public async Task<IActionResult> UpdateMenu(ulong guildId, int menuId, [FromBody] RoleMenuRequest request)
    {
        var guild = client.GetGuild(guildId);
        if (guild is null)
            return NotFound("Guild not found.");

        var actor = await ResolveActorAsync(guild);
        if (actor is null)
            return StatusCode(403, "You are not a member of this server.");

        try
        {
            var before = await service.GetGuildMenuAsync(guildId, menuId);
            if (before is null)
                return NotFound("Role menu not found.");
            auditContext.RecordBefore(ToResponse(guild, before));

            var result = await service.UpdateAsync(guildId, menuId, actor, ToDraft(request));
            if (!result.Success || result.Menu is null)
                return Failure(result);

            var response = ToResponse(guild, result.Menu);
            auditContext.RecordAfter(response);
            return Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update role menu {MenuId} in guild {GuildId}", menuId, guildId);
            return StatusCode(500, "Failed to save the role menu.");
        }
    }

    /// <summary>
    ///     Deletes a role menu and its message.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="menuId">The menu ID.</param>
    /// <returns>An empty success, or 404.</returns>
    [HttpDelete("{menuId:int}")]
    public async Task<IActionResult> DeleteMenu(ulong guildId, int menuId)
    {
        var guild = client.GetGuild(guildId);
        if (guild is null)
            return NotFound("Guild not found.");

        var actor = await ResolveActorAsync(guild);
        if (actor is null)
            return StatusCode(403, "You are not a member of this server.");

        try
        {
            var before = await service.GetGuildMenuAsync(guildId, menuId);
            if (before is null)
                return NotFound("Role menu not found.");
            auditContext.RecordBefore(ToResponse(guild, before));

            var result = await service.DeleteAsync(guildId, menuId);
            if (!result.Success)
                return Failure(result);

            auditContext.RecordAfter(null);
            return Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete role menu {MenuId} in guild {GuildId}", menuId, guildId);
            return StatusCode(500, "Failed to save the role menu.");
        }
    }

    /// <summary>
    ///     Pauses or resumes a role menu.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="menuId">The menu ID.</param>
    /// <param name="request">Whether the menu is enabled.</param>
    /// <returns>The updated menu.</returns>
    [HttpPut("{menuId:int}/enabled")]
    public async Task<IActionResult> SetEnabled(ulong guildId, int menuId, [FromBody] RoleMenuEnabledRequest request)
    {
        var guild = client.GetGuild(guildId);
        if (guild is null)
            return NotFound("Guild not found.");

        var actor = await ResolveActorAsync(guild);
        if (actor is null)
            return StatusCode(403, "You are not a member of this server.");

        try
        {
            var before = await service.GetGuildMenuAsync(guildId, menuId);
            if (before is null)
                return NotFound("Role menu not found.");
            auditContext.RecordBefore(ToResponse(guild, before));

            var result = await service.SetEnabledAsync(guildId, menuId, request.Enabled);
            if (!result.Success || result.Menu is null)
                return Failure(result);

            var response = ToResponse(guild, result.Menu);
            auditContext.RecordAfter(response);
            return Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to toggle role menu {MenuId} in guild {GuildId}", menuId, guildId);
            return StatusCode(500, "Failed to save the role menu.");
        }
    }

    /// <summary>
    ///     Reorders a role menu's options.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="menuId">The menu ID.</param>
    /// <param name="request">Every option ID in the new order.</param>
    /// <returns>The updated menu.</returns>
    [HttpPut("{menuId:int}/order")]
    public async Task<IActionResult> Reorder(ulong guildId, int menuId, [FromBody] RoleMenuOrderRequest request)
    {
        var guild = client.GetGuild(guildId);
        if (guild is null)
            return NotFound("Guild not found.");

        var actor = await ResolveActorAsync(guild);
        if (actor is null)
            return StatusCode(403, "You are not a member of this server.");

        try
        {
            var before = await service.GetGuildMenuAsync(guildId, menuId);
            if (before is null)
                return NotFound("Role menu not found.");
            auditContext.RecordBefore(ToResponse(guild, before));

            var result = await service.ReorderAsync(guildId, menuId, request.OptionIds ?? []);
            if (!result.Success || result.Menu is null)
                return Failure(result);

            var response = ToResponse(guild, result.Menu);
            auditContext.RecordAfter(response);
            return Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to reorder role menu {MenuId} in guild {GuildId}", menuId, guildId);
            return StatusCode(500, "Failed to save the role menu.");
        }
    }

    /// <summary>
    ///     Posts a fresh copy of a role menu and deletes the old message.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="menuId">The menu ID.</param>
    /// <param name="request">The target channel, or null for the same channel.</param>
    /// <returns>The updated menu.</returns>
    [HttpPost("{menuId:int}/repost")]
    public async Task<IActionResult> Repost(ulong guildId, int menuId, [FromBody] RoleMenuRepostRequest? request)
    {
        var guild = client.GetGuild(guildId);
        if (guild is null)
            return NotFound("Guild not found.");

        var actor = await ResolveActorAsync(guild);
        if (actor is null)
            return StatusCode(403, "You are not a member of this server.");

        try
        {
            var before = await service.GetGuildMenuAsync(guildId, menuId);
            if (before is null)
                return NotFound("Role menu not found.");
            auditContext.RecordBefore(ToResponse(guild, before));

            var result = await service.RepostAsync(guildId, menuId, request?.ChannelId);
            if (!result.Success || result.Menu is null)
                return Failure(result);

            var response = ToResponse(guild, result.Menu);
            auditContext.RecordAfter(response);
            return Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to repost role menu {MenuId} in guild {GuildId}", menuId, guildId);
            return StatusCode(500, "Failed to save the role menu.");
        }
    }

    /// <summary>
    ///     Gets the channels, roles, emojis, and limits the menu editor needs.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>The lookups.</returns>
    [HttpGet("lookups")]
    public async Task<IActionResult> GetLookups(ulong guildId)
    {
        var guild = client.GetGuild(guildId);
        if (guild is null)
            return NotFound("Guild not found.");

        var actor = await ResolveActorAsync(guild);
        var botCanManageRoles = guild.CurrentUser.GuildPermissions.ManageRoles;
        var menuCount = (await service.GetMenusAsync(guildId)).Count;

        var channels = guild.TextChannels
            .Where(RoleMenuService.IsMenuChannel)
            .OrderBy(c => c.Category?.Position ?? -1)
            .ThenBy(c => c.Position)
            .ThenBy(c => c.Id)
            .Select(c =>
            {
                var problem = RoleMenuService.ChannelProblem(guild, c);
                return new RoleMenuChannelLookup
                {
                    Id = c.Id,
                    Name = c.Name,
                    CategoryName = c.Category?.Name,
                    Position = c.Position,
                    CanPost = problem is null,
                    Problem = problem
                };
            })
            .ToList();

        var roles = guild.Roles
            .Where(r => r.Id != guild.Id)
            .OrderByDescending(r => r.Position)
            .Select(r =>
            {
                var problem = service.CheckRole(guild, actor, r.Id);
                return new RoleMenuRoleLookup
                {
                    Id = r.Id,
                    Name = r.Name,
                    Color = r.Colors.PrimaryColor.RawValue,
                    Position = r.Position,
                    Assignable = problem == RoleMenuRoleProblem.None,
                    Problem = RoleMenuService.RoleProblemText(problem, botCanManageRoles)
                };
            })
            .ToList();

        var emojis = guild.Emotes
            .Where(e => e.IsAvailable != false)
            .OrderBy(e => e.Name)
            .Select(e => new RoleMenuEmojiLookup
            {
                Id = e.Id,
                Name = e.Name,
                Animated = e.Animated,
                Formatted = e.ToString(),
                Url = e.Url
            })
            .ToList();

        return Ok(new RoleMenuLookupsResponse
        {
            Channels = channels,
            Roles = roles,
            Emojis = emojis,
            BotCanManageRoles = botCanManageRoles,
            MenuCount = menuCount,
            Limits = new RoleMenuLimitsLookup
            {
                MaxOptions = RoleMenuService.MaxOptions,
                MaxReactionOptions = RoleMenuService.MaxReactionOptions,
                ButtonsPerRow = RoleMenuService.ButtonsPerRow,
                NameLength = RoleMenuService.NameLength,
                LabelLength = RoleMenuService.LabelLength,
                DescriptionLength = RoleMenuService.DescriptionLength,
                PlaceholderLength = RoleMenuService.PlaceholderLength,
                MaxTempSeconds = RoleMenuService.MaxTempSeconds,
                MaxBlockedRoles = RoleMenuService.MaxBlockedRoles
            }
        });
    }

    /// <summary>
    ///     Lists the older emoji role setups that can be moved into role menus.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>The older setups.</returns>
    [HttpGet("import-sources")]
    public async Task<IActionResult> GetImportSources(ulong guildId)
    {
        var guild = client.GetGuild(guildId);
        if (guild is null)
            return NotFound("Guild not found.");

        var (_, messages) = await roleCommands.Get(guildId);
        var sources = (messages ?? []).Select(m => new RoleMenuImportSourceResponse
        {
            Id = m.Id,
            ChannelId = m.ChannelId,
            ChannelName = guild.GetChannel(m.ChannelId)?.Name,
            MessageId = m.MessageId,
            JumpUrl = $"https://discord.com/channels/{guildId}/{m.ChannelId}/{m.MessageId}",
            Exclusive = m.Exclusive,
            Pairs = (m.ReactionRoles ?? []).Select(r =>
            {
                var role = guild.GetRole(r.RoleId);
                return new RoleMenuImportPair
                {
                    Emoji = r.EmoteName ?? "",
                    RoleId = r.RoleId,
                    RoleName = role?.Name,
                    RoleExists = role is not null
                };
            }).ToList()
        }).ToList();

        return Ok(sources);
    }

    /// <summary>
    ///     Moves an older emoji role setup into a new role menu.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="request">Import settings.</param>
    /// <returns>The created menu.</returns>
    [HttpPost("import")]
    public async Task<IActionResult> Import(ulong guildId, [FromBody] RoleMenuImportRequest request)
    {
        var guild = client.GetGuild(guildId);
        if (guild is null)
            return NotFound("Guild not found.");

        var actor = await ResolveActorAsync(guild);
        if (actor is null)
            return StatusCode(403, "You are not a member of this server.");

        try
        {
            auditContext.RecordBefore(null);
            var result = await service.ImportAsync(guildId, actor, new RoleMenuImportDraft
            {
                SourceId = request.SourceId,
                Style = Enum.IsDefined((RoleMenuStyle)request.Style)
                    ? (RoleMenuStyle)request.Style
                    : RoleMenuStyle.Dropdown,
                ChannelId = request.ChannelId,
                Name = request.Name,
                CopyMessage = request.CopyMessage,
                RetireOriginal = request.RetireOriginal
            });
            if (!result.Success || result.Menu is null)
                return Failure(result);

            var response = ToResponse(guild, result.Menu);
            auditContext.RecordAfter(response);
            return Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to import a role menu in guild {GuildId}", guildId);
            return StatusCode(500, "Failed to save the role menu.");
        }
    }

    /// <summary>
    ///     Lists the premade role menus.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>The templates with their roles.</returns>
    [HttpGet("templates")]
    public IActionResult GetTemplates(ulong guildId)
    {
        return Ok(RoleMenuTemplates.All.Select(t => new
        {
            Kind = (int)t.Kind,
            Key = t.Kind.ToString(),
            t.Name,
            Style = (int)t.Style,
            Mode = (int)t.Mode,
            t.Mentionable,
            Roles = t.Entries.Select(e => new
            {
                e.Label, e.Emoji, e.Color
            })
        }));
    }

    /// <summary>
    ///     Creates a premade role menu's roles and posts its menu, with any edits made to it first.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="kind">The template kind.</param>
    /// <param name="request">The channel and edits.</param>
    /// <returns>The created menu.</returns>
    [HttpPost("templates/{kind:int}")]
    public async Task<IActionResult> PostTemplate(ulong guildId, int kind, [FromBody] RoleMenuTemplateRequest request)
    {
        var guild = client.GetGuild(guildId);
        if (guild is null)
            return NotFound("Guild not found.");

        if (!Enum.IsDefined((RoleMenuTemplateKind)kind))
            return Failure(RoleMenuResult.Fail(RoleMenuError.TemplateNotFound));

        var actor = await ResolveActorAsync(guild);
        if (actor is null)
            return StatusCode(403, "You are not a member of this server.");

        try
        {
            var style = request.Style is { } requested && Enum.IsDefined((RoleMenuStyle)requested)
                ? (RoleMenuStyle)requested
                : (RoleMenuStyle?)null;
            var session = service.StartTemplate(guildId, actor.Id, (RoleMenuTemplateKind)kind, request.ChannelId,
                style);

            if (!string.IsNullOrWhiteSpace(request.Name))
                session.Name = request.Name.Trim();
            session.Message = string.IsNullOrWhiteSpace(request.Message) ? null : request.Message;
            if (request.Roles is { Count: > 0 })
            {
                session.Entries = request.Roles
                    .Select(r => new RoleMenuTemplateEntry(r.Label.Trim(), r.Emoji?.Trim() ?? "", r.Color))
                    .ToList();
                if (session.Entries.Any(e => e.Label.Length is 0 or > RoleMenuService.LabelLength) ||
                    session.Entries.Any(e => e.Color is < 0 or > 0xFFFFFF))
                    return BadRequest("Role names have to be 1 to 80 characters and colours 0 to 16777215.");
            }

            if (session.Name.Length > RoleMenuService.NameLength)
                return Failure(RoleMenuResult.Fail(RoleMenuError.NameInvalid));

            auditContext.RecordBefore(null);
            var result = await service.PostTemplateAsync(guild, actor, session);
            service.DropTemplateSession(guildId, actor.Id);
            if (!result.Success || result.Menu is null)
                return Failure(result);

            var response = ToResponse(guild, result.Menu);
            auditContext.RecordAfter(response);
            return Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to post a role menu template in guild {GuildId}", guildId);
            return StatusCode(500, "Failed to save the role menu.");
        }
    }

    /// <summary>
    ///     Lists a guild's role menu groups.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>The groups ordered by name.</returns>
    [HttpGet("groups")]
    public async Task<IActionResult> GetGroups(ulong guildId)
    {
        var guild = client.GetGuild(guildId);
        if (guild is null)
            return NotFound("Guild not found.");

        var menus = await service.GetMenusAsync(guildId);
        return Ok(service.GetGroups(guildId).Select(g => ToGroupResponse(g, menus)).ToList());
    }

    /// <summary>
    ///     Creates a role menu group, or changes the limit of the group with that name.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="request">The name and limit.</param>
    /// <returns>The group.</returns>
    [HttpPost("groups")]
    public async Task<IActionResult> CreateGroup(ulong guildId, [FromBody] RoleMenuGroupRequest request)
    {
        var guild = client.GetGuild(guildId);
        if (guild is null)
            return NotFound("Guild not found.");

        try
        {
            auditContext.RecordBefore(null);
            var group = await service.EnsureGroupAsync(guildId, request.Name ?? "");
            if (group is null)
                return BadRequest("Group names have to be 1 to 100 characters.");

            group = await service.SetGroupMaxAsync(guildId, group.Id, request.MaxRoles);
            if (group is null)
                return BadRequest("The limit can't be negative.");

            var response = ToGroupResponse(group, await service.GetMenusAsync(guildId));
            auditContext.RecordAfter(response);
            return Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save a role menu group in guild {GuildId}", guildId);
            return StatusCode(500, "Failed to save the group.");
        }
    }

    /// <summary>
    ///     Changes how many roles a member can hold across a group's menus.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="groupId">The group ID.</param>
    /// <param name="request">The new limit.</param>
    /// <returns>The group.</returns>
    [HttpPut("groups/{groupId:int}")]
    public async Task<IActionResult> UpdateGroup(ulong guildId, int groupId, [FromBody] RoleMenuGroupRequest request)
    {
        var guild = client.GetGuild(guildId);
        if (guild is null)
            return NotFound("Guild not found.");

        try
        {
            var before = service.GetGroups(guildId).FirstOrDefault(g => g.Id == groupId);
            if (before is null)
                return NotFound("Group not found.");

            var menus = await service.GetMenusAsync(guildId);
            auditContext.RecordBefore(ToGroupResponse(before, menus));

            var group = await service.SetGroupMaxAsync(guildId, groupId, request.MaxRoles);
            if (group is null)
                return BadRequest("The limit can't be negative.");

            var response = ToGroupResponse(group, menus);
            auditContext.RecordAfter(response);
            return Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update role menu group {GroupId} in guild {GuildId}", groupId, guildId);
            return StatusCode(500, "Failed to save the group.");
        }
    }

    /// <summary>
    ///     Deletes a role menu group and unlinks its menus.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="groupId">The group ID.</param>
    /// <returns>No content on success.</returns>
    [HttpDelete("groups/{groupId:int}")]
    public async Task<IActionResult> DeleteGroup(ulong guildId, int groupId)
    {
        var guild = client.GetGuild(guildId);
        if (guild is null)
            return NotFound("Guild not found.");

        try
        {
            var before = service.GetGroups(guildId).FirstOrDefault(g => g.Id == groupId);
            if (before is null)
                return NotFound("Group not found.");

            auditContext.RecordBefore(ToGroupResponse(before, await service.GetMenusAsync(guildId)));
            await service.DeleteGroupAsync(guildId, groupId);
            auditContext.RecordAfter(null);
            return NoContent();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete role menu group {GroupId} in guild {GuildId}", groupId, guildId);
            return StatusCode(500, "Failed to delete the group.");
        }
    }

    /// <summary>
    ///     Creates a channel for role menus where members can read and react but not send messages.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="request">The channel name and category.</param>
    /// <returns>The new channel.</returns>
    [HttpPost("channel")]
    public async Task<IActionResult> CreateChannel(ulong guildId, [FromBody] RoleMenuChannelRequest request)
    {
        var guild = client.GetGuild(guildId);
        if (guild is null)
            return NotFound("Guild not found.");

        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 100)
            return BadRequest("Channel names have to be 1 to 100 characters.");

        auditContext.RecordBefore(null);
        var category = request.CategoryId is > 0 ? guild.GetCategoryChannel(request.CategoryId.Value) : null;
        var channel = await service.CreateMenuChannelAsync(guild, request.Name.Trim(), category);
        if (channel is null)
            return StatusCode(502, "Discord refused to create the channel. Check that the bot can manage channels.");

        var response = new RoleMenuChannelLookup
        {
            Id = channel.Id,
            Name = channel.Name,
            CategoryName = category?.Name,
            Position = channel.Position,
            CanPost = true
        };
        auditContext.RecordAfter(response);
        return Ok(response);
    }

    /// <summary>
    ///     Maps a stored group to its API response.
    /// </summary>
    /// <param name="group">The group.</param>
    /// <param name="menus">The guild's menus.</param>
    /// <returns>The response.</returns>
    private static RoleMenuGroupResponse ToGroupResponse(RoleMenuGroup group, IEnumerable<RoleMenu> menus)
    {
        return new RoleMenuGroupResponse
        {
            Id = group.Id,
            Name = group.Name,
            MaxRoles = group.MaxRoles,
            MenuIds = menus.Where(m => m.GroupId == group.Id).Select(m => m.Id).OrderBy(id => id).ToList()
        };
    }

    /// <summary>
    ///     Resolves the dashboard user making the request as a guild member.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <returns>The member, or null when the request has no valid user or they are not in the guild.</returns>
    private async Task<SocketGuildUser?> ResolveActorAsync(SocketGuild guild)
    {
        var userId = await HttpContext.GetDashboardUserIdAsync() ?? 0;
        return userId == 0 ? null : guild.GetUser(userId);
    }

    /// <summary>
    ///     Maps a request to a service draft.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns>The draft.</returns>
    private static RoleMenuDraft ToDraft(RoleMenuRequest request)
    {
        return new RoleMenuDraft
        {
            Name = request.Name,
            ChannelId = request.ChannelId,
            Message = request.Message,
            Style = (RoleMenuStyle)request.Style,
            Placeholder = request.Placeholder,
            Mode = (RoleMenuMode)request.Mode,
            MinRoles = request.MinRoles,
            MaxRoles = request.MaxRoles,
            RequiredRoleId = request.RequiredRoleId is null or 0 ? null : request.RequiredRoleId,
            BlockedRoleIds = request.BlockedRoleIds ?? [],
            TempRoleSeconds = request.TempRoleSeconds,
            ExpiresAt = request.ExpiresAt,
            GroupId = request.GroupId is null or 0 ? null : request.GroupId,
            AccentColor = request.AccentColor,
            ButtonColor = request.ButtonColor,
            ReplyMode = (RoleMenuReplyMode)request.ReplyMode,
            Enabled = request.Enabled,
            Options = (request.Options ?? []).Select(o => new RoleMenuOptionDraft
            {
                Id = o.Id is > 0 ? o.Id : null,
                RoleId = o.RoleId,
                Label = o.Label,
                Emoji = o.Emoji,
                Description = o.Description,
                ButtonStyle = o.ButtonStyle,
                TempRoleSeconds = o.TempRoleSeconds,
                MaxHolders = o.MaxHolders
            }).ToList()
        };
    }

    /// <summary>
    ///     Maps a stored menu to its API response.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="menu">The menu.</param>
    /// <returns>The response.</returns>
    private RoleMenuResponse ToResponse(SocketGuild guild, RoleMenu menu)
    {
        var botCanManageRoles = guild.CurrentUser.GuildPermissions.ManageRoles;
        return new RoleMenuResponse
        {
            Id = menu.Id,
            Name = menu.Name,
            ChannelId = menu.ChannelId,
            ChannelName = guild.GetTextChannel(menu.ChannelId)?.Name,
            MessageId = menu.MessageId,
            JumpUrl = RoleMenuService.GetJumpUrl(menu),
            Message = menu.Message,
            Style = menu.Style,
            Placeholder = menu.Placeholder,
            Mode = menu.Mode,
            MinRoles = menu.MinRoles,
            MaxRoles = menu.MaxRoles,
            RequiredRoleId = menu.RequiredRoleId,
            BlockedRoleIds = RoleMenuService.ParseRoleIds(menu.BlockedRoleIds),
            TempRoleSeconds = menu.TempRoleSeconds,
            ExpiresAt = menu.ExpiresAt,
            GroupId = menu.GroupId,
            AccentColor = menu.AccentColor,
            ButtonColor = menu.ButtonColor,
            ReplyMode = menu.ReplyMode,
            Enabled = menu.Enabled,
            Status = StatusKey(RoleMenuService.GetStatus(guild, menu)),
            CreatedBy = menu.CreatedBy,
            DateAdded = menu.DateAdded,
            DateModified = menu.DateModified,
            Options = menu.Options
                .OrderBy(o => o.Position)
                .ThenBy(o => o.Id)
                .Select(o =>
                {
                    var role = guild.GetRole(o.RoleId);
                    var problem = service.CheckRole(guild, null, o.RoleId);
                    return new RoleMenuOptionResponse
                    {
                        Id = o.Id,
                        RoleId = o.RoleId,
                        RoleName = role?.Name,
                        RoleColor = role?.Colors.PrimaryColor.RawValue ?? 0,
                        Label = o.Label,
                        Emoji = o.Emoji,
                        Description = o.Description,
                        ButtonStyle = o.ButtonStyle,
                        TempRoleSeconds = o.TempRoleSeconds,
                        MaxHolders = o.MaxHolders,
                        Holders = role?.Members.Count() ?? 0,
                        Position = o.Position,
                        Problem = RoleMenuService.RoleProblemText(problem, botCanManageRoles)
                    };
                })
                .ToList()
        };
    }

    /// <summary>
    ///     Maps a status to its API key.
    /// </summary>
    /// <param name="status">The status.</param>
    /// <returns>"live", "paused", "not_posted", or "channel_missing".</returns>
    private static string StatusKey(RoleMenuStatus status)
    {
        return status switch
        {
            RoleMenuStatus.Live => "live",
            RoleMenuStatus.Paused => "paused",
            RoleMenuStatus.NotPosted => "not_posted",
            _ => "channel_missing"
        };
    }

    /// <summary>
    ///     Maps a refused write to an HTTP result with a plain text message.
    /// </summary>
    /// <param name="result">The refused result.</param>
    /// <returns>The HTTP result.</returns>
    private IActionResult Failure(RoleMenuResult result)
    {
        var detail = result.Detail ?? "";
        return result.Error switch
        {
            RoleMenuError.NotFound => NotFound("Role menu not found."),
            RoleMenuError.NameInvalid => BadRequest("Menu names have to be 1 to 100 characters."),
            RoleMenuError.NoOptions => BadRequest("A menu needs at least one option."),
            RoleMenuError.TooManyOptions => BadRequest("A menu can have at most 25 options."),
            RoleMenuError.DuplicateRole => BadRequest("Each role can only be on a menu once."),
            RoleMenuError.RoleMissing => BadRequest("A role on this menu no longer exists."),
            RoleMenuError.RoleNotAssignable => BadRequest(
                $"The bot can't give out {detail}. Move the bot's highest role above it, and make sure no integration manages it."),
            RoleMenuError.RoleAboveActor => StatusCode(403,
                $"You can only offer roles below your own highest role, and {detail} isn't."),
            RoleMenuError.InvalidEmoji => BadRequest($"{detail} isn't an emoji the bot can use."),
            RoleMenuError.InvalidChannel => BadRequest(
                $"Pick a text or announcement channel the bot can post in. {detail}".TrimEnd()),
            RoleMenuError.TextTooLong => BadRequest($"The {detail} is too long."),
            RoleMenuError.InvalidRequiredRole => BadRequest("The required role was not found."),
            RoleMenuError.InvalidDuration => BadRequest(
                "Durations have to be between 1 second and 1 year, and the menu expiry has to be in the future."),
            RoleMenuError.InvalidOrder => BadRequest("The order has to list every option exactly once."),
            RoleMenuError.PostFailed => StatusCode(502, $"Discord rejected the message: {detail}"),
            RoleMenuError.ImportSourceNotFound => NotFound("That older setup was not found."),
            RoleMenuError.ImportNoRoles => BadRequest("None of the roles in that setup exist anymore."),
            RoleMenuError.InvalidGroup => BadRequest("That group was not found."),
            RoleMenuError.InvalidColor => BadRequest(
                "The colour has to be between 0 and 16777215, and the button colour 0 to 4."),
            RoleMenuError.InvalidReactions => BadRequest(
                $"Reaction menus need a different emoji on every option and can have at most {RoleMenuService.MaxReactionOptions} options."),
            RoleMenuError.TemplateNotFound => NotFound("That template was not found."),
            RoleMenuError.RolesNotCreated => StatusCode(502,
                "Discord refused to create the roles. Check the bot's Manage Roles permission and the server's role limit."),
            _ => StatusCode(500, "Failed to save the role menu.")
        };
    }
}
