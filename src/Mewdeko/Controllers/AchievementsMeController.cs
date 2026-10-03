using Mewdeko.Controllers.Common.Achievements;
using Mewdeko.Controllers.Common.DashboardAccess;
using Mewdeko.Modules.Achievements.Common;
using Mewdeko.Modules.Achievements.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     API controller for members viewing their own achievements and changing their badges and preferences.
///     Every action checks that the signed in dashboard user is the member in the route.
/// </summary>
[ApiController]
[Route("botapi/me/{guildId}/{userId}/achievements")]
[Authorize("ApiKeyPolicy")]
[SkipDashboardAccess]
public class AchievementsMeController : Controller
{
    private readonly DiscordShardedClient client;
    private readonly AchievementService service;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AchievementsMeController" /> class.
    /// </summary>
    /// <param name="service">The achievement service.</param>
    /// <param name="client">The Discord client.</param>
    public AchievementsMeController(AchievementService service, DiscordShardedClient client)
    {
        this.service = service;
        this.client = client;
    }

    /// <summary>
    ///     The member's progress, badges, and preferences in a server.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The member.</param>
    /// <returns>The member's view.</returns>
    [HttpGet]
    public async Task<IActionResult> Get(ulong guildId, ulong userId)
    {
        var (guild, error) = await AuthorizeAsync(guildId, userId);
        if (error is not null)
            return error;

        var settings = service.GetSettings(guildId);
        var catalog = await service.GetCatalogAsync(guildId);
        var summary = await service.GetSummaryAsync(guildId, userId);
        var progress = await service.GetProgressAsync(guild!, userId);
        var badges = await service.GetOwnedBadgesAsync(guildId, userId);
        var prefs = await service.GetUserSettingsAsync(userId);
        var (globalPoints, globalUnlocked, globalServers) = await service.GetGlobalTotalsAsync(userId);
        var unlockedKeys = progress.Where(p => p.Unlocked).Select(p => p.Definition.Key).ToHashSet();

        var achievements = progress.Select(p =>
        {
            var response = AchievementMapping.Map(p.Definition, catalog, guild, null, null, null);
            if (p.Definition.Hidden && !settings.Row.RevealHidden && !unlockedKeys.Contains(p.Definition.Key))
            {
                response.Name = "Hidden achievement";
                response.Description = "Keep exploring to find it.";
                response.Icon = AchievementIcons.GlyphPrefix + "eye-slash";
                response.IconUrl = null;
                response.Keyword = null;
                response.Threshold = 0;
                response.Metric = 0;
            }

            response.RoleRewardId = null;
            response.RoleRewardName = p.Definition.RoleRewardId is { } roleId ? guild!.GetRole(roleId)?.Name : null;
            return response;
        }).ToList();

        return Ok(new AchievementMeResponse
        {
            Enabled = settings.Enabled,
            Member = AchievementMapping.MapMember(guild!, userId, summary.Points, summary.Unlocked, summary.LastUnlockAt,
                summary.Rank, client),
            Total = summary.Total,
            TierPoints = summary.Tier.MinPoints,
            NextTier = summary.NextTier?.Name,
            NextTierPoints = summary.NextTier?.MinPoints,
            Categories = AchievementMapping.MapCategories(catalog, settings)
                .Where(c => achievements.Any(a => a.CategoryKey == c.Key))
                .ToList(),
            Achievements = achievements,
            Progress = progress.Select(p => new AchievementProgressResponse
            {
                Key = p.Definition.Key,
                UnlockedAt = p.UnlockedAt,
                Current = p.Definition.Hidden && !settings.Row.RevealHidden && !p.Unlocked ? null : p.Current
            }).ToList(),
            Badges = badges.Select(b => AchievementMapping.MapBadge(b, catalog)).ToList(),
            Equipped = summary.Equipped.ToList(),
            Settings = MapSettings(prefs),
            GlobalPoints = globalPoints,
            GlobalUnlocked = globalUnlocked,
            GlobalServers = globalServers,
            Grades = AchievementMapping.MapGrades()
        });
    }

    /// <summary>
    ///     Changes the member's privacy and notification preferences. They apply in every server.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The member.</param>
    /// <param name="request">The changes.</param>
    /// <returns>The saved preferences.</returns>
    [HttpPut("settings")]
    public async Task<IActionResult> UpdateSettings(ulong guildId, ulong userId,
        [FromBody] AchievementUserSettingsRequest request)
    {
        var (_, error) = await AuthorizeAsync(guildId, userId);
        if (error is not null)
            return error;

        if (request.ProfileVisibility is < 0 or > 1 || request.AchievementsVisibility is < 0 or > 1 ||
            request.BadgesVisibility is < 0 or > 1 || request.DmUnlocks is < 0 or > 2)
            return BadRequest("Unknown setting value.");

        var saved = await service.UpdateUserSettingsAsync(userId, row =>
        {
            if (request.ProfileVisibility is { } profile)
                row.ProfileVisibility = profile;
            if (request.AchievementsVisibility is { } achievements)
                row.AchievementsVisibility = achievements;
            if (request.BadgesVisibility is { } badges)
                row.BadgesVisibility = badges;
            if (request.HideFromLeaderboards is { } hide)
                row.HideFromLeaderboards = hide;
            if (request.DmUnlocks is { } dm)
                row.DmUnlocks = dm;
            if (request.ShowInLog is { } log)
                row.ShowInLog = log;
            if (request.MentionMe is { } mention)
                row.MentionMe = mention;
        });
        return Ok(MapSettings(saved));
    }

    /// <summary>
    ///     Replaces the member's four badge slots.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The member.</param>
    /// <param name="request">Badge keys for slots 1 to 4.</param>
    /// <returns>The saved slots.</returns>
    [HttpPut("badges")]
    public async Task<IActionResult> SetBadges(ulong guildId, ulong userId, [FromBody] AchievementBadgeSlotsRequest request)
    {
        var (_, error) = await AuthorizeAsync(guildId, userId);
        if (error is not null)
            return error;

        if (request.Slots.Count > AchievementCatalog.BadgeSlots)
            return BadRequest($"There are only {AchievementCatalog.BadgeSlots} slots.");

        var saved = await service.SetBadgeSlotsAsync(guildId, userId, request.Slots);
        return Ok(new
        {
            equipped = saved
        });
    }

    private async Task<(SocketGuild? Guild, IActionResult? Error)> AuthorizeAsync(ulong guildId, ulong userId)
    {
        var caller = await HttpContext.GetDashboardUserIdAsync();
        if (caller is null)
            return (null, Unauthorized());
        if (caller != userId)
            return (null, StatusCode(403, "You can only see your own achievements here."));

        var guild = client.GetGuild(guildId);
        if (guild?.GetUser(userId) is null)
            return (null, StatusCode(403, "You are not a member of this server."));
        return (guild, null);
    }

    private static AchievementUserSettingsResponse MapSettings(DataModel.AchievementUserSetting row)
    {
        return new AchievementUserSettingsResponse
        {
            ProfileVisibility = row.ProfileVisibility,
            AchievementsVisibility = row.AchievementsVisibility,
            BadgesVisibility = row.BadgesVisibility,
            HideFromLeaderboards = row.HideFromLeaderboards,
            DmUnlocks = row.DmUnlocks,
            ShowInLog = row.ShowInLog,
            MentionMe = row.MentionMe
        };
    }
}
