using System.Globalization;
using System.Text;
using Mewdeko.Modules.Utility.Common;
using Mewdeko.Modules.Utility.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     Controller for managing invite tracking and statistics
/// </summary>
[ApiController]
[Route("botapi/[controller]/{guildId}")]
[Authorize("ApiKeyPolicy")]
public class InviteTrackingController : Controller
{
    private readonly IDashboardAuditContext auditContext;
    private readonly DiscordShardedClient client;
    private readonly InviteCountService inviteService;

    /// <summary>
    ///     Initializes a new instance of the InviteTrackingController
    /// </summary>
    /// <param name="inviteService">The inviteservice service.</param>
    /// <param name="client">The Discord client instance.</param>
    /// <param name="auditContext">Records before/after state for the dashboard audit log.</param>
    public InviteTrackingController(
        InviteCountService inviteService,
        DiscordShardedClient client,
        IDashboardAuditContext auditContext)
    {
        this.inviteService = inviteService;
        this.client = client;
        this.auditContext = auditContext;
    }

    /// <summary>
    ///     Gets invite tracking settings for a guild
    /// </summary>
    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings(ulong guildId)
    {
        var settings = await inviteService.GetInviteCountSettingsAsync(guildId);
        return Ok(settings);
    }

    /// <summary>
    ///     Enables or disables invite tracking for a guild
    /// </summary>
    [HttpPost("toggle")]
    public async Task<IActionResult> ToggleInviteTracking(ulong guildId, [FromBody] bool enabled)
    {
        auditContext.RecordBefore(await inviteService.GetInviteCountSettingsAsync(guildId));
        var result = await inviteService.SetInviteTrackingEnabledAsync(guildId, enabled);
        auditContext.RecordAfter(await inviteService.GetInviteCountSettingsAsync(guildId));
        return Ok(result);
    }

    /// <summary>
    ///     Sets whether invites should be removed when users leave
    /// </summary>
    [HttpPost("remove-on-leave")]
    public async Task<IActionResult> SetRemoveOnLeave(ulong guildId, [FromBody] bool removeOnLeave)
    {
        auditContext.RecordBefore(await inviteService.GetInviteCountSettingsAsync(guildId));
        var result = await inviteService.SetRemoveInviteOnLeaveAsync(guildId, removeOnLeave);
        auditContext.RecordAfter(await inviteService.GetInviteCountSettingsAsync(guildId));
        return Ok(result);
    }

    /// <summary>
    ///     Sets minimum account age for invite counting
    /// </summary>
    [HttpPost("min-age")]
    public async Task<IActionResult> SetMinAccountAge(ulong guildId, [FromBody] string minAge)
    {
        if (!TryParseAge(minAge, out var timeSpan))
            return BadRequest("Use a TimeSpan such as 3.00:00:00, a number of days, or 3d");

        auditContext.RecordBefore(await inviteService.GetInviteCountSettingsAsync(guildId));
        var result = await inviteService.SetMinAccountAgeAsync(guildId, timeSpan);
        auditContext.RecordAfter(await inviteService.GetInviteCountSettingsAsync(guildId));
        return Ok(result.ToString());
    }

    /// <summary>
    ///     Accepts a TimeSpan string, a plain number of days, or a number suffixed with d/h/m.
    /// </summary>
    private static bool TryParseAge(string? text, out TimeSpan result)
    {
        result = TimeSpan.Zero;
        text = text?.Trim() ?? "";
        if (text.Length == 0)
            return false;

        if (TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out result))
            return result >= TimeSpan.Zero;

        if (double.TryParse(text, NumberStyles.Float,
                CultureInfo.InvariantCulture, out var days))
        {
            result = TimeSpan.FromDays(days);
            return days >= 0;
        }

        var unit = char.ToLowerInvariant(text[^1]);
        if (!double.TryParse(text[..^1], NumberStyles.Float,
                CultureInfo.InvariantCulture, out var amount) || amount < 0)
            return false;

        result = unit switch
        {
            'd' => TimeSpan.FromDays(amount),
            'h' => TimeSpan.FromHours(amount),
            'm' => TimeSpan.FromMinutes(amount),
            _ => TimeSpan.MinValue
        };
        return result != TimeSpan.MinValue;
    }

    /// <summary>
    ///     Sets whether rejoining members earn a regular invite or are flagged as fake
    /// </summary>
    [HttpPost("count-rejoins")]
    public async Task<IActionResult> SetCountRejoins(ulong guildId, [FromBody] bool countRejoins)
    {
        auditContext.RecordBefore(await inviteService.GetInviteCountSettingsAsync(guildId));
        var result = await inviteService.SetCountRejoinsAsync(guildId, countRejoins);
        auditContext.RecordAfter(await inviteService.GetInviteCountSettingsAsync(guildId));
        return Ok(result);
    }

    /// <summary>
    ///     Sets whether members without an avatar are flagged as fake
    /// </summary>
    [HttpPost("fake-no-avatar")]
    public async Task<IActionResult> SetFakeOnNoAvatar(ulong guildId, [FromBody] bool enabled)
    {
        auditContext.RecordBefore(await inviteService.GetInviteCountSettingsAsync(guildId));
        var result = await inviteService.SetFakeOnNoAvatarAsync(guildId, enabled);
        auditContext.RecordAfter(await inviteService.GetInviteCountSettingsAsync(guildId));
        return Ok(result);
    }

    /// <summary>
    ///     Sets the channel personal invite links point at (null for the system channel)
    /// </summary>
    [HttpPost("link-channel")]
    public async Task<IActionResult> SetLinkChannel(ulong guildId, [FromBody] ulong? channelId)
    {
        auditContext.RecordBefore(await inviteService.GetInviteCountSettingsAsync(guildId));
        await inviteService.SetLinkChannelAsync(guildId, channelId);
        auditContext.RecordAfter(await inviteService.GetInviteCountSettingsAsync(guildId));
        return Ok();
    }

    /// <summary>
    ///     Sets the channel that receives join and leave attribution embeds (null to disable)
    /// </summary>
    [HttpPost("log-channel")]
    public async Task<IActionResult> SetLogChannel(ulong guildId, [FromBody] ulong? channelId)
    {
        auditContext.RecordBefore(await inviteService.GetInviteCountSettingsAsync(guildId));
        await inviteService.SetLogChannelAsync(guildId, channelId);
        auditContext.RecordAfter(await inviteService.GetInviteCountSettingsAsync(guildId));
        return Ok();
    }

    /// <summary>
    ///     Gets the net invite count for a specific user
    /// </summary>
    [HttpGet("count/{userId}")]
    public async Task<IActionResult> GetInviteCount(ulong guildId, ulong userId)
    {
        var count = await inviteService.GetInviteCount(userId, guildId);
        return Ok(count);
    }

    /// <summary>
    ///     Gets the full invite breakdown and rank for a user
    /// </summary>
    [HttpGet("breakdown/{userId}")]
    public async Task<IActionResult> GetBreakdown(ulong guildId, ulong userId)
    {
        var row = await inviteService.GetInviteBreakdownAsync(guildId, userId);
        var rank = await inviteService.GetRankAsync(guildId, userId);
        return Ok(new
        {
            row.UserId,
            Total = row.Count,
            row.Regular,
            row.Left,
            row.Fake,
            row.Bonus,
            Rank = rank
        });
    }

    /// <summary>
    ///     Adjusts a user's invite tally. Deltas may be negative.
    /// </summary>
    [HttpPost("adjust/{userId}")]
    public async Task<IActionResult> Adjust(ulong guildId, ulong userId, [FromBody] InviteAdjustRequest? request)
    {
        if (request == null)
            return BadRequest("The request body is missing or is not valid JSON for this endpoint");

        auditContext.RecordBefore(await inviteService.GetInviteBreakdownAsync(guildId, userId));
        var row = await inviteService.AdjustInvitesAsync(guildId, userId, request.Regular, request.Bonus,
            request.Fake);
        auditContext.RecordAfter(row);
        return Ok(new
        {
            row.UserId,
            Total = row.Count,
            row.Regular,
            row.Left,
            row.Fake,
            row.Bonus
        });
    }

    /// <summary>
    ///     Resets a user's invites
    /// </summary>
    [HttpDelete("count/{userId}")]
    public async Task<IActionResult> ResetUser(ulong guildId, ulong userId)
    {
        auditContext.RecordBefore(await inviteService.GetInviteBreakdownAsync(guildId, userId));
        var result = await inviteService.ResetInvitesAsync(guildId, userId);
        return Ok(result);
    }

    /// <summary>
    ///     Resets invites for the whole guild or for inviters who left
    /// </summary>
    [HttpPost("reset")]
    public async Task<IActionResult> Reset(ulong guildId, [FromBody] InviteResetScope scope)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound("Guild not found");

        var count = await inviteService.ResetInvitesAsync(guild, scope);
        return Ok(count);
    }

    /// <summary>
    ///     Imports invite use counts from Discord
    /// </summary>
    [HttpPost("sync")]
    public async Task<IActionResult> Sync(ulong guildId, [FromQuery] ulong? userId = null)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound("Guild not found");

        var raised = await inviteService.SyncInvitesAsync(guild, userId);
        return Ok(raised);
    }

    /// <summary>
    ///     Gets who invited a specific user
    /// </summary>
    [HttpGet("inviter/{userId}")]
    public async Task<IActionResult> GetInviter(ulong guildId, ulong userId)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound("Guild not found");

        var record = await inviteService.GetJoinRecordAsync(guildId, userId);
        if (record == null)
            return NotFound("Join not witnessed");

        var inviter = await inviteService.GetInviter(userId, guild);
        return Ok(new
        {
            Inviter = inviter == null
                ? null
                : new
                {
                    inviter.Id, inviter.Username, inviter.Discriminator, AvatarUrl = inviter.GetAvatarUrl()
                },
            record.InviteCode,
            JoinType = ((InviteJoinType)record.JoinType).ToString(),
            record.IsFake,
            FakeReason = ((InviteFakeReason)record.FakeReason).ToString(),
            JoinedAt = record.DateAdded,
            record.LeftAt
        });
    }

    /// <summary>
    ///     Gets witnessed joins, filtered by inviter, code or label
    /// </summary>
    [HttpGet("invited")]
    public async Task<IActionResult> GetInvited(ulong guildId, [FromQuery] ulong? inviterId = null,
        [FromQuery] string? code = null, [FromQuery] string? label = null, [FromQuery] bool includeLeft = true,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var records = await inviteService.GetInvitedRecordsAsync(guildId, inviterId, code, label, includeLeft);
        var guild = client.GetGuild(guildId);

        var items = records.Skip((page - 1) * pageSize).Take(pageSize).Select(record =>
        {
            var user = guild?.GetUser(record.UserId);
            return new
            {
                record.UserId,
                user?.Username,
                AvatarUrl = user?.GetAvatarUrl(),
                record.InviterId,
                record.InviteCode,
                JoinType = ((InviteJoinType)record.JoinType).ToString(),
                record.IsFake,
                FakeReason = ((InviteFakeReason)record.FakeReason).ToString(),
                JoinedAt = record.DateAdded,
                record.LeftAt
            };
        });

        return Ok(new
        {
            Total = records.Count, Page = page, PageSize = pageSize, Items = items
        });
    }

    /// <summary>
    ///     Gets all users invited by a specific user who are still present
    /// </summary>
    [HttpGet("invited/{userId}")]
    public async Task<IActionResult> GetInvitedUsers(ulong guildId, ulong userId)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound("Guild not found");

        var invitedUsers = await inviteService.GetInvitedUsers(userId, guild);
        return Ok(invitedUsers.Select(user => new
        {
            user.Id, user.Username, user.Discriminator, AvatarUrl = user.GetAvatarUrl()
        }));
    }

    /// <summary>
    ///     Gets the invite leaderboard for a guild
    /// </summary>
    [HttpGet("leaderboard")]
    public async Task<IActionResult> GetLeaderboard(ulong guildId, [FromQuery] StatsRange range = StatsRange.AllTime,
        [FromQuery] ulong? roleId = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 25)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound("Guild not found");

        var leaderboard = await inviteService.GetInviteLeaderboardAsync(guild, range, roleId, page * pageSize);
        return Ok(leaderboard.Skip((page - 1) * pageSize).Take(pageSize).Select((entry, i) => new
        {
            Rank = (page - 1) * pageSize + i + 1,
            entry.UserId,
            entry.Username,
            AvatarUrl = guild.GetUser(entry.UserId)?.GetAvatarUrl(),
            entry.Total,
            entry.Regular,
            entry.Left,
            entry.Fake,
            entry.Bonus,
            entry.Retention,
            entry.LatestJoinAt
        }));
    }

    /// <summary>
    ///     Gets growth analytics for a window
    /// </summary>
    [HttpGet("analytics")]
    public async Task<IActionResult> GetAnalytics(ulong guildId, [FromQuery] StatsRange range = StatsRange.Monthly)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound("Guild not found");

        var analytics = await inviteService.GetAnalyticsAsync(guild, range);
        var days = range switch
        {
            StatsRange.Daily => 1,
            StatsRange.Weekly => 7,
            StatsRange.Monthly => 30,
            _ => 90
        };
        var series = await inviteService.GetDailyGrowthAsync(guildId, days);

        return Ok(new
        {
            analytics.Range,
            analytics.Joins,
            analytics.Leaves,
            analytics.NetGrowth,
            analytics.FakeJoins,
            analytics.Stayed,
            analytics.Retention,
            Sources = new
            {
                Invite = analytics.ViaInvite, Vanity = analytics.ViaVanity, Bot = analytics.ViaBot, analytics.Unknown
            },
            analytics.TopCodes,
            analytics.TopInviters,
            Series = series.Select(x => new
            {
                x.Day, x.Joins, x.Leaves
            })
        });
    }

    /// <summary>
    ///     Gets the invite codes in the guild, with labels and owners
    /// </summary>
    [HttpGet("codes")]
    public async Task<IActionResult> GetCodes(ulong guildId)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound("Guild not found");

        var invites = await guild.GetInvitesAsync();
        var labels = (await inviteService.GetLabelsAsync(guildId)).ToDictionary(x => x.InviteCode);

        return Ok(invites.Select(invite =>
        {
            labels.TryGetValue(invite.Code, out var label);
            return new
            {
                invite.Code,
                invite.Url,
                invite.ChannelId,
                InviterId = invite.Inviter?.Id,
                InviterName = invite.Inviter?.Username,
                Uses = invite.Uses ?? 0,
                invite.MaxUses,
                invite.MaxAge,
                invite.IsTemporary,
                invite.CreatedAt,
                label?.Label,
                LabelRoleId = label?.RoleId,
                label?.OwnerUserId
            };
        }));
    }

    /// <summary>
    ///     Deletes an invite code
    /// </summary>
    [HttpDelete("codes/{code}")]
    public async Task<IActionResult> DeleteCode(ulong guildId, string code)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound("Guild not found");

        return Ok(await inviteService.DeleteInviteAsync(guild, code));
    }

    /// <summary>
    ///     Gets the labels in the guild
    /// </summary>
    [HttpGet("labels")]
    public async Task<IActionResult> GetLabels(ulong guildId)
    {
        return Ok(await inviteService.GetLabelsAsync(guildId));
    }

    /// <summary>
    ///     Creates or updates a label
    /// </summary>
    [HttpPut("labels")]
    public async Task<IActionResult> SetLabel(ulong guildId, [FromBody] InviteLabelRequest? request)
    {
        if (request == null)
            return BadRequest("The request body is missing or is not valid JSON for this endpoint");
        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Label))
            return BadRequest("Code and label are required");
        if (request.Label.Length > 64)
            return BadRequest("Label too long");

        auditContext.RecordBefore(await inviteService.GetLabelAsync(guildId, request.Code));
        var label = await inviteService.SetLabelAsync(guildId, request.Code, request.Label, request.RoleId,
            request.OwnerUserId);
        auditContext.RecordAfter(label);
        return Ok(label);
    }

    /// <summary>
    ///     Removes a label
    /// </summary>
    [HttpDelete("labels/{code}")]
    public async Task<IActionResult> RemoveLabel(ulong guildId, string code)
    {
        auditContext.RecordBefore(await inviteService.GetLabelAsync(guildId, code));
        return Ok(await inviteService.RemoveLabelAsync(guildId, code));
    }

    /// <summary>
    ///     Gets exclusions of a kind
    /// </summary>
    [HttpGet("exclusions/{kind}")]
    public async Task<IActionResult> GetExclusions(ulong guildId, InviteExclusionKind kind)
    {
        return Ok(await inviteService.GetExclusionsAsync(guildId, kind));
    }

    /// <summary>
    ///     Adds an exclusion
    /// </summary>
    [HttpPost("exclusions/{kind}/{targetId}")]
    public async Task<IActionResult> AddExclusion(ulong guildId, InviteExclusionKind kind, ulong targetId)
    {
        return Ok(await inviteService.AddExclusionAsync(guildId, targetId, kind));
    }

    /// <summary>
    ///     Removes an exclusion
    /// </summary>
    [HttpDelete("exclusions/{kind}/{targetId}")]
    public async Task<IActionResult> RemoveExclusion(ulong guildId, InviteExclusionKind kind, ulong targetId)
    {
        return Ok(await inviteService.RemoveExclusionAsync(guildId, targetId, kind));
    }

    /// <summary>
    ///     Exports the leaderboard as CSV
    /// </summary>
    [HttpGet("export/leaderboard")]
    public async Task<IActionResult> ExportLeaderboard(ulong guildId,
        [FromQuery] StatsRange range = StatsRange.AllTime)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound("Guild not found");

        var csv = await inviteService.ExportLeaderboardCsvAsync(guild, range);
        return File(Encoding.UTF8.GetBytes(csv), "text/csv", $"invites-{range}.csv");
    }

    /// <summary>
    ///     Exports witnessed joins as CSV
    /// </summary>
    [HttpGet("export/invited")]
    public async Task<IActionResult> ExportInvited(ulong guildId, [FromQuery] ulong? inviterId = null,
        [FromQuery] string? code = null, [FromQuery] string? label = null)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound("Guild not found");

        var csv = await inviteService.ExportInvitedListCsvAsync(guild, inviterId, code, label);
        return File(Encoding.UTF8.GetBytes(csv), "text/csv", "invited.csv");
    }

    /// <summary>
    ///     A manual invite adjustment.
    /// </summary>
    public class InviteAdjustRequest
    {
        /// <summary>
        ///     Change to regular invites.
        /// </summary>
        public int Regular { get; set; }

        /// <summary>
        ///     Change to bonus invites.
        /// </summary>
        public int Bonus { get; set; }

        /// <summary>
        ///     Change to fake invites.
        /// </summary>
        public int Fake { get; set; }
    }

    /// <summary>
    ///     A label create or update.
    /// </summary>
    public class InviteLabelRequest
    {
        /// <summary>
        ///     The invite code or URL.
        /// </summary>
        public string Code { get; set; } = "";

        /// <summary>
        ///     The label text.
        /// </summary>
        public string Label { get; set; } = "";

        /// <summary>
        ///     A role granted on join, or null.
        /// </summary>
        public ulong? RoleId { get; set; }

        /// <summary>
        ///     The member credited for joins, or null for the code's creator.
        /// </summary>
        public ulong? OwnerUserId { get; set; }
    }
}