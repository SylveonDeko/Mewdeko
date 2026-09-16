using Mewdeko.Modules.ServerStats.Common;
using Mewdeko.Modules.ServerStats.Services;
using Mewdeko.Modules.Utility.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     Live boards and server reports for the dashboard.
/// </summary>
[ApiController]
[Route("botapi/[controller]/{guildId}")]
[Authorize("ApiKeyPolicy")]
public class LiveBoardsController : Controller
{
    private readonly IDashboardAuditContext auditContext;
    private readonly LiveBoardService boards;
    private readonly DiscordShardedClient client;
    private readonly ServerReportService reports;

    /// <summary>
    ///     Initializes a new instance of the <see cref="LiveBoardsController" /> class.
    /// </summary>
    /// <param name="boards">The live board service.</param>
    /// <param name="reports">The server report service.</param>
    /// <param name="client">The Discord client.</param>
    /// <param name="auditContext">Records before/after state for the dashboard audit log.</param>
    public LiveBoardsController(LiveBoardService boards, ServerReportService reports, DiscordShardedClient client,
        IDashboardAuditContext auditContext)
    {
        this.boards = boards;
        this.reports = reports;
        this.client = client;
        this.auditContext = auditContext;
    }

    /// <summary>
    ///     Lists live boards.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> List(ulong guildId)
    {
        return Ok((await boards.GetAsync(guildId)).Select(b => new
        {
            b.Id,
            b.ChannelId,
            b.MessageId,
            Kind = (LiveBoardKind)b.Kind,
            Range = (StatsRange)b.Range,
            b.Pin,
            b.Entries,
            b.IntervalMinutes,
            b.LastUpdateAt
        }));
    }

    /// <summary>
    ///     Creates a live board and posts its first message.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create(ulong guildId, [FromBody] LiveBoardRequest? request)
    {
        if (request == null)
            return BadRequest("The request body is missing or is not valid JSON for this endpoint");

        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound("Guild not found");
        if (guild.GetTextChannel(request.ChannelId) is not { } channel)
            return BadRequest("Channel not found");
        if ((await boards.GetAsync(guildId)).Count >= 10)
            return BadRequest("A guild may have at most 10 live boards");

        var board = await boards.CreateAsync(guild, channel, request.Kind, request.Range, request.Pin,
            request.Entries, request.IntervalMinutes);
        auditContext.RecordAfter(board);
        return Ok(board);
    }

    /// <summary>
    ///     Deletes a live board and its message.
    /// </summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(ulong guildId, int id)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound("Guild not found");

        return Ok(await boards.DeleteAsync(guild, id));
    }

    /// <summary>
    ///     Refreshes every live board now.
    /// </summary>
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(ulong guildId)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound("Guild not found");

        var list = await boards.GetAsync(guildId);
        foreach (var board in list)
            await boards.RefreshAsync(guild, board);
        return Ok(list.Count);
    }

    /// <summary>
    ///     Gets the server report settings.
    /// </summary>
    [HttpGet("report")]
    public async Task<IActionResult> GetReport(ulong guildId)
    {
        var settings = await reports.GetSettingsAsync(guildId);
        return Ok(new
        {
            settings.Enabled, settings.ChannelId, Frequency = (ReportFrequency)settings.Frequency, settings.LastSentAt
        });
    }

    /// <summary>
    ///     Updates the server report settings.
    /// </summary>
    [HttpPut("report")]
    public async Task<IActionResult> UpdateReport(ulong guildId, [FromBody] ServerReportRequest? request)
    {
        if (request == null)
            return BadRequest("The request body is missing or is not valid JSON for this endpoint");

        auditContext.RecordBefore(await reports.GetSettingsAsync(guildId));
        var updated = await reports.UpdateSettingsAsync(guildId, s =>
        {
            if (request.ClearChannel) s.ChannelId = null;
            else if (request.ChannelId.HasValue) s.ChannelId = request.ChannelId.Value;
            if (request.Frequency.HasValue) s.Frequency = (int)request.Frequency.Value;
            if (request.Enabled.HasValue) s.Enabled = request.Enabled.Value;
        });
        auditContext.RecordAfter(updated);
        return Ok(updated);
    }

    /// <summary>
    ///     Posts a report to the configured channel now.
    /// </summary>
    [HttpPost("report/send")]
    public async Task<IActionResult> SendReport(ulong guildId)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound("Guild not found");

        var settings = await reports.GetSettingsAsync(guildId);
        return Ok(await reports.SendAsync(guild, settings));
    }

    /// <summary>
    ///     A live board create request.
    /// </summary>
    public class LiveBoardRequest
    {
        /// <summary>The channel to post in.</summary>
        public ulong ChannelId { get; set; }

        /// <summary>What to show.</summary>
        public LiveBoardKind Kind { get; set; }

        /// <summary>The window.</summary>
        public StatsRange Range { get; set; } = StatsRange.Weekly;

        /// <summary>Whether to pin the message.</summary>
        public bool Pin { get; set; } = true;

        /// <summary>Rows for leaderboards.</summary>
        public int Entries { get; set; } = 10;

        /// <summary>How often to refresh.</summary>
        public int IntervalMinutes { get; set; } = 15;
    }

    /// <summary>
    ///     A server report settings update.
    /// </summary>
    public class ServerReportRequest
    {
        /// <summary>The channel to post in.</summary>
        public ulong? ChannelId { get; set; }

        /// <summary>True to clear the channel and disable reports.</summary>
        public bool ClearChannel { get; set; }

        /// <summary>How often to post.</summary>
        public ReportFrequency? Frequency { get; set; }

        /// <summary>Whether reports are posted.</summary>
        public bool? Enabled { get; set; }
    }
}