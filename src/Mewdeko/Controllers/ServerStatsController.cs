using System.Text;
using Mewdeko.Modules.ServerStats.Common;
using Mewdeko.Modules.ServerStats.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     Activity statistics for the dashboard: overviews, rankings, time series, tracking settings and exclusions.
/// </summary>
[ApiController]
[Route("botapi/[controller]/{guildId}")]
[Authorize("ApiKeyPolicy")]
public class ServerStatsController : Controller
{
    private readonly IDashboardAuditContext auditContext;
    private readonly DiscordShardedClient client;
    private readonly ServerStatsSettingsService settings;
    private readonly ServerStatsService stats;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ServerStatsController" /> class.
    /// </summary>
    /// <param name="stats">The stats query service.</param>
    /// <param name="settings">The stats settings service.</param>
    /// <param name="client">The Discord client.</param>
    /// <param name="auditContext">Records before/after state for the dashboard audit log.</param>
    public ServerStatsController(ServerStatsService stats, ServerStatsSettingsService settings,
        DiscordShardedClient client, IDashboardAuditContext auditContext)
    {
        this.stats = stats;
        this.settings = settings;
        this.client = client;
        this.auditContext = auditContext;
    }

    private SocketGuild? Guild(ulong guildId)
    {
        return client.GetGuild(guildId);
    }

    private object Named(SocketGuild guild, TopEntry entry, bool isUser)
    {
        if (isUser)
        {
            var user = guild.GetUser(entry.Id);
            return new
            {
                entry.Id,
                entry.Value,
                Name = user?.Username,
                user?.DisplayName,
                AvatarUrl = user?.GetAvatarUrl()
            };
        }

        return new
        {
            entry.Id, entry.Value, guild.GetChannel(entry.Id)?.Name
        };
    }

    /// <summary>
    ///     Gets the guild wide overview for a window.
    /// </summary>
    [HttpGet("overview")]
    public async Task<IActionResult> Overview(ulong guildId, [FromQuery] int? days = null)
    {
        var guild = Guild(guildId);
        if (guild == null)
            return NotFound("Guild not found");

        var lookback = stats.ResolveLookback(guildId, days);
        var o = await stats.GetOverviewAsync(guild, lookback);
        var snapshot = GuildSnapshotService.Sample(guild, DateTime.UtcNow);

        return Ok(new
        {
            o.LookbackDays,
            o.Messages,
            o.VoiceSeconds,
            o.MessageContributors,
            o.VoiceContributors,
            o.Joins,
            o.Leaves,
            NetGrowth = o.Joins - o.Leaves,
            TopMessageUser = o.TopMessageUser == null ? null : Named(guild, o.TopMessageUser, true),
            TopVoiceUser = o.TopVoiceUser == null ? null : Named(guild, o.TopVoiceUser, true),
            TopMessageChannel = o.TopMessageChannel == null ? null : Named(guild, o.TopMessageChannel, false),
            TopVoiceChannel = o.TopVoiceChannel == null ? null : Named(guild, o.TopVoiceChannel, false),
            Now = new
            {
                snapshot.Members,
                snapshot.Humans,
                snapshot.Bots,
                snapshot.Online,
                snapshot.Idle,
                snapshot.Dnd,
                snapshot.Offline,
                snapshot.InVoice
            }
        });
    }

    /// <summary>
    ///     Gets one member's activity for a window.
    /// </summary>
    [HttpGet("user/{userId}")]
    public async Task<IActionResult> UserActivity(ulong guildId, ulong userId, [FromQuery] int? days = null)
    {
        var guild = Guild(guildId);
        if (guild == null)
            return NotFound("Guild not found");

        var lookback = stats.ResolveLookback(guildId, days);
        var a = await stats.GetUserActivityAsync(guild, userId, lookback);
        return Ok(new
        {
            a.UserId,
            a.LookbackDays,
            a.Messages,
            a.VoiceSeconds,
            a.MessageRank,
            a.VoiceRank,
            a.AllTimeMessages,
            a.AllTimeVoiceSeconds,
            TopMessageChannels = a.TopMessageChannels.Select(x => Named(guild, x, false)),
            TopVoiceChannels = a.TopVoiceChannels.Select(x => Named(guild, x, false))
        });
    }

    /// <summary>
    ///     Gets one channel's activity for a window.
    /// </summary>
    [HttpGet("channel/{channelId}")]
    public async Task<IActionResult> Channel(ulong guildId, ulong channelId, [FromQuery] int? days = null)
    {
        var guild = Guild(guildId);
        if (guild == null)
            return NotFound("Guild not found");

        var lookback = stats.ResolveLookback(guildId, days);
        var a = await stats.GetChannelActivityAsync(guild, channelId, lookback);
        return Ok(new
        {
            a.ChannelId,
            a.LookbackDays,
            a.Messages,
            a.VoiceSeconds,
            a.Contributors,
            TopMessageUsers = a.TopMessageUsers.Select(x => Named(guild, x, true)),
            TopVoiceUsers = a.TopVoiceUsers.Select(x => Named(guild, x, true))
        });
    }

    /// <summary>
    ///     Ranks members by messages or voice time.
    /// </summary>
    [HttpGet("top/users")]
    public async Task<IActionResult> TopUsers(ulong guildId, [FromQuery] StatKind kind = StatKind.Messages,
        [FromQuery] int? days = null, [FromQuery] int limit = 50, [FromQuery] ulong? channelId = null)
    {
        var guild = Guild(guildId);
        if (guild == null)
            return NotFound("Guild not found");

        var lookback = stats.ResolveLookback(guildId, days);
        var entries = await stats.GetTopUsersAsync(guildId, kind, lookback, Math.Clamp(limit, 1, 500), channelId);
        return Ok(entries.Select((x, i) => new
        {
            Rank = i + 1, Entry = Named(guild, x, true)
        }));
    }

    /// <summary>
    ///     Ranks channels by messages or voice time.
    /// </summary>
    [HttpGet("top/channels")]
    public async Task<IActionResult> TopChannels(ulong guildId, [FromQuery] StatKind kind = StatKind.Messages,
        [FromQuery] int? days = null, [FromQuery] int limit = 50)
    {
        var guild = Guild(guildId);
        if (guild == null)
            return NotFound("Guild not found");

        var lookback = stats.ResolveLookback(guildId, days);
        var entries = await stats.GetTopChannelsAsync(guildId, kind, lookback, Math.Clamp(limit, 1, 500));
        return Ok(entries.Select((x, i) => new
        {
            Rank = i + 1, Entry = Named(guild, x, false)
        }));
    }

    /// <summary>
    ///     Ranks games and apps by time spent, guild wide or for one member.
    /// </summary>
    [HttpGet("top/activities")]
    public async Task<IActionResult> TopActivities(ulong guildId, [FromQuery] int? days = null,
        [FromQuery] int limit = 25, [FromQuery] ulong? userId = null)
    {
        var lookback = stats.ResolveLookback(guildId, days);
        var rows = await stats.GetTopActivitiesAsync(guildId, lookback, Math.Clamp(limit, 1, 100), userId);
        return Ok(rows.Select((x, i) => new
        {
            Rank = i + 1,
            x.Name,
            x.ApplicationId,
            Type = x.Type.ToString(),
            x.Seconds,
            x.Players,
            ActiveNow = stats.CountActiveNow(guildId, x.Name)
        }));
    }

    /// <summary>
    ///     Gets who plays one game or app: members active now and the members with the most time in it.
    /// </summary>
    [HttpGet("activity")]
    public async Task<IActionResult> Activity(ulong guildId, [FromQuery] string name, [FromQuery] int? days = null,
        [FromQuery] int limit = 25)
    {
        var guild = Guild(guildId);
        if (guild == null)
            return NotFound("Guild not found");

        var lookback = stats.ResolveLookback(guildId, days);
        var totals = await stats.GetPerUserActivitySecondsAsync(guildId, name, lookback);
        return Ok(new
        {
            Name = name,
            ActiveNow = stats.CountActiveNow(guildId, name),
            Players = totals.Count,
            TotalSeconds = totals.Values.Sum(),
            Top = totals.OrderByDescending(x => x.Value).Take(Math.Clamp(limit, 1, 100)).Select((x, i) => new
            {
                Rank = i + 1, Entry = Named(guild, new TopEntry(x.Key, x.Value), true)
            })
        });
    }

    /// <summary>
    ///     Gets the activity filter list and mode.
    /// </summary>
    [HttpGet("activity-filters")]
    public IActionResult GetActivityFilters(ulong guildId)
    {
        return Ok(new
        {
            Mode = (ActivityFilterMode)settings.GetCachedSettings(guildId).ActivityFilterMode,
            Names = settings.GetActivityFilters(guildId)
        });
    }

    /// <summary>
    ///     Toggles a name on the activity filter list.
    /// </summary>
    [HttpPost("activity-filters")]
    public async Task<IActionResult> ToggleActivityFilter(ulong guildId, [FromBody] string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 128)
            return BadRequest("Name is required and must be at most 128 characters");
        return Ok(await settings.ToggleActivityFilterAsync(guildId, name));
    }

    /// <summary>
    ///     Gets a time series for charts.
    /// </summary>
    [HttpGet("series/{kind}")]
    public async Task<IActionResult> Series(ulong guildId, StatChartKind kind, [FromQuery] int? days = null,
        [FromQuery] ulong? userId = null, [FromQuery] ulong? channelId = null)
    {
        var lookback = Math.Max(1, stats.ResolveLookback(guildId, days));
        switch (kind)
        {
            case StatChartKind.Messages:
                return Ok(await stats.GetMessageSeriesAsync(guildId, lookback, userId, channelId));
            case StatChartKind.Voice:
                return Ok(await stats.GetVoiceSeriesAsync(guildId, lookback, userId, channelId));
            case StatChartKind.Members:
            case StatChartKind.Status:
            case StatChartKind.InVoice:
                return Ok(await stats.GetSnapshotSeriesAsync(guildId, lookback));
            default:
            {
                var (joins, leaves) = await stats.GetJoinLeaveSeriesAsync(guildId, lookback);
                return Ok(new
                {
                    Joins = joins, Leaves = leaves
                });
            }
        }
    }

    /// <summary>
    ///     Gets the tracking settings.
    /// </summary>
    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings(ulong guildId)
    {
        return Ok(await settings.GetSettingsAsync(guildId));
    }

    /// <summary>
    ///     Updates the tracking settings.
    /// </summary>
    [HttpPut("settings")]
    public async Task<IActionResult> UpdateSettings(ulong guildId, [FromBody] ServerStatsSettingsRequest? request)
    {
        if (request == null)
            return BadRequest("The request body is missing or is not valid JSON for this endpoint");

        auditContext.RecordBefore(await settings.GetSettingsAsync(guildId));
        var updated = await settings.UpdateSettingsAsync(guildId, s =>
        {
            if (request.TrackVoice.HasValue) s.TrackVoice = request.TrackVoice.Value;
            if (request.TrackSnapshots.HasValue) s.TrackSnapshots = request.TrackSnapshots.Value;
            if (request.MessageCooldownSeconds.HasValue)
                s.MessageCooldownSeconds = request.MessageCooldownSeconds.Value;
            if (request.DefaultLookbackDays.HasValue) s.DefaultLookbackDays = request.DefaultLookbackDays.Value;
            if (request.CountBots.HasValue) s.CountBots = request.CountBots.Value;
            if (request.VoiceStates.HasValue) s.VoiceStates = request.VoiceStates.Value;
            if (request.TrackActivities.HasValue) s.TrackActivities = request.TrackActivities.Value;
            if (request.VerifyActivities.HasValue) s.VerifyActivities = request.VerifyActivities.Value;
            if (request.ActivityFilterMode.HasValue) s.ActivityFilterMode = (int)request.ActivityFilterMode.Value;
        });
        auditContext.RecordAfter(updated);
        return Ok(updated);
    }

    /// <summary>
    ///     Gets the exclusions of every kind.
    /// </summary>
    [HttpGet("exclusions")]
    public IActionResult GetExclusions(ulong guildId)
    {
        return Ok(new
        {
            Channels = settings.GetExclusions(guildId, StatsExclusionKind.Channel),
            Roles = settings.GetExclusions(guildId, StatsExclusionKind.Role),
            Users = settings.GetExclusions(guildId, StatsExclusionKind.User)
        });
    }

    /// <summary>
    ///     Adds an exclusion.
    /// </summary>
    [HttpPost("exclusions/{kind}/{targetId}")]
    public async Task<IActionResult> AddExclusion(ulong guildId, StatsExclusionKind kind, ulong targetId)
    {
        return Ok(await settings.AddExclusionAsync(guildId, targetId, kind));
    }

    /// <summary>
    ///     Removes an exclusion.
    /// </summary>
    [HttpDelete("exclusions/{kind}/{targetId}")]
    public async Task<IActionResult> RemoveExclusion(ulong guildId, StatsExclusionKind kind, ulong targetId)
    {
        return Ok(await settings.RemoveExclusionAsync(guildId, targetId, kind));
    }

    /// <summary>
    ///     Exports a member ranking as CSV.
    /// </summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export(ulong guildId, [FromQuery] StatKind kind = StatKind.Messages,
        [FromQuery] int? days = null)
    {
        var guild = Guild(guildId);
        if (guild == null)
            return NotFound("Guild not found");

        var lookback = stats.ResolveLookback(guildId, days);
        var csv = await stats.ExportCsvAsync(guild, kind, lookback);
        return File(Encoding.UTF8.GetBytes(csv), "text/csv", $"{kind}-{lookback}d.csv".ToLowerInvariant());
    }

    /// <summary>
    ///     A partial update to the tracking settings.
    /// </summary>
    public class ServerStatsSettingsRequest
    {
        /// <summary>Whether voice time is tracked.</summary>
        public bool? TrackVoice { get; set; }

        /// <summary>Whether hourly snapshots are recorded.</summary>
        public bool? TrackSnapshots { get; set; }

        /// <summary>Seconds between counted messages from one member.</summary>
        public int? MessageCooldownSeconds { get; set; }

        /// <summary>The default window in days.</summary>
        public int? DefaultLookbackDays { get; set; }

        /// <summary>Whether bots are counted.</summary>
        public bool? CountBots { get; set; }

        /// <summary>A mask of ignored voice states.</summary>
        public int? VoiceStates { get; set; }

        /// <summary>Whether games and apps are tracked.</summary>
        public bool? TrackActivities { get; set; }

        /// <summary>Whether only Discord attested activities count.</summary>
        public bool? VerifyActivities { get; set; }

        /// <summary>Whether the activity filter is a whitelist or blacklist.</summary>
        public ActivityFilterMode? ActivityFilterMode { get; set; }
    }
}