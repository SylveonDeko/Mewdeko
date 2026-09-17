namespace Mewdeko.Controllers.Common.Analytics;

/// <summary>
///     One metric from the registry.
/// </summary>
/// <param name="Metric">The metric name.</param>
/// <param name="Kind">counter, gauge or histogram.</param>
/// <param name="Labels">Label keys with up to 50 sample values each.</param>
/// <param name="LastSeen">When the metric was last written.</param>
public sealed record MetricInfo(string Metric, string Kind, Dictionary<string, string[]> Labels, DateTime LastSeen);

/// <summary>
///     One point of a series.
/// </summary>
/// <param name="BucketUnix">Start of the bucket as unix seconds.</param>
/// <param name="Value">The aggregated value, or null when the bucket has no data.</param>
public sealed record SeriesPoint(long BucketUnix, double? Value);

/// <summary>
///     One series of a series response.
/// </summary>
/// <param name="Name">The group value, or the metric name when not grouped.</param>
/// <param name="Labels">The labels identifying the series; for Other, count and names.</param>
/// <param name="Points">One point per bucket in the range.</param>
public sealed record SeriesItem(string Name, Dictionary<string, string> Labels, List<SeriesPoint> Points);

/// <summary>
///     Result of a series query.
/// </summary>
/// <param name="Resolution">Bucket width in minutes.</param>
/// <param name="Series">The series, largest total first.</param>
/// <param name="Truncated">Whether series beyond the maximum were folded into Other.</param>
public sealed record SeriesResponse(int Resolution, List<SeriesItem> Series, bool Truncated);

/// <summary>
///     A single aggregated value.
/// </summary>
/// <param name="Value">The value, or null when there is no data.</param>
public sealed record AggregateResponse(double? Value);

/// <summary>
///     One name and value pair.
/// </summary>
/// <param name="Name">The label value.</param>
/// <param name="Value">The aggregated value.</param>
public sealed record BreakdownItem(string Name, double Value);

/// <summary>
///     One command in the top commands table.
/// </summary>
/// <param name="Command">The command name.</param>
/// <param name="Module">The module.</param>
/// <param name="Count">Invocations in range.</param>
/// <param name="Failures">Failed invocations.</param>
/// <param name="FailureRate">Failures divided by count.</param>
/// <param name="AvgMs">Mean duration.</param>
/// <param name="P95Ms">95th percentile duration from raw rows.</param>
/// <param name="Guilds">Distinct guilds that used it.</param>
public sealed record TopCommand(
    string Command,
    string? Module,
    long Count,
    long Failures,
    double FailureRate,
    double? AvgMs,
    double? P95Ms,
    long Guilds);

/// <summary>
///     One command and error class pair in the failing commands table.
/// </summary>
/// <param name="Command">The command name.</param>
/// <param name="Module">The module.</param>
/// <param name="ErrorClass">The error class.</param>
/// <param name="Count">Failures in range.</param>
/// <param name="LastSeen">Latest failure.</param>
/// <param name="LastMessage">Message of the latest failure.</param>
public sealed record FailingCommand(
    string Command,
    string? Module,
    string ErrorClass,
    long Count,
    DateTime LastSeen,
    string? LastMessage);

/// <summary>
///     One raw command invocation.
/// </summary>
/// <param name="Id">Row id.</param>
/// <param name="At">When it finished.</param>
/// <param name="Bot">The instance.</param>
/// <param name="Shard">The shard.</param>
/// <param name="GuildId">The guild, or null.</param>
/// <param name="GuildSize">Member count at the time.</param>
/// <param name="Kind">How it was invoked.</param>
/// <param name="Module">The module.</param>
/// <param name="Command">The command name.</param>
/// <param name="Ok">Whether it succeeded.</param>
/// <param name="ErrorClass">The error class when it failed.</param>
/// <param name="ErrorMessage">The error message when it failed.</param>
/// <param name="DurationMs">Execution time.</param>
/// <param name="AckMs">Time to first acknowledgement.</param>
/// <param name="Language">The locale.</param>
public sealed record CommandInvocationItem(
    long Id,
    DateTime At,
    string Bot,
    int? Shard,
    string? GuildId,
    int? GuildSize,
    string Kind,
    string? Module,
    string Command,
    bool Ok,
    string? ErrorClass,
    string? ErrorMessage,
    int DurationMs,
    int? AckMs,
    string? Language);

/// <summary>
///     One page of a list.
/// </summary>
/// <typeparam name="T">The item type.</typeparam>
/// <param name="Items">The page.</param>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Items per page.</param>
/// <param name="Total">Total matching items.</param>
public sealed record PagedResponse<T>(List<T> Items, int Page, int PageSize, long Total);

/// <summary>
///     One failed invocation of a command with a given error class.
/// </summary>
/// <param name="At">When it failed.</param>
/// <param name="GuildId">The guild, or null.</param>
/// <param name="Kind">How it was invoked.</param>
/// <param name="Module">The module.</param>
/// <param name="Message">The error message.</param>
/// <param name="DurationMs">Execution time.</param>
public sealed record CommandErrorSample(
    DateTime At,
    string? GuildId,
    string Kind,
    string? Module,
    string? Message,
    int DurationMs);

/// <summary>
///     One cell of the usage heatmap.
/// </summary>
/// <param name="Hour">Hour of day, UTC.</param>
/// <param name="Size">Guild size bucket.</param>
/// <param name="Count">Invocations.</param>
public sealed record HeatmapCell(int Hour, string Size, long Count);

/// <summary>
///     Command usage by hour of day and guild size.
/// </summary>
/// <param name="Sizes">The size buckets in order.</param>
/// <param name="Cells">Non-empty cells.</param>
public sealed record HeatmapResponse(List<string> Sizes, List<HeatmapCell> Cells);

/// <summary>
///     Count of one event type.
/// </summary>
/// <param name="Type">The event type.</param>
/// <param name="Count">Occurrences.</param>
public sealed record EventCount(string Type, double Count);

/// <summary>
///     One guild in the top guilds table.
/// </summary>
/// <param name="GuildId">The guild.</param>
/// <param name="Name">Guild name when the bot is in it.</param>
/// <param name="MemberCount">Member count when known.</param>
/// <param name="Events">Gateway events in range.</param>
/// <param name="TopTypes">The busiest event types.</param>
public sealed record TopGuild(string GuildId, string? Name, int? MemberCount, long Events, List<EventCount> TopTypes);

/// <summary>
///     One hour where a guild's activity was far above its own baseline.
/// </summary>
/// <param name="GuildId">The guild.</param>
/// <param name="Name">Guild name when known.</param>
/// <param name="EventType">The event type.</param>
/// <param name="Hour">The hour.</param>
/// <param name="Count">Events in that hour.</param>
/// <param name="Mean">Mean for that hour of day over the previous 14 days.</param>
/// <param name="StdDev">Standard deviation over the same window.</param>
/// <param name="Z">How many deviations above the mean.</param>
public sealed record GuildAnomaly(
    string GuildId,
    string? Name,
    string EventType,
    DateTime Hour,
    long Count,
    double Mean,
    double StdDev,
    double Z);

/// <summary>
///     One hour and type cell of a guild timeline.
/// </summary>
/// <param name="Hour">The hour.</param>
/// <param name="Type">The event type.</param>
/// <param name="Count">Events.</param>
public sealed record GuildTimelineCell(DateTime Hour, string Type, long Count);

/// <summary>
///     Hourly activity of one guild by event type.
/// </summary>
/// <param name="GuildId">The guild.</param>
/// <param name="Types">Event types present, busiest first.</param>
/// <param name="Cells">Non-empty cells.</param>
public sealed record GuildTimelineResponse(string GuildId, List<string> Types, List<GuildTimelineCell> Cells);

/// <summary>
///     One security relevant event of a guild.
/// </summary>
/// <param name="Id">Row id.</param>
/// <param name="At">When it happened.</param>
/// <param name="EventType">The event type.</param>
/// <param name="Bot">The instance.</param>
/// <param name="Shard">The shard.</param>
public sealed record GuildEventItem(long Id, DateTime At, string EventType, string Bot, int? Shard);

/// <summary>
///     Use of one feature.
/// </summary>
/// <param name="Feature">The feature key.</param>
/// <param name="Count">Successful uses.</param>
/// <param name="Errors">Failed uses.</param>
public sealed record FeatureUse(string Feature, long Count, long Errors);

/// <summary>
///     Member makeup and shape of a guild, read from the gateway cache.
/// </summary>
/// <param name="MemberCount">Total members as reported by Discord.</param>
/// <param name="Humans">Cached members that are not bots.</param>
/// <param name="Bots">Cached members that are bots.</param>
/// <param name="Online">Cached members that are online, idle or do not disturb.</param>
/// <param name="Boosts">Active nitro boosts.</param>
/// <param name="BoostTier">Boost tier, 0 to 3.</param>
/// <param name="Channels">Channel count including categories and threads in cache.</param>
/// <param name="Roles">Role count.</param>
/// <param name="OwnerId">The owner.</param>
/// <param name="CreatedAt">When the guild was created.</param>
public sealed record GuildShape(
    int MemberCount,
    int Humans,
    int Bots,
    int Online,
    int Boosts,
    int BoostTier,
    int Channels,
    int Roles,
    string OwnerId,
    DateTime CreatedAt);

/// <summary>
///     Use of one command by a guild.
/// </summary>
/// <param name="Command">The command name.</param>
/// <param name="Count">Invocations in range.</param>
/// <param name="Failures">Invocations that failed.</param>
public sealed record CommandUse(string Command, long Count, long Failures);

/// <summary>
///     Summary card for one guild.
/// </summary>
/// <param name="GuildId">The guild.</param>
/// <param name="Name">Guild name when the bot is in it.</param>
/// <param name="MemberCount">Member count when known.</param>
/// <param name="Shard">The shard when the bot is in it.</param>
/// <param name="JoinedAt">When the bot joined, when known.</param>
/// <param name="Present">Whether the bot is currently in the guild.</param>
/// <param name="Commands">Commands run in range.</param>
/// <param name="Events">Gateway events in range.</param>
/// <param name="Features">Features used in range.</param>
/// <param name="Shape">Member makeup when the bot is in the guild.</param>
/// <param name="ConfiguredFeatures">Feature keys the guild has set up, per the live feature definitions.</param>
/// <param name="EnabledFeatures">Feature keys the guild has set up and switched on.</param>
/// <param name="TopCommands">Most used commands in range.</param>
public sealed record GuildCard(
    string GuildId,
    string? Name,
    int? MemberCount,
    int? Shard,
    DateTime? JoinedAt,
    bool Present,
    long Commands,
    long Events,
    List<FeatureUse> Features,
    GuildShape? Shape,
    List<string> ConfiguredFeatures,
    List<string> EnabledFeatures,
    List<CommandUse> TopCommands);

/// <summary>
///     One row of the per server overview table.
/// </summary>
/// <param name="GuildId">The guild.</param>
/// <param name="Name">Guild name.</param>
/// <param name="Shard">The shard the guild lives on.</param>
/// <param name="Shape">Member makeup and size.</param>
/// <param name="JoinedAt">When the bot joined, when known.</param>
/// <param name="Commands">Commands run in range.</param>
/// <param name="Events">Gateway events in range.</param>
/// <param name="FeaturesUsed">Distinct features used in range.</param>
/// <param name="FeaturesConfigured">Distinct features the guild has set up.</param>
/// <param name="FeaturesEnabled">Distinct features the guild has set up and switched on.</param>
/// <param name="Features">Feature keys used in range, most used first.</param>
public sealed record GuildOverviewRow(
    string GuildId,
    string Name,
    int Shard,
    GuildShape Shape,
    DateTime? JoinedAt,
    long Commands,
    long Events,
    int FeaturesUsed,
    int FeaturesConfigured,
    int FeaturesEnabled,
    List<string> Features);

/// <summary>
///     One feature in the adoption table.
/// </summary>
/// <param name="Feature">The feature key.</param>
/// <param name="ActiveGuilds">Guilds that used it in range.</param>
/// <param name="Activity">Uses in range.</param>
/// <param name="Errors">Failed uses in range.</param>
/// <param name="Configured">Guilds with it configured per the latest census.</param>
/// <param name="Enabled">Guilds with it enabled per the latest census.</param>
public sealed record FeatureAdoption(
    string Feature,
    long ActiveGuilds,
    long Activity,
    long Errors,
    double? Configured,
    double? Enabled);

/// <summary>
///     How many guilds used a given number of features.
/// </summary>
/// <param name="Features">Number of distinct features.</param>
/// <param name="Guilds">Guilds with that many.</param>
public sealed record FeatureDepthBucket(int Features, long Guilds);

/// <summary>
///     One guild's size and feature count.
/// </summary>
/// <param name="GuildId">The guild.</param>
/// <param name="MemberCount">Member count when known.</param>
/// <param name="Features">Distinct features used in range.</param>
public sealed record FeatureDepthPoint(string GuildId, int? MemberCount, int Features);

/// <summary>
///     Feature depth: histogram plus size versus features points.
/// </summary>
/// <param name="Histogram">Guilds per feature count.</param>
/// <param name="Points">Per guild points, largest guilds first.</param>
public sealed record FeatureDepthResponse(List<FeatureDepthBucket> Histogram, List<FeatureDepthPoint> Points);

/// <summary>
///     How many guilds use one setting value, from the latest census.
/// </summary>
/// <param name="Metric">The census metric name.</param>
/// <param name="Table">The settings table.</param>
/// <param name="Column">The column.</param>
/// <param name="Value">The value.</param>
/// <param name="Count">Guilds with that value.</param>
public sealed record SettingUsage(string Metric, string Table, string Column, string Value, double Count);

/// <summary>
///     A value for one day.
/// </summary>
/// <param name="Day">The day.</param>
/// <param name="Value">The value.</param>
public sealed record DayValue(DateTime Day, double Value);

/// <summary>
///     One nightly snapshot.
/// </summary>
/// <param name="Day">The day.</param>
/// <param name="Bot">The instance.</param>
/// <param name="Guilds">Guild count.</param>
/// <param name="Users">Summed member count.</param>
/// <param name="Features">Feature key to number of guilds with it configured.</param>
public sealed record SnapshotItem(
    DateTime Day,
    string Bot,
    int Guilds,
    long Users,
    Dictionary<string, double>? Features);

/// <summary>
///     Joins and leaves on one day.
/// </summary>
/// <param name="Day">The day.</param>
/// <param name="Joins">Guilds joined.</param>
/// <param name="Leaves">Guilds left.</param>
public sealed record ChurnDay(DateTime Day, double Joins, double Leaves);

/// <summary>
///     Guild churn over the range.
/// </summary>
/// <param name="Joins">Guilds joined.</param>
/// <param name="Leaves">Guilds left.</param>
/// <param name="Net">Joins minus leaves.</param>
/// <param name="Bounced">Guilds that were only active for a day and are gone.</param>
/// <param name="BounceRate">Bounced divided by joins.</param>
/// <param name="JoinsBySize">Joins per guild size bucket.</param>
/// <param name="LeavesBySize">Leaves per guild size bucket.</param>
/// <param name="Days">Daily joins and leaves.</param>
public sealed record ChurnResponse(
    double Joins,
    double Leaves,
    double Net,
    long Bounced,
    double? BounceRate,
    List<BreakdownItem> JoinsBySize,
    List<BreakdownItem> LeavesBySize,
    List<ChurnDay> Days);

/// <summary>
///     Retention of the guilds joined on one day.
/// </summary>
/// <param name="Day">The join day.</param>
/// <param name="Joined">Guilds joined that day.</param>
/// <param name="Retained">Of those, guilds the bot is still in.</param>
/// <param name="Rate">Retained divided by joined.</param>
public sealed record RetentionPoint(DateTime Day, double Joined, long Retained, double? Rate);

/// <summary>
///     A guild that was active for at most a day and is gone.
/// </summary>
/// <param name="GuildId">The guild.</param>
/// <param name="FirstSeen">First active hour.</param>
/// <param name="LastSeen">Last active hour.</param>
/// <param name="Events">Events while active.</param>
public sealed record BouncedGuild(string GuildId, DateTime FirstSeen, DateTime LastSeen, long Events);

/// <summary>
///     A guild that went quiet in the last week.
/// </summary>
/// <param name="GuildId">The guild.</param>
/// <param name="Name">Guild name when known.</param>
/// <param name="MemberCount">Member count when known.</param>
/// <param name="LastActive">Last active hour.</param>
/// <param name="Events">Events in the week before it went quiet.</param>
public sealed record SilentGuild(string GuildId, string? Name, int? MemberCount, DateTime LastActive, long Events);

/// <summary>
///     One distinct error across the range.
/// </summary>
/// <param name="Type">Exception type.</param>
/// <param name="Module">The module.</param>
/// <param name="Location">Where it was thrown.</param>
/// <param name="Hash">Message hash for the samples lookup.</param>
/// <param name="Count">Occurrences.</param>
/// <param name="FirstSeen">First occurrence.</param>
/// <param name="LastSeen">Latest occurrence.</param>
/// <param name="LastMessage">Message of the latest occurrence.</param>
public sealed record ErrorGroup(
    string Type,
    string? Module,
    string? Location,
    string Hash,
    long Count,
    DateTime FirstSeen,
    DateTime LastSeen,
    string? LastMessage);

/// <summary>
///     One hourly error sample.
/// </summary>
/// <param name="Hour">The hour.</param>
/// <param name="Bot">The instance.</param>
/// <param name="Shard">The shard.</param>
/// <param name="Location">Where it was thrown.</param>
/// <param name="Message">The message.</param>
/// <param name="Count">Occurrences in the hour.</param>
/// <param name="FirstSeen">First occurrence in the hour.</param>
/// <param name="LastSeen">Latest occurrence in the hour.</param>
/// <param name="LastGuildId">Guild of the latest occurrence.</param>
public sealed record ErrorSampleItem(
    DateTime Hour,
    string Bot,
    int? Shard,
    string? Location,
    string? Message,
    int Count,
    DateTime FirstSeen,
    DateTime LastSeen,
    string? LastGuildId);

/// <summary>
///     AI usage of one model.
/// </summary>
/// <param name="Model">The model name.</param>
/// <param name="Provider">The provider.</param>
/// <param name="Requests">Requests in range.</param>
/// <param name="Failures">Failed requests.</param>
/// <param name="TokensIn">Prompt tokens.</param>
/// <param name="TokensOut">Completion tokens.</param>
/// <param name="P95Ms">95th percentile request duration.</param>
/// <param name="CostUsd">Estimated cost, or null for unknown models.</param>
public sealed record AiModelSummary(
    string Model,
    string? Provider,
    double Requests,
    double Failures,
    double TokensIn,
    double TokensOut,
    double? P95Ms,
    double? CostUsd);

/// <summary>
///     AI usage over the range.
/// </summary>
/// <param name="Models">Per model rows, most requests first.</param>
/// <param name="Requests">Total requests.</param>
/// <param name="TokensIn">Total prompt tokens.</param>
/// <param name="TokensOut">Total completion tokens.</param>
/// <param name="CostUsd">Estimated cost of the priced models.</param>
public sealed record AiSummaryResponse(
    List<AiModelSummary> Models,
    double Requests,
    double TokensIn,
    double TokensOut,
    double? CostUsd);

/// <summary>
///     One guild's AI chat use.
/// </summary>
/// <param name="GuildId">The guild.</param>
/// <param name="Name">Guild name when known.</param>
/// <param name="Count">Successful uses.</param>
/// <param name="Errors">Failed uses.</param>
public sealed record AiGuild(string GuildId, string? Name, long Count, long Errors);

/// <summary>
///     One dashboard route.
/// </summary>
/// <param name="Route">The route template.</param>
/// <param name="Views">Requests in range.</param>
/// <param name="Visitors">Distinct visitor hashes.</param>
/// <param name="P95Ms">95th percentile response time.</param>
/// <param name="Errors">5xx responses.</param>
public sealed record RouteStats(string Route, long Views, long Visitors, double? P95Ms, long Errors);

/// <summary>
///     One route and status pair that failed.
/// </summary>
/// <param name="Route">The route template.</param>
/// <param name="Status">The status code.</param>
/// <param name="Count">Occurrences.</param>
/// <param name="LastSeen">Latest occurrence.</param>
public sealed record ErrorRoute(string Route, int Status, long Count, DateTime LastSeen);

/// <summary>
///     The login funnel.
/// </summary>
/// <param name="LoginViews">Views of the login route.</param>
/// <param name="LoginVisitors">Distinct visitors of the login route.</param>
/// <param name="CallbackViews">Views of the OAuth callback route.</param>
/// <param name="CallbackVisitors">Distinct visitors of the callback route.</param>
/// <param name="DashboardViews">Views of dashboard routes.</param>
/// <param name="DashboardVisitors">Distinct visitors of dashboard routes.</param>
public sealed record FunnelResponse(
    long LoginViews,
    long LoginVisitors,
    long CallbackViews,
    long CallbackVisitors,
    long DashboardViews,
    long DashboardVisitors);

/// <summary>
///     Size of one analytics table.
/// </summary>
/// <param name="Table">The table name.</param>
/// <param name="Rows">Row count.</param>
/// <param name="Newest">Newest timestamp in it.</param>
public sealed record TableHealth(string Table, long Rows, DateTime? Newest);

/// <summary>
///     One registered instance.
/// </summary>
/// <param name="BotId">The instance's bot user id.</param>
/// <param name="BotName">The bot's name.</param>
/// <param name="Host">The host.</param>
/// <param name="Port">The API port.</param>
/// <param name="IsActive">Whether it is marked active.</param>
/// <param name="LastStatusUpdate">When the instance registry last heard from it.</param>
/// <param name="LastGuildCountAt">Its newest guild.count bucket.</param>
public sealed record InstanceHealth(
    string BotId,
    string BotName,
    string Host,
    int Port,
    bool IsActive,
    DateTime LastStatusUpdate,
    DateTime? LastGuildCountAt);

/// <summary>
///     Pipeline health.
/// </summary>
/// <param name="Enabled">Whether analytics is enabled on this instance.</param>
/// <param name="LastFlushAt">Last successful flush.</param>
/// <param name="LastRollupAt">Last successful rollup.</param>
/// <param name="LastMaintenanceAt">Last successful retention pass.</param>
/// <param name="LastError">Last flush failure, if the latest flush failed.</param>
/// <param name="PendingSeries">Series waiting in memory.</param>
/// <param name="PendingRows">Raw rows waiting in memory.</param>
/// <param name="Tables">Per table sizes.</param>
/// <param name="Instances">Registered instances.</param>
public sealed record HealthResponse(
    bool Enabled,
    DateTime? LastFlushAt,
    DateTime? LastRollupAt,
    DateTime? LastMaintenanceAt,
    string? LastError,
    int PendingSeries,
    int PendingRows,
    List<TableHealth> Tables,
    List<InstanceHealth> Instances);