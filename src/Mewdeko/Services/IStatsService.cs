namespace Mewdeko.Services;

/// <summary>
///     Interface for a service responsible for providing various statistics.
/// </summary>
public interface IStatsService : INService
{
    /// <summary>
    ///     Gets a string representing the process working set, in megabytes.
    /// </summary>
    public string Heap { get; }

    /// <summary>
    ///     Gets a string representing the size of the live managed heap, in megabytes.
    /// </summary>
    public string ManagedHeap { get; }

    /// <summary>
    ///     Gets a string representing the memory committed by the garbage collector, in megabytes.
    /// </summary>
    public string CommittedHeap { get; }

    /// <summary>
    ///     Gets a description of the garbage collector the runtime is using.
    /// </summary>
    public string GcMode { get; }

    /// <summary>
    ///     Gets a string representing the library information.
    /// </summary>
    public string Library { get; }

    /// <summary>
    ///     Gets a formatted uptime string.
    /// </summary>
    /// <param name="separator">Optional separator to be used between different components of the uptime string.</param>
    /// <returns>A string representing the uptime.</returns>
    public string GetUptimeString(string separator = ", ");
}