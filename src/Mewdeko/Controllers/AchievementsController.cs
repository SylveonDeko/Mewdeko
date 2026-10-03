using Mewdeko.Common.Palette;
using Mewdeko.Controllers.Common.Achievements;
using Mewdeko.Controllers.Common.AuditLog;
using Mewdeko.Controllers.Common.DashboardAccess;
using Mewdeko.Modules.Achievements.Common;
using Mewdeko.Modules.Achievements.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     API controller for running achievements from the dashboard: settings, the achievement library,
///     categories, and members.
/// </summary>
[ApiController]
[Route("botapi/[controller]/{guildId}")]
[Authorize("ApiKeyPolicy")]
public class AchievementsController : Controller
{
    private readonly IDashboardAuditContext auditContext;
    private readonly DiscordShardedClient client;
    private readonly AchievementIconService icons;
    private readonly ILogger<AchievementsController> logger;
    private readonly GuildPaletteService paletteService;
    private readonly AchievementService service;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AchievementsController" /> class.
    /// </summary>
    /// <param name="service">The achievement service.</param>
    /// <param name="icons">Uploaded icons.</param>
    /// <param name="paletteService">Guild palettes, for the card designer.</param>
    /// <param name="client">The Discord client.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="auditContext">Records before and after state for the dashboard audit log.</param>
    public AchievementsController(AchievementService service, AchievementIconService icons,
        GuildPaletteService paletteService, DiscordShardedClient client, ILogger<AchievementsController> logger,
        IDashboardAuditContext auditContext)
    {
        this.service = service;
        this.icons = icons;
        this.paletteService = paletteService;
        this.client = client;
        this.logger = logger;
        this.auditContext = auditContext;
    }

    /// <summary>
    ///     Settings, data sources, totals, and recent unlocks.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>The overview.</returns>
    [HttpGet]
    public async Task<IActionResult> GetOverview(ulong guildId)
    {
        var guild = client.GetGuild(guildId);
        if (guild is null)
            return NotFound("Guild not found.");

        var catalog = await service.GetCatalogAsync(guildId);
        var counts = await service.GetUnlockCountsAsync(guildId);
        var (members, unlocks, week) = await service.GetGuildTotalsAsync(guildId);
        var recent = await service.GetRecentUnlocksAsync(guildId, 12);

        AchievementRarityResponse Rarity(KeyValuePair<string, int> pair)
        {
            var def = catalog.ByKey[pair.Key];
            return new AchievementRarityResponse
            {
                Key = def.Key,
                Name = def.Name,
                Icon = catalog.IconFor(def).Stored,
                IconUrl = catalog.ImageUrl(catalog.IconFor(def)),
                Grade = (int)def.Grade,
                Count = pair.Value
            };
        }

        var known = counts.Where(c => catalog.ByKey.ContainsKey(c.Key)).ToList();
        return Ok(new AchievementOverviewResponse
        {
            Settings = AchievementMapping.MapSettings(service.GetSettings(guildId)),
            DataSources = await service.GetDataSourcesAsync(guildId),
            Members = members,
            Unlocks = unlocks,
            UnlocksThisWeek = week,
            Earnable = catalog.Earnable.Count,
            Total = catalog.All.Count(d => !d.IsGlobal),
            CustomCount = catalog.All.Count(d => d.IsCustom),
            Recent = recent
                .Where(r => catalog.ByKey.ContainsKey(r.AchievementKey))
                .Select(r =>
                {
                    var def = catalog.ByKey[r.AchievementKey];
                    var member = guild.GetUser(r.UserId);
                    IUser? user = member ?? client.GetUser(r.UserId);
                    return new AchievementRecentUnlockResponse
                    {
                        UserId = r.UserId,
                        Username = member?.DisplayName ?? user?.Username ?? r.UserId.ToString(),
                        AvatarUrl = member?.GetDisplayAvatarUrl() ?? user?.GetAvatarUrl(),
                        Key = def.Key,
                        Name = def.Name,
                        Icon = catalog.IconFor(def).Stored,
                        IconUrl = catalog.ImageUrl(catalog.IconFor(def)),
                        Grade = (int)def.Grade,
                        UnlockedAt = r.UnlockedAt
                    };
                })
                .ToList(),
            MostCommon = known.OrderByDescending(c => c.Value).Take(5).Select(Rarity).ToList(),
            Rarest = known.OrderBy(c => c.Value).Take(5).Select(Rarity).ToList()
        });
    }

    /// <summary>
    ///     Changes settings.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="request">The changes.</param>
    /// <returns>The saved settings.</returns>
    [HttpPut("settings")]
    public async Task<IActionResult> UpdateSettings(ulong guildId, [FromBody] AchievementSettingsRequest request)
    {
        var guild = client.GetGuild(guildId);
        if (guild is null)
            return NotFound("Guild not found.");

        if (request.AnnounceMode is < 0 or > 4)
            return BadRequest("Unknown announcement mode.");
        if (request.UnlockMessage?.Length > AchievementService.MessageLength)
            return BadRequest("The unlock message is too long.");
        if (request.XpPerPoint is < 0 or > 1000)
            return BadRequest("XP per point has to be 0 to 1000.");
        if (request.LogChannelId is > 0 && guild.GetTextChannel(request.LogChannelId.Value) is null)
            return BadRequest("That log channel doesn't exist.");

        auditContext.RecordBefore(AchievementMapping.MapSettings(service.GetSettings(guildId)));
        if (request.Enabled is { } enabled && enabled != service.IsEnabled(guildId))
            await service.SetEnabledAsync(guildId, enabled);

        var saved = await service.UpdateSettingsAsync(guildId, row =>
        {
            if (request.AnnounceMode is { } mode)
                row.AnnounceMode = mode;
            if (request.LogChannelId is { } channelId)
                row.LogChannelId = channelId == 0 ? null : channelId;
            if (request.DmByDefault is { } dm)
                row.DmByDefault = dm;
            if (request.MentionUsers is { } mention)
                row.MentionUsers = mention;
            if (request.UnlockMessage is not null)
                row.UnlockMessage = string.IsNullOrWhiteSpace(request.UnlockMessage) ? null : request.UnlockMessage;
            if (request.XpPerPoint is { } xp)
                row.XpPerPoint = xp;
            if (request.RevealHidden is { } reveal)
                row.RevealHidden = reveal;
            if (request.UnlockImage is { } unlockImage)
                row.UnlockImage = unlockImage;
            if (request.DeleteAfter is { } deleteAfter)
                row.DeleteAfter = Math.Clamp(deleteAfter, 0, AchievementService.MaxDeleteAfter);
            if (request.ExcludedRoleIds is not null)
                row.ExcludedRoleIds = AchievementService.JoinKeys(request.ExcludedRoleIds);
            if (request.ExcludedChannelIds is not null)
                row.ExcludedChannelIds = AchievementService.JoinKeys(request.ExcludedChannelIds);
        });

        var response = AchievementMapping.MapSettings(saved);
        auditContext.RecordAfter(response);
        return Ok(response);
    }

    /// <summary>
    ///     Every achievement and category, with grades, metrics, ranks, placeholders, and limits.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>The catalog.</returns>
    [HttpGet("catalog")]
    public async Task<IActionResult> GetCatalog(ulong guildId)
    {
        var guild = client.GetGuild(guildId);
        if (guild is null)
            return NotFound("Guild not found.");

        return Ok(await AchievementMapping.MapCatalogAsync(service, guild));
    }

    /// <summary>
    ///     Channels, roles, and emojis for editors.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>The lookups.</returns>
    [HttpGet("lookups")]
    public IActionResult GetLookups(ulong guildId)
    {
        var guild = client.GetGuild(guildId);
        if (guild is null)
            return NotFound("Guild not found.");

        var bot = guild.CurrentUser;
        var channels = guild.Channels
            .Where(c => c is SocketTextChannel and not SocketThreadChannel || c is SocketVoiceChannel)
            .OrderBy(c => (c as INestedChannel)?.CategoryId is { } categoryId ? guild.GetCategoryChannel(categoryId)?.Position ?? -1 : -1)
            .ThenBy(c => c is SocketVoiceChannel ? 1 : 0)
            .ThenBy(c => c.Position)
            .Select(c =>
            {
                var perms = bot.GetPermissions(c);
                return new AchievementChannelLookup
                {
                    Id = c.Id,
                    Name = c.Name,
                    CategoryName = (c as INestedChannel)?.CategoryId is { } categoryId
                        ? guild.GetCategoryChannel(categoryId)?.Name
                        : null,
                    Type = c is SocketVoiceChannel ? 2 : 0,
                    CanSend = c is SocketTextChannel && perms.ViewChannel && perms.SendMessages && perms.EmbedLinks
                };
            })
            .ToList();

        var roles = guild.Roles
            .Where(r => r.Id != guild.Id)
            .OrderByDescending(r => r.Position)
            .Select(r => new AchievementRoleLookup
            {
                Id = r.Id,
                Name = r.Name,
                Color = r.Colors.PrimaryColor.RawValue,
                Position = r.Position,
                Assignable = AchievementService.CanAssign(guild, r.Id)
            })
            .ToList();

        var emojis = guild.Emotes
            .Where(e => e.IsAvailable != false)
            .OrderBy(e => e.Name)
            .Select(e => new AchievementEmojiLookup
            {
                Id = e.Id,
                Name = e.Name,
                Formatted = e.ToString(),
                Url = e.Url
            })
            .ToList();

        return Ok(new AchievementLookupsResponse
        {
            Channels = channels,
            Roles = roles,
            Emojis = emojis,
            BotCanManageRoles = bot.GuildPermissions.ManageRoles
        });
    }

    /// <summary>
    ///     Saves a server's changes to a built in achievement.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="key">The built in key.</param>
    /// <param name="request">The changes.</param>
    /// <returns>The achievement.</returns>
    [HttpPut("builtin/{key}")]
    public async Task<IActionResult> SaveOverride(ulong guildId, string key, [FromBody] AchievementOverrideRequest request)
    {
        var guild = client.GetGuild(guildId);
        if (guild is null)
            return NotFound("Guild not found.");

        auditContext.RecordBefore(await FindResponseAsync(guild, key));
        var result = await service.SaveOverrideAsync(guild, key, new AchievementOverrideDraft
        {
            Enabled = request.Enabled,
            Name = request.Name,
            Description = request.Description,
            Icon = request.Icon,
            Points = request.Points,
            Hidden = request.Hidden,
            RoleRewardId = request.RoleRewardId,
            CurrencyReward = request.CurrencyReward,
            XpReward = request.XpReward
        });
        return await RespondAsync(guild, result);
    }

    /// <summary>
    ///     Puts a built in achievement back to its default.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="key">The built in key.</param>
    /// <returns>The achievement.</returns>
    [HttpDelete("builtin/{key}")]
    public async Task<IActionResult> ResetOverride(ulong guildId, string key)
    {
        var guild = client.GetGuild(guildId);
        if (guild is null)
            return NotFound("Guild not found.");

        auditContext.RecordBefore(await FindResponseAsync(guild, key));
        return await RespondAsync(guild, await service.ResetOverrideAsync(guildId, key));
    }

    /// <summary>
    ///     Turns many achievements on or off at once.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="request">The keys and state.</param>
    /// <returns>How many changed.</returns>
    [HttpPut("enabled")]
    public async Task<IActionResult> SetEnabled(ulong guildId, [FromBody] AchievementBulkEnableRequest request)
    {
        if (client.GetGuild(guildId) is null)
            return NotFound("Guild not found.");
        if (request.Keys.Count == 0)
            return BadRequest("Pick at least one achievement.");

        auditContext.RecordBefore(null);
        var changed = await service.SetManyEnabledAsync(guildId, request.Keys, request.Enabled);
        auditContext.RecordAfter(new
        {
            request.Keys, request.Enabled
        });
        return Ok(new
        {
            changed
        });
    }

    /// <summary>
    ///     Creates a server made achievement.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="request">The achievement.</param>
    /// <returns>The achievement.</returns>
    [HttpPost("custom")]
    public async Task<IActionResult> CreateCustom(ulong guildId, [FromBody] CustomAchievementRequest request)
    {
        var guild = client.GetGuild(guildId);
        if (guild is null)
            return NotFound("Guild not found.");

        var actor = await HttpContext.GetDashboardUserIdAsync() ?? 0;
        auditContext.RecordBefore(null);
        try
        {
            return await RespondAsync(guild, await service.CreateCustomAsync(guild, actor, ToDraft(request)));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create an achievement in {GuildId}", guildId);
            return StatusCode(500, "Failed to save the achievement.");
        }
    }

    /// <summary>
    ///     Replaces a server made achievement.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="id">The achievement ID.</param>
    /// <param name="request">The new state.</param>
    /// <returns>The achievement.</returns>
    [HttpPut("custom/{id:int}")]
    public async Task<IActionResult> UpdateCustom(ulong guildId, int id, [FromBody] CustomAchievementRequest request)
    {
        var guild = client.GetGuild(guildId);
        if (guild is null)
            return NotFound("Guild not found.");

        auditContext.RecordBefore(await FindResponseAsync(guild, AchievementCatalog.CustomKey(id)));
        return await RespondAsync(guild, await service.UpdateCustomAsync(guild, id, ToDraft(request)));
    }

    /// <summary>
    ///     Deletes a server made achievement and every unlock of it.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="id">The achievement ID.</param>
    /// <returns>No content.</returns>
    [HttpDelete("custom/{id:int}")]
    public async Task<IActionResult> DeleteCustom(ulong guildId, int id)
    {
        var guild = client.GetGuild(guildId);
        if (guild is null)
            return NotFound("Guild not found.");

        auditContext.RecordBefore(await FindResponseAsync(guild, AchievementCatalog.CustomKey(id)));
        if (!await service.DeleteCustomAsync(guildId, id))
            return NotFound("Achievement not found.");
        auditContext.RecordAfter(null);
        return NoContent();
    }

    /// <summary>
    ///     Sets the order of the server's own achievements.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="request">IDs in order.</param>
    /// <returns>No content.</returns>
    [HttpPut("custom/order")]
    public async Task<IActionResult> ReorderCustom(ulong guildId, [FromBody] AchievementOrderRequest request)
    {
        if (client.GetGuild(guildId) is null)
            return NotFound("Guild not found.");

        await service.ReorderCustomAsync(guildId, request.Ids);
        return NoContent();
    }

    /// <summary>
    ///     Creates a server made category.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="request">The category.</param>
    /// <returns>The categories.</returns>
    [HttpPost("categories")]
    public async Task<IActionResult> CreateCategory(ulong guildId, [FromBody] AchievementCategoryRequest request)
    {
        if (client.GetGuild(guildId) is null)
            return NotFound("Guild not found.");

        auditContext.RecordBefore(null);
        var result = await service.CreateCategoryAsync(guildId, request.Name, request.Description, request.Icon);
        if (!result.Success)
            return Failure(result.Error);
        return await CategoriesAsync(guildId, AchievementCatalog.CategoryKey(result.Value!.Id));
    }

    /// <summary>
    ///     Renames a server made category.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="id">The category ID.</param>
    /// <param name="request">The new state.</param>
    /// <returns>The categories.</returns>
    [HttpPut("categories/{id:int}")]
    public async Task<IActionResult> UpdateCategory(ulong guildId, int id, [FromBody] AchievementCategoryRequest request)
    {
        if (client.GetGuild(guildId) is null)
            return NotFound("Guild not found.");

        auditContext.RecordBefore(null);
        var result = await service.UpdateCategoryAsync(guildId, id, request.Name, request.Description, request.Icon);
        if (!result.Success)
            return Failure(result.Error);
        return await CategoriesAsync(guildId, AchievementCatalog.CategoryKey(id));
    }

    /// <summary>
    ///     Deletes a server made category, moving its achievements to the server category.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="id">The category ID.</param>
    /// <returns>No content.</returns>
    [HttpDelete("categories/{id:int}")]
    public async Task<IActionResult> DeleteCategory(ulong guildId, int id)
    {
        if (client.GetGuild(guildId) is null)
            return NotFound("Guild not found.");

        auditContext.RecordBefore(null);
        return await service.DeleteCategoryAsync(guildId, id) ? NoContent() : NotFound("Category not found.");
    }

    /// <summary>
    ///     Saves the category order and which categories are off.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="request">The layout.</param>
    /// <returns>The categories.</returns>
    [HttpPut("categories/layout")]
    public async Task<IActionResult> SetCategoryLayout(ulong guildId, [FromBody] AchievementCategoryLayoutRequest request)
    {
        if (client.GetGuild(guildId) is null)
            return NotFound("Guild not found.");

        auditContext.RecordBefore(AchievementMapping.MapSettings(service.GetSettings(guildId)));
        await service.SetCategoryLayoutAsync(guildId, request.Order, request.Disabled);
        auditContext.RecordAfter(AchievementMapping.MapSettings(service.GetSettings(guildId)));
        return await CategoriesAsync(guildId, null);
    }

    /// <summary>
    ///     A page of members ranked by achievements, optionally filtered by name.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="search">Name filter.</param>
    /// <param name="page">0 based page.</param>
    /// <param name="pageSize">Rows per page, up to 100.</param>
    /// <param name="sort">0 points, 1 unlocked, 2 recent.</param>
    /// <returns>The members.</returns>
    [HttpGet("members")]
    public async Task<IActionResult> GetMembers(ulong guildId, [FromQuery] string? search = null,
        [FromQuery] int page = 0, [FromQuery] int pageSize = 25, [FromQuery] int sort = 0)
    {
        var guild = client.GetGuild(guildId);
        if (guild is null)
            return NotFound("Guild not found.");

        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(0, page);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            var matches = guild.Users
                .Where(u => !u.IsBot && (u.Username.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                                         u.DisplayName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                                         u.Id.ToString() == term))
                .Take(500)
                .ToList();
            var rows = await service.GetMemberRowsAsync(guildId, matches.Select(u => u.Id).ToList());
            var ordered = matches
                .Select(u => (User: u, Row: rows.GetValueOrDefault(u.Id)))
                .OrderByDescending(x => x.Row?.Points ?? 0)
                .ThenBy(x => x.User.DisplayName)
                .ToList();
            return Ok(new AchievementMembersResponse
            {
                Total = ordered.Count,
                Members = ordered
                    .Skip(page * pageSize)
                    .Take(pageSize)
                    .Select(x => AchievementMapping.MapMember(guild, x.User.Id, x.Row?.Points ?? 0,
                        x.Row?.UnlockedCount ?? 0, x.Row?.LastUnlockAt, 0, client))
                    .ToList()
            });
        }

        var (entries, total) = await service.GetLeaderboardAsync(guildId, (AchievementLeaderboardSort)Math.Clamp(sort, 0, 2),
            page, pageSize, true);
        return Ok(new AchievementMembersResponse
        {
            Total = total,
            Members = entries.Select(e => AchievementMapping.MapMember(guild, e.UserId, e.Points, e.Unlocked,
                e.LastUnlockAt, e.Rank, client)).ToList()
        });
    }

    /// <summary>
    ///     One member's progress, badges, and totals.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The member.</param>
    /// <returns>The detail.</returns>
    [HttpGet("members/{userId}")]
    public async Task<IActionResult> GetMember(ulong guildId, ulong userId)
    {
        var guild = client.GetGuild(guildId);
        if (guild is null)
            return NotFound("Guild not found.");

        var summary = await service.GetSummaryAsync(guildId, userId);
        var progress = await service.GetProgressAsync(guild, userId);
        var badges = await service.GetOwnedBadgesAsync(guildId, userId);
        var catalog = await service.GetCatalogAsync(guildId);

        return Ok(new AchievementMemberDetailResponse
        {
            Member = AchievementMapping.MapMember(guild, userId, summary.Points, summary.Unlocked, summary.LastUnlockAt,
                summary.Rank, client),
            Total = summary.Total,
            Progress = progress.Select(p => new AchievementProgressResponse
            {
                Key = p.Definition.Key,
                UnlockedAt = p.UnlockedAt,
                Current = p.Current
            }).ToList(),
            Badges = badges.Select(b => AchievementMapping.MapBadge(b, catalog)).ToList(),
            Equipped = summary.Equipped.ToList()
        });
    }

    /// <summary>
    ///     Hands an achievement to a member.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The member.</param>
    /// <param name="request">The achievement.</param>
    /// <returns>The member's detail.</returns>
    [HttpPost("members/{userId}/grant")]
    public async Task<IActionResult> Grant(ulong guildId, ulong userId, [FromBody] AchievementGrantRequest request)
    {
        var guild = client.GetGuild(guildId);
        var member = guild?.GetUser(userId);
        if (guild is null || member is null)
            return NotFound("Member not found.");

        var actor = await HttpContext.GetDashboardUserIdAsync() ?? 0;
        auditContext.RecordBefore(null);
        var result = await service.GrantAsync(member, request.Key, actor);
        if (!result.Success)
            return Failure(result.Error);
        auditContext.RecordAfter(new
        {
            userId, request.Key
        });
        return await GetMember(guildId, userId);
    }

    /// <summary>
    ///     Takes an achievement from a member.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The member.</param>
    /// <param name="key">The achievement key.</param>
    /// <returns>The member's detail.</returns>
    [HttpDelete("members/{userId}/achievements/{key}")]
    public async Task<IActionResult> Revoke(ulong guildId, ulong userId, string key)
    {
        if (client.GetGuild(guildId) is null)
            return NotFound("Guild not found.");

        auditContext.RecordBefore(new
        {
            userId, key
        });
        var result = await service.RevokeAsync(guildId, userId, key);
        if (!result.Success)
            return Failure(result.Error);
        return await GetMember(guildId, userId);
    }

    /// <summary>
    ///     Clears every achievement a member has.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The member.</param>
    /// <returns>How many were cleared.</returns>
    [HttpPost("members/{userId}/reset")]
    public async Task<IActionResult> ResetMember(ulong guildId, ulong userId)
    {
        if (client.GetGuild(guildId) is null)
            return NotFound("Guild not found.");

        auditContext.RecordBefore(new
        {
            userId
        });
        var cleared = await service.ResetMemberAsync(guildId, userId);
        return Ok(new
        {
            cleared
        });
    }

    /// <summary>
    ///     Clears every achievement in the server and checks everyone again quietly.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>How many unlocks were cleared.</returns>
    [HttpPost("reset")]
    public async Task<IActionResult> ResetGuild(ulong guildId)
    {
        if (client.GetGuild(guildId) is null)
            return NotFound("Guild not found.");

        auditContext.RecordBefore(null);
        var cleared = await service.ResetGuildAsync(guildId);
        return Ok(new
        {
            cleared
        });
    }

    /// <summary>
    ///     Checks every member again soon, quietly.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>Accepted.</returns>
    [HttpPost("recheck")]
    public IActionResult Recheck(ulong guildId)
    {
        if (client.GetGuild(guildId) is null)
            return NotFound("Guild not found.");

        service.QueueSweep(guildId);
        return Accepted();
    }

    /// <summary>
    ///     Turns on message counting so message achievements can track.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>The data sources.</returns>
    [HttpPost("data-sources/messages")]
    public async Task<IActionResult> EnableMessageCounting(ulong guildId)
    {
        if (client.GetGuild(guildId) is null)
            return NotFound("Guild not found.");

        await service.EnableMessageCountingAsync(guildId);
        return Ok(await service.GetDataSourcesAsync(guildId));
    }

    /// <summary>
    ///     Every Font Awesome glyph the icon picker offers, with code points for clients that draw the font
    ///     themselves.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>The glyphs.</returns>
    [HttpGet("glyphs")]
    [SkipAudit]
    public IActionResult GetGlyphs(ulong guildId)
    {
        return Ok(AchievementIcons.Glyphs.Select(g => new AchievementGlyphResponse
        {
            Name = g.Name,
            Codepoint = g.Codepoint,
            Aliases = g.Aliases.ToList()
        }));
    }

    /// <summary>
    ///     Stores an image to use as an icon. It is shrunk to 256 pixels and saved as PNG.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="request">The image.</param>
    /// <returns>The upload.</returns>
    [HttpPost("icons")]
    public async Task<IActionResult> UploadIcon(ulong guildId, [FromBody] AchievementIconUploadRequest request)
    {
        if (client.GetGuild(guildId) is null)
            return NotFound("Guild not found.");

        if (DecodeUpload(request.Data) is not { } bytes)
            return Failure(AchievementError.IconInvalid);

        auditContext.RecordBefore(null);
        var actor = await HttpContext.GetDashboardUserIdAsync() ?? 0;
        var result = await icons.UploadAsync(guildId, actor, bytes);
        if (!result.Success)
            return Failure(result.Error);

        var catalog = await service.GetCatalogAsync(guildId);
        var response = new AchievementIconUploadResponse
        {
            Id = result.Value!.Id,
            Icon = AchievementIcons.UploadPrefix + result.Value.Id,
            Url = catalog.UploadUrls.GetValueOrDefault(result.Value.Id) ??
                  AchievementService.IconUploadPath(guildId, result.Value.Id)
        };
        auditContext.RecordAfter(response);
        return Ok(response);
    }

    /// <summary>
    ///     Deletes an uploaded icon. Achievements and categories using it go back to their default icon.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="id">The upload ID.</param>
    /// <returns>No content.</returns>
    [HttpDelete("icons/{id:int}")]
    public async Task<IActionResult> DeleteIcon(ulong guildId, int id)
    {
        auditContext.RecordBefore(new { id });
        return await icons.DeleteAsync(guildId, id) ? NoContent() : NotFound("That icon doesn't exist.");
    }

    /// <summary>
    ///     The bytes of an uploaded icon, for the dashboard to re-serve publicly on instances without a CDN.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="id">The upload ID.</param>
    /// <returns>The PNG.</returns>
    [HttpGet("icons/{id:int}")]
    [SkipAudit]
    public async Task<IActionResult> GetIcon(ulong guildId, int id)
    {
        var bytes = await icons.GetBytesAsync(guildId, id);
        return bytes is null ? NotFound() : File(bytes, "image/png");
    }

    /// <summary>
    ///     Draws an achievement's image as a member sees it: unlocked, or locked with their progress. Returned
    ///     as a base64 PNG so it passes through JSON proxies.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="key">The achievement key.</param>
    /// <param name="userId">The member, or the dashboard user when missing.</param>
    /// <returns>The image as a data URI.</returns>
    [HttpGet("image")]
    [SkipAudit]
    public async Task<IActionResult> GetImage(ulong guildId, [FromQuery] string key, [FromQuery] ulong? userId)
    {
        var guild = client.GetGuild(guildId);
        if (guild is null)
            return NotFound("Guild not found.");

        var memberId = userId ?? await HttpContext.GetDashboardUserIdAsync() ?? 0;
        var member = guild.GetUser(memberId) ?? guild.CurrentUser;
        var catalog = await service.GetCatalogAsync(guildId);
        if (!catalog.ByKey.ContainsKey(key))
            return Failure(AchievementError.NotFound);

        var progress = (await service.GetProgressAsync(guild, member.Id, d => d.Key == key)).FirstOrDefault();
        if (progress is null)
            return Failure(AchievementError.NotFound);

        await using var image = await service.RenderProgressImageAsync(member, progress, catalog);
        if (image is null)
            return StatusCode(500, "Couldn't draw that image.");
        return Ok(new { image = "data:image/png;base64," + Convert.ToBase64String(image.ToArray()) });
    }

    /// <summary>
    ///     Draws an achievement as the editor has it, before saving, as just unlocked by the dashboard user.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="request">The achievement as edited.</param>
    /// <returns>The image as a data URI.</returns>
    [HttpPost("image/preview")]
    [SkipAudit]
    public async Task<IActionResult> PreviewImage(ulong guildId, [FromBody] AchievementImagePreviewRequest request)
    {
        var guild = client.GetGuild(guildId);
        if (guild is null)
            return NotFound("Guild not found.");

        var catalog = await service.GetCatalogAsync(guildId);
        AchievementDefinition? stored = null;
        if (request.Key is { } key)
            catalog.ByKey.TryGetValue(key, out stored);

        var grade = (AchievementGrade)Math.Clamp(request.Grade, 0, AchievementCatalog.Grades.Count - 1);
        var categoryKey = request.CategoryKey ?? stored?.CategoryKey ?? AchievementCatalog.CustomCategory;
        var name = string.IsNullOrWhiteSpace(request.Name) ? stored?.Name ?? "New achievement" : request.Name.Trim();
        var definition = new AchievementDefinition
        {
            Key = stored?.Key ?? "preview",
            CategoryKey = categoryKey,
            Name = name.Length > AchievementService.NameLength ? name[..AchievementService.NameLength] : name,
            Description = string.IsNullOrWhiteSpace(request.Description)
                ? stored?.Description ?? ""
                : request.Description.Trim(),
            Icon = AchievementIcons.TryNormalize(request.Icon, out var icon) ? icon ?? "" : "",
            Grade = grade,
            Points = request.Points ?? stored?.Points ?? AchievementCatalog.GetGrade(grade).Points
        };

        var memberId = await HttpContext.GetDashboardUserIdAsync() ?? 0;
        var member = guild.GetUser(memberId) ?? guild.CurrentUser;
        await using var image = await service.RenderPreviewImageAsync(member, definition, catalog);
        if (image is null)
            return StatusCode(500, "Couldn't draw that image.");
        return Ok(new { image = "data:image/png;base64," + Convert.ToBase64String(image.ToArray()) });
    }

    /// <summary>
    ///     The server's card designs, its default, what uses which, card images, and the palette the bot draws with.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>The cards.</returns>
    [HttpGet("card")]
    [SkipAudit]
    public async Task<IActionResult> GetCard(ulong guildId)
    {
        var guild = client.GetGuild(guildId);
        if (guild is null)
            return NotFound("Guild not found.");
        return Ok(await CardResponseAsync(guild));
    }

    /// <summary>
    ///     Saves a new card design. It is brought within limits before saving.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="request">Its name and design.</param>
    /// <returns>The cards.</returns>
    [HttpPost("card/designs")]
    public async Task<IActionResult> CreateCardDesign(ulong guildId, [FromBody] AchievementCardDesignRequest request)
    {
        var guild = client.GetGuild(guildId);
        if (guild is null)
            return NotFound("Guild not found.");

        auditContext.RecordBefore(null);
        var result = await service.CreateCardDesignAsync(guildId, request.Name ?? "",
            request.Template ?? AchievementCardTemplate.Default());
        if (!result.Success)
            return Failure(result.Error);
        if (request.MakeDefault)
            await service.SetDefaultCardAsync(guildId, result.Value!.Id);
        auditContext.RecordAfter(new { result.Value!.Id, result.Value.Name, request.MakeDefault });
        return Ok(await CardResponseAsync(guild));
    }

    /// <summary>
    ///     Renames or changes a saved card design.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="id">The design ID.</param>
    /// <param name="request">Its new name, design, or both.</param>
    /// <returns>The cards.</returns>
    [HttpPut("card/designs/{id:int}")]
    public async Task<IActionResult> UpdateCardDesign(ulong guildId, int id,
        [FromBody] AchievementCardDesignRequest request)
    {
        var guild = client.GetGuild(guildId);
        if (guild is null)
            return NotFound("Guild not found.");

        auditContext.RecordBefore(new { id });
        var result = await service.UpdateCardDesignAsync(guildId, id, request.Name, request.Template);
        if (!result.Success)
            return Failure(result.Error);
        auditContext.RecordAfter(new { result.Value!.Id, result.Value.Name });
        return Ok(await CardResponseAsync(guild));
    }

    /// <summary>
    ///     Deletes a saved card design. Anything using it falls back to the next design in line.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="id">The design ID.</param>
    /// <returns>The cards.</returns>
    [HttpDelete("card/designs/{id:int}")]
    public async Task<IActionResult> DeleteCardDesign(ulong guildId, int id)
    {
        var guild = client.GetGuild(guildId);
        if (guild is null)
            return NotFound("Guild not found.");

        auditContext.RecordBefore(new { id });
        if (!await service.DeleteCardDesignAsync(guildId, id))
            return Failure(AchievementError.NotFound);
        return Ok(await CardResponseAsync(guild));
    }

    /// <summary>
    ///     Sets the design every achievement uses unless its category or itself picks another.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="request">The design, or null for the built in one.</param>
    /// <returns>The cards.</returns>
    [HttpPut("card/default")]
    public async Task<IActionResult> SetDefaultCard(ulong guildId, [FromBody] AchievementCardDefaultRequest request)
    {
        var guild = client.GetGuild(guildId);
        if (guild is null)
            return NotFound("Guild not found.");

        auditContext.RecordBefore(new { id = service.GetSettings(guildId).Row.DefaultCardId });
        var error = await service.SetDefaultCardAsync(guildId, request.Id);
        if (error != AchievementError.None)
            return Failure(error);
        auditContext.RecordAfter(new { id = request.Id });
        return Ok(await CardResponseAsync(guild));
    }

    /// <summary>
    ///     Sets the design a category or achievement uses instead of the default.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="request">The category or achievement and the design.</param>
    /// <returns>The cards.</returns>
    [HttpPut("card/assign")]
    public async Task<IActionResult> AssignCard(ulong guildId, [FromBody] AchievementCardAssignRequest request)
    {
        var guild = client.GetGuild(guildId);
        if (guild is null)
            return NotFound("Guild not found.");

        auditContext.RecordBefore(null);
        var error = await service.AssignCardAsync(guildId, request.Category, request.Key, request.Id);
        if (error != AchievementError.None)
            return Failure(error);
        auditContext.RecordAfter(request);
        return Ok(await CardResponseAsync(guild));
    }

    /// <summary>
    ///     Draws a card design before it is saved, as the dashboard user, and reports where each element landed so
    ///     editors can draw handles over the image.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="request">The design and state to draw.</param>
    /// <returns>The image and layout.</returns>
    [HttpPost("card/preview")]
    [SkipAudit]
    public async Task<IActionResult> PreviewCard(ulong guildId, [FromBody] AchievementCardPreviewRequest request)
    {
        var guild = client.GetGuild(guildId);
        if (guild is null)
            return NotFound("Guild not found.");

        var catalog = await service.GetCatalogAsync(guildId);
        var definition = request.Key is { } key && catalog.ByKey.TryGetValue(key, out var chosen)
            ? chosen
            : catalog.All.FirstOrDefault(d => d.IsMetric && !d.Hidden) ?? catalog.All.FirstOrDefault();
        if (definition is null)
            return Failure(AchievementError.NotFound);

        AchievementCardTemplate template;
        if (request.Template is not null)
            template = await service.OwnedCardAsync(guildId, request.Template);
        else if (request.DesignId is { } designId)
            template = (await service.GetCardDesignsAsync(guildId)).FirstOrDefault(d => d.Id == designId)?.Template ??
                       AchievementCardTemplate.Default();
        else
            template = await service.GetCardForAsync(guildId, definition);
        var memberId = await HttpContext.GetDashboardUserIdAsync() ?? 0;
        var member = guild.GetUser(memberId) ?? guild.CurrentUser;
        var result = await service.RenderCardPreviewAsync(member, template, definition, catalog, request.Locked);
        if (result is null)
            return StatusCode(500, "Couldn't draw that card.");

        await using var image = result.Image;
        return Ok(new AchievementCardPreviewResponse
        {
            Image = "data:image/png;base64," + Convert.ToBase64String(image.ToArray()),
            Width = result.CardWidth,
            Height = result.CardHeight,
            Margin = AchievementUnlockRenderer.Margin,
            Layout = result.Layout.Select(b => new AchievementCardBoxResponse
            {
                Id = b.Id,
                X = b.X,
                Y = b.Y,
                W = b.W,
                H = b.H,
                Drawn = b.Drawn
            }).ToList(),
            Template = template
        });
    }

    /// <summary>
    ///     Stores an image for the card designer, shrunk to 1600 pixels and saved as PNG. Delete it like an icon.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="request">The image.</param>
    /// <returns>The upload.</returns>
    [HttpPost("card/images")]
    public async Task<IActionResult> UploadCardImage(ulong guildId, [FromBody] AchievementIconUploadRequest request)
    {
        if (client.GetGuild(guildId) is null)
            return NotFound("Guild not found.");
        if (DecodeUpload(request.Data) is not { } bytes)
            return Failure(AchievementError.IconInvalid);

        auditContext.RecordBefore(null);
        var actor = await HttpContext.GetDashboardUserIdAsync() ?? 0;
        var result = await icons.UploadAsync(guildId, actor, bytes, AchievementIconService.CardKind);
        if (!result.Success)
            return Failure(result.Error);

        var catalog = await service.GetCatalogAsync(guildId);
        var response = new AchievementIconUploadResponse
        {
            Id = result.Value!.Id,
            Icon = AchievementIcons.UploadPrefix + result.Value.Id,
            Url = catalog.UploadUrls.GetValueOrDefault(result.Value.Id) ??
                  AchievementService.IconUploadPath(guildId, result.Value.Id)
        };
        auditContext.RecordAfter(response);
        return Ok(response);
    }

    private async Task<AchievementCardResponse> CardResponseAsync(SocketGuild guild)
    {
        var settings = service.GetSettings(guild.Id);
        var catalog = await service.GetCatalogAsync(guild.Id);
        var palette = await paletteService.GetAsync(guild);
        var designs = await service.GetCardDesignsAsync(guild.Id);
        return new AchievementCardResponse
        {
            Designs = designs.Select(d => new AchievementCardDesignResponse
            {
                Id = d.Id,
                Name = d.Name,
                Template = d.Template,
                DateUpdated = d.DateUpdated
            }).ToList(),
            DefaultId = designs.Any(d => d.Id == settings.Row.DefaultCardId) ? settings.Row.DefaultCardId : null,
            BuiltIn = AchievementCardTemplate.Default(),
            Assignments = settings.CardAssignments,
            Images = catalog.CardUploads.Order().Select(id => new AchievementIconUploadResponse
            {
                Id = id,
                Icon = AchievementIcons.UploadPrefix + id,
                Url = catalog.UploadUrls.GetValueOrDefault(id) ?? AchievementService.IconUploadPath(guild.Id, id)
            }).ToList(),
            Placeholders = AchievementCardRules.Placeholders.ToList(),
            Palette = new Dictionary<string, string>
            {
                ["primary"] = palette.Primary,
                ["secondary"] = palette.Secondary,
                ["accent"] = palette.Accent,
                ["text"] = palette.Text,
                ["muted"] = $"#{palette.MutedColor.Red:x2}{palette.MutedColor.Green:x2}{palette.MutedColor.Blue:x2}",
                ["background"] = palette.Background
            }
        };
    }

    private static byte[]? DecodeUpload(string data)
    {
        var payload = data.Trim();
        var comma = payload.IndexOf(',');
        if (payload.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && comma > 0)
            payload = payload[(comma + 1)..];
        try
        {
            return Convert.FromBase64String(payload);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private async Task<AchievementResponse?> FindResponseAsync(SocketGuild guild, string key)
    {
        var catalog = await service.GetCatalogAsync(guild.Id);
        return catalog.ByKey.TryGetValue(key, out var def)
            ? AchievementMapping.Map(def, catalog, guild, null, null, null)
            : null;
    }

    private async Task<IActionResult> RespondAsync(SocketGuild guild, AchievementResult<AchievementDefinition> result)
    {
        if (!result.Success || result.Value is null)
            return Failure(result.Error);

        var counts = await service.GetUnlockCountsAsync(guild.Id);
        var (overrides, customs) = await service.GetRawRowsAsync(guild.Id);
        var catalog = await service.GetCatalogAsync(guild.Id);
        var response = AchievementMapping.Map(result.Value, catalog, guild, counts, overrides, customs);
        auditContext.RecordAfter(response);
        return Ok(response);
    }

    private async Task<IActionResult> CategoriesAsync(ulong guildId, string? changedKey)
    {
        var catalog = await service.GetCatalogAsync(guildId);
        var categories = AchievementMapping.MapCategories(catalog, service.GetSettings(guildId));
        auditContext.RecordAfter(changedKey is null ? categories : categories.FirstOrDefault(c => c.Key == changedKey));
        return Ok(categories);
    }

    private static CustomAchievementDraft ToDraft(CustomAchievementRequest request)
    {
        return new CustomAchievementDraft
        {
            CategoryKey = request.CategoryKey,
            Name = request.Name,
            Description = request.Description,
            Icon = request.Icon,
            Grade = (AchievementGrade)Math.Clamp(request.Grade, 0, AchievementCatalog.Grades.Count - 1),
            Points = request.Points,
            Hidden = request.Hidden,
            Enabled = request.Enabled,
            Trigger = (AchievementTrigger)request.Trigger,
            Metric = (AchievementMetric)request.Metric,
            Threshold = request.Threshold,
            Keyword = request.Keyword,
            ChannelId = request.ChannelId,
            RoleRewardId = request.RoleRewardId,
            CurrencyReward = request.CurrencyReward,
            XpReward = request.XpReward
        };
    }

    private IActionResult Failure(AchievementError error)
    {
        var message = AchievementMapping.Describe(error);
        return error switch
        {
            AchievementError.NotFound => NotFound(message),
            AchievementError.AlreadyInState => Conflict(message),
            _ => BadRequest(message)
        };
    }
}
