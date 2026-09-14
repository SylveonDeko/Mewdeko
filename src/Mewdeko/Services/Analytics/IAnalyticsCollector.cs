namespace Mewdeko.Services.Analytics;

/// <summary>
///     Records analytics observations. Every call is cheap and never throws; observations are aggregated in
///     memory and written by <see cref="AnalyticsWriter" />.
/// </summary>
public interface IAnalyticsCollector
{
    /// <summary>
    ///     Whether analytics are switched on in the bot config.
    /// </summary>
    public bool Enabled { get; }

    /// <summary>
    ///     The instance identifier stamped on every observation.
    /// </summary>
    public string Bot { get; }

    /// <summary>
    ///     Adds to a counter metric.
    /// </summary>
    /// <param name="metric">The metric name from the catalog.</param>
    /// <param name="value">The amount to add.</param>
    /// <param name="labels">Label key and value pairs.</param>
    public void Counter(string metric, double value = 1, params (string Key, string Value)[] labels);

    /// <summary>
    ///     Records the current value of a gauge metric.
    /// </summary>
    /// <param name="metric">The metric name from the catalog.</param>
    /// <param name="value">The value.</param>
    /// <param name="labels">Label key and value pairs.</param>
    public void Gauge(string metric, double value, params (string Key, string Value)[] labels);

    /// <summary>
    ///     Records one duration in a histogram metric.
    /// </summary>
    /// <param name="metric">The metric name from the catalog.</param>
    /// <param name="milliseconds">The duration in milliseconds.</param>
    /// <param name="labels">Label key and value pairs.</param>
    public void Duration(string metric, double milliseconds, params (string Key, string Value)[] labels);

    /// <summary>
    ///     Records a command or interaction execution: the raw row plus cmd.count, cmd.duration and cmd.errors.
    /// </summary>
    /// <param name="sample">The execution.</param>
    public void Command(CommandSample sample);

    /// <summary>
    ///     Counts a gateway event.
    /// </summary>
    /// <param name="eventType">The event name as used by the event handler.</param>
    /// <param name="shard">The shard, when known.</param>
    /// <param name="count">How many occurrences.</param>
    public void Event(string eventType, int? shard, int count = 1);

    /// <summary>
    ///     Counts a gateway event against a guild, and keeps the raw row when it is security relevant.
    /// </summary>
    /// <param name="guildId">The guild.</param>
    /// <param name="eventType">The event name.</param>
    /// <param name="shard">The shard, when known.</param>
    /// <param name="securityRelevant">Whether to keep a raw row for the guild event log.</param>
    public void GuildEvent(ulong guildId, string eventType, int? shard, bool securityRelevant);

    /// <summary>
    ///     Records a feature firing.
    /// </summary>
    /// <param name="feature">The feature key from the catalog.</param>
    /// <param name="guildId">The guild, or null.</param>
    /// <param name="ok">Whether it succeeded.</param>
    /// <param name="error">A short error class when it failed.</param>
    public void Feature(string feature, ulong? guildId, bool ok = true, string? error = null);

    /// <summary>
    ///     Records an exception: err.count plus an error sample.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <param name="location">Where it was caught, such as a service or handler name.</param>
    /// <param name="module">The module, when known.</param>
    /// <param name="guildId">The guild, when known.</param>
    /// <param name="shard">The shard, when known.</param>
    public void Error(Exception exception, string location, string? module = null, ulong? guildId = null,
        int? shard = null);

    /// <summary>
    ///     Records a dashboard page view.
    /// </summary>
    /// <param name="sample">The request.</param>
    public void PageView(PageViewSample sample);
}