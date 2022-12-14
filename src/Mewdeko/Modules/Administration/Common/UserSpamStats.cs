using System.Collections.Concurrent;
using System.Threading;

namespace Mewdeko.Modules.Administration.Common;

public sealed class UserSpamStats : IDisposable
{
    private readonly object applyLock = new();

    public UserSpamStats(IUserMessage msg)
    {
        LastMessage = msg.Content.ToUpperInvariant();
        Timers = new ConcurrentQueue<Timer>();

        ApplyNextMessage(msg);
    }

    public int Count => Timers.Count;
    public string LastMessage { get; set; }

    private ConcurrentQueue<Timer> Timers { get; }

    public void Dispose()
    {
        while (Timers.TryDequeue(out var old))
            old.Change(Timeout.Infinite, Timeout.Infinite);
    }

    public void ApplyNextMessage(IUserMessage message)
    {
        lock (applyLock)
        {
            var upperMsg = message.Content.ToUpperInvariant();
            if (upperMsg != LastMessage || (string.IsNullOrWhiteSpace(upperMsg) && message.Attachments.Count > 0))
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