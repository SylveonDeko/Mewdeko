using System.Diagnostics;

namespace Mewdeko.Services.Analytics;

/// <summary>
///     Remembers when each interaction arrived so the command handler can report its execution time and the time
///     to its first acknowledgement, which the REST hook observes on the interaction callback route.
/// </summary>
public sealed class InteractionAckTracker : INService
{
    private const int SweepThreshold = 2000;
    private static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(15);

    private readonly ConcurrentDictionary<ulong, Entry> pending = new();

    /// <summary>
    ///     Records that an interaction has started executing.
    /// </summary>
    /// <param name="interactionId">The interaction id.</param>
    /// <param name="createdAt">When Discord created the interaction.</param>
    public void Begin(ulong interactionId, DateTimeOffset createdAt)
    {
        if (pending.Count >= SweepThreshold)
            Sweep();

        pending[interactionId] = new Entry(createdAt, Stopwatch.GetTimestamp());
    }

    /// <summary>
    ///     Records the first acknowledgement of an interaction, if it is still being tracked.
    /// </summary>
    /// <param name="interactionId">The interaction id.</param>
    public void Acknowledge(ulong interactionId)
    {
        if (!pending.TryGetValue(interactionId, out var entry) || entry.AckMs is not null)
            return;

        var ack = (long)(DateTimeOffset.UtcNow - entry.CreatedAt).TotalMilliseconds;
        pending.TryUpdate(interactionId, entry with
        {
            AckMs = Math.Max(0, ack)
        }, entry);
    }

    /// <summary>
    ///     Stops tracking an interaction and returns what was measured.
    /// </summary>
    /// <param name="interactionId">The interaction id.</param>
    /// <returns>The acknowledgement time when observed, and the execution time since <see cref="Begin" />.</returns>
    public (long? AckMs, long DurationMs) End(ulong interactionId)
    {
        if (!pending.TryRemove(interactionId, out var entry))
            return (null, 0);

        return (entry.AckMs, (long)Stopwatch.GetElapsedTime(entry.StartedTimestamp).TotalMilliseconds);
    }

    private void Sweep()
    {
        var cutoff = DateTimeOffset.UtcNow - MaxAge;
        foreach (var (id, entry) in pending)
        {
            if (entry.CreatedAt < cutoff)
                pending.TryRemove(id, out _);
        }
    }

    private sealed record Entry(DateTimeOffset CreatedAt, long StartedTimestamp, long? AckMs = null);
}