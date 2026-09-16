using DataModel;
using Mewdeko.Modules.StatRoles.Common;
using Mewdeko.Modules.StatRoles.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     Stat roles for the dashboard: roles granted and removed on a schedule based on activity.
/// </summary>
[ApiController]
[Route("botapi/[controller]/{guildId}")]
[Authorize("ApiKeyPolicy")]
public class StatRolesController : Controller
{
    private readonly IDashboardAuditContext auditContext;
    private readonly DiscordShardedClient client;
    private readonly StatRoleService service;

    /// <summary>
    ///     Initializes a new instance of the <see cref="StatRolesController" /> class.
    /// </summary>
    /// <param name="service">The stat role service.</param>
    /// <param name="client">The Discord client.</param>
    /// <param name="auditContext">Records before/after state for the dashboard audit log.</param>
    public StatRolesController(StatRoleService service, DiscordShardedClient client,
        IDashboardAuditContext auditContext)
    {
        this.service = service;
        this.client = client;
        this.auditContext = auditContext;
    }

    private static object Shape(StatRole r)
    {
        return new
        {
            r.Id,
            r.RoleId,
            r.Name,
            r.Enabled,
            StatType = (StatRoleStat)r.StatType,
            LimitType = (StatRoleLimit)r.LimitType,
            r.Minimum,
            r.Maximum,
            r.LookbackDays,
            r.TopStart,
            r.TopEnd,
            r.RequiredDays,
            r.Permanent,
            r.Invert,
            r.ApplyToBots,
            r.GroupName,
            r.ActivityName,
            ChannelFilter = StatRoleService.ReadIds(r.ChannelFilter),
            RoleWhitelist = StatRoleService.ReadIds(r.RoleWhitelist),
            RoleBlacklist = StatRoleService.ReadIds(r.RoleBlacklist),
            IgnoredUsers = StatRoleService.ReadIds(r.IgnoredUsers),
            r.NotifyChannelId,
            r.NotifyDm,
            r.NotifyMessage,
            r.IntervalMinutes,
            r.LastRunAt,
            Condition = StatRoleEmbeds.Condition(r)
        };
    }

    private static void Apply(StatRole r, StatRoleRequest request)
    {
        if (request.RoleId.HasValue) r.RoleId = request.RoleId.Value;
        if (request.Name != null) r.Name = request.Name;
        if (request.Enabled.HasValue) r.Enabled = request.Enabled.Value;
        if (request.StatType.HasValue) r.StatType = (int)request.StatType.Value;
        if (request.LimitType.HasValue) r.LimitType = (int)request.LimitType.Value;
        if (request.Minimum.HasValue) r.Minimum = request.Minimum.Value;
        if (request.ClearMaximum) r.Maximum = null;
        else if (request.Maximum.HasValue) r.Maximum = request.Maximum.Value;
        if (request.LookbackDays.HasValue) r.LookbackDays = request.LookbackDays.Value;
        if (request.TopStart.HasValue) r.TopStart = request.TopStart.Value;
        if (request.TopEnd.HasValue) r.TopEnd = request.TopEnd.Value;
        if (request.RequiredDays.HasValue) r.RequiredDays = request.RequiredDays.Value;
        if (request.Permanent.HasValue) r.Permanent = request.Permanent.Value;
        if (request.Invert.HasValue) r.Invert = request.Invert.Value;
        if (request.ApplyToBots.HasValue) r.ApplyToBots = request.ApplyToBots.Value;
        if (request.GroupName != null) r.GroupName = request.GroupName.Length == 0 ? null : request.GroupName;
        if (request.ActivityName != null)
            r.ActivityName = request.ActivityName.Length == 0 ? null : request.ActivityName;
        if (request.ChannelFilter != null) r.ChannelFilter = StatRoleService.WriteIds(request.ChannelFilter);
        if (request.RoleWhitelist != null) r.RoleWhitelist = StatRoleService.WriteIds(request.RoleWhitelist);
        if (request.RoleBlacklist != null) r.RoleBlacklist = StatRoleService.WriteIds(request.RoleBlacklist);
        if (request.IgnoredUsers != null) r.IgnoredUsers = StatRoleService.WriteIds(request.IgnoredUsers);
        if (request.ClearNotifyChannel) r.NotifyChannelId = null;
        else if (request.NotifyChannelId.HasValue) r.NotifyChannelId = request.NotifyChannelId.Value;
        if (request.NotifyDm.HasValue) r.NotifyDm = request.NotifyDm.Value;
        if (request.NotifyMessage != null)
            r.NotifyMessage = request.NotifyMessage.Length == 0 ? null : request.NotifyMessage;
        if (request.IntervalMinutes.HasValue) r.IntervalMinutes = request.IntervalMinutes.Value;
    }

    /// <summary>
    ///     Lists stat roles.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> List(ulong guildId)
    {
        return Ok((await service.GetAsync(guildId)).Select(Shape));
    }

    /// <summary>
    ///     Gets one stat role.
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(ulong guildId, int id)
    {
        var role = await service.GetAsync(guildId, id);
        return role == null ? NotFound() : Ok(Shape(role));
    }

    /// <summary>
    ///     Creates a stat role.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create(ulong guildId, [FromBody] StatRoleRequest? request)
    {
        if (request == null)
            return BadRequest("The request body is missing or is not valid JSON for this endpoint");
        if (!request.RoleId.HasValue)
            return BadRequest("RoleId is required");

        var role = new StatRole
        {
            GuildId = guildId
        };
        Apply(role, request);

        var created = await service.CreateAsync(role);
        if (created == null)
            return Conflict("That role is already managed by a stat role");

        auditContext.RecordAfter(created);
        return Ok(Shape(created));
    }

    /// <summary>
    ///     Updates a stat role.
    /// </summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(ulong guildId, int id, [FromBody] StatRoleRequest? request)
    {
        if (request == null)
            return BadRequest("The request body is missing or is not valid JSON for this endpoint");

        var role = await service.GetAsync(guildId, id);
        if (role == null)
            return NotFound();

        auditContext.RecordBefore(Shape(role));
        Apply(role, request);
        await service.UpdateAsync(role);
        auditContext.RecordAfter(Shape(role));
        return Ok(Shape(role));
    }

    /// <summary>
    ///     Deletes a stat role.
    /// </summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(ulong guildId, int id)
    {
        auditContext.RecordBefore(await service.GetAsync(guildId, id));
        return Ok(await service.DeleteAsync(guildId, id));
    }

    /// <summary>
    ///     Previews who would gain and lose the role.
    /// </summary>
    [HttpPost("{id:int}/preview")]
    public async Task<IActionResult> Preview(ulong guildId, int id)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound("Guild not found");

        var role = await service.GetAsync(guildId, id);
        if (role == null)
            return NotFound();

        var result = await service.RunAsync(guild, role, true);
        return Ok(ShapeResult(guild, result));
    }

    /// <summary>
    ///     Evaluates and applies the role now.
    /// </summary>
    [HttpPost("{id:int}/run")]
    public async Task<IActionResult> Run(ulong guildId, int id)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound("Guild not found");

        var role = await service.GetAsync(guildId, id);
        if (role == null)
            return NotFound();

        if (!service.TryReserveManualRun(guildId))
            return StatusCode(429, "A manual run happened recently");

        var result = await service.RunAsync(guild, role, false);
        return Ok(ShapeResult(guild, result));
    }

    private static object ShapeResult(SocketGuild guild, StatRoleRunResult result)
    {
        object Member(StatRoleMember m)
        {
            var user = guild.GetUser(m.UserId);
            return new
            {
                m.UserId,
                m.Value,
                m.Rank,
                user?.Username,
                AvatarUrl = user?.GetAvatarUrl()
            };
        }

        return new
        {
            result.StatRoleId,
            result.RoleId,
            QualifyingCount = result.Qualifying.Count,
            Qualifying = result.Qualifying.Take(100).Select(Member),
            ToGrant = result.ToGrant.Take(100).Select(Member),
            ToRemove = result.ToRemove.Take(100).Select(Member),
            result.Granted,
            result.Removed,
            result.Failed
        };
    }

    /// <summary>
    ///     A stat role create or update. Every field is optional on update.
    /// </summary>
    public class StatRoleRequest
    {
        /// <summary>The Discord role to manage.</summary>
        public ulong? RoleId { get; set; }

        /// <summary>A display name.</summary>
        public string? Name { get; set; }

        /// <summary>Whether the stat role runs.</summary>
        public bool? Enabled { get; set; }

        /// <summary>What to measure.</summary>
        public StatRoleStat? StatType { get; set; }

        /// <summary>How members qualify.</summary>
        public StatRoleLimit? LimitType { get; set; }

        /// <summary>Threshold or per day minimum.</summary>
        public long? Minimum { get; set; }

        /// <summary>Threshold maximum.</summary>
        public long? Maximum { get; set; }

        /// <summary>True to clear the maximum.</summary>
        public bool ClearMaximum { get; set; }

        /// <summary>The window in days, or 0 for all time.</summary>
        public int? LookbackDays { get; set; }

        /// <summary>Best rank or percentile that qualifies.</summary>
        public int? TopStart { get; set; }

        /// <summary>Worst rank or percentile that qualifies.</summary>
        public int? TopEnd { get; set; }

        /// <summary>Days that must meet the per day minimum.</summary>
        public int? RequiredDays { get; set; }

        /// <summary>Whether the role is never removed once earned.</summary>
        public bool? Permanent { get; set; }

        /// <summary>Whether members who do NOT qualify get the role.</summary>
        public bool? Invert { get; set; }

        /// <summary>Whether bots are considered.</summary>
        public bool? ApplyToBots { get; set; }

        /// <summary>The group name; empty string clears it.</summary>
        public string? GroupName { get; set; }

        /// <summary>For ActivityMinutes: the game or app to measure; empty string means any.</summary>
        public string? ActivityName { get; set; }

        /// <summary>Channels the stat is limited to.</summary>
        public List<ulong>? ChannelFilter { get; set; }

        /// <summary>Roles a member needs one of.</summary>
        public List<ulong>? RoleWhitelist { get; set; }

        /// <summary>Roles a member must not hold.</summary>
        public List<ulong>? RoleBlacklist { get; set; }

        /// <summary>Members never touched.</summary>
        public List<ulong>? IgnoredUsers { get; set; }

        /// <summary>The announcement channel.</summary>
        public ulong? NotifyChannelId { get; set; }

        /// <summary>True to stop announcing in a channel.</summary>
        public bool ClearNotifyChannel { get; set; }

        /// <summary>Whether members are messaged directly.</summary>
        public bool? NotifyDm { get; set; }

        /// <summary>The notification template; empty string resets it.</summary>
        public string? NotifyMessage { get; set; }

        /// <summary>How often the role is evaluated.</summary>
        public int? IntervalMinutes { get; set; }
    }
}