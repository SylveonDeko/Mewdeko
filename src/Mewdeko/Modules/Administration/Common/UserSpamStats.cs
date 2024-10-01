using System.Collections.Concurrent;
using System.Threading;

namespace Mewdeko.Modules.Administration.Common;

/// <summary>
///     Represents statistics for a user's spam behavior.
/// </summary>
public sealed class UserSpamStats : IDisposable
{
    private readonly object applyLock = new();

    /// <summary>
    ///     Initializes a new instance of the <see cref="UserSpamStats" /> class with the specified message.
    /// </summary>
    /// <param name="msg">The message to initialize the statistics.</param>
    public UserSpamStats(IUserMessage msg)
    {
        LastMessage = msg.Content.ToUpperInvariant();
        Timers = new ConcurrentQueue<Timer>();

        ApplyNextMessage(msg);
    }

    /// <summary>
    ///     Gets the number of active timers.
    /// </summary>
    public int Count
    {
        get
        {
            return Timers.Count;
        }
    }

    /// <summary>
    ///     Gets or sets the content of the last message.
    /// </summary>
    public string LastMessage { get; set; }

    private ConcurrentQueue<Timer> Timers { get; }

    /// <summary>
    ///     Disposes the instance, cancelling all active timers.
    /// </summary>
    public void Dispose()
    {
        while (Timers.TryDequeue(out var old))
            old.Change(Timeout.Infinite, Timeout.Infinite);
    }

    /// <summary>
    ///     Applies the next message to update the statistics.
    /// </summary>
    /// <param name="message">The message to apply.</param>
    public void ApplyNextMessage(IUserMessage message)
    {
        lock (applyLock)
        {
            var upperMsg = message.Content.ToUpperInvariant();
            if (upperMsg != LastMessage || string.IsNullOrWhiteSpace(upperMsg) && message.Attachments.Count > 0)
            {
                LastMessage = upperMsg;
                while (Timers.TryDequeue(out var old))
                    old.Change(Timeout.Infinite, Timeout.Infinite);
            }

            var t = new Timer(_ =>
            {
                if (Timers.TryDequeue(out var old))
                    old.Change(Timeout.Infinite, Timeout.Infinite);
            }, null, TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(30));
            Timers.Enqueue(t);
        }
    }
}