namespace Mewdeko.Controllers.Common.Logging;

/// <summary>
///     Response model for logging configuration
/// </summary>
public class LoggingConfigurationResponse
{
    /// <summary>
    ///     Whether logging is enabled for the guild
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    ///     Dictionary of log types to their assigned channel IDs
    /// </summary>
    public Dictionary<string, ulong?> LogTypes { get; set; } = new();

    /// <summary>
    ///     List of channels ignored from logging
    /// </summary>
    public List<ulong> IgnoredChannels { get; set; } = new();

    /// <summary>
    ///     Prefixes whose messages are skipped by message edit and delete logs
    /// </summary>
    public List<string> IgnorePrefixes { get; set; } = new();

    /// <summary>
    ///     Whether logs skip actions by bots
    /// </summary>
    public bool IgnoreBots { get; set; } = true;
}