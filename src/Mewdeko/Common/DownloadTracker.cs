using System.Threading;

namespace Mewdeko.Common;

/// <summary>
///     Tracks the downloading of users from guilds to ensure they are downloaded within specified intervals.
/// </summary>
public class DownloadTracker : INService
{
    private readonly SemaphoreSlim downloadUsersSemaphore = new(1, 1);

    /// <summary>
    ///     Gets the collection of timestamps for the last user downloads per guild.
    /// </summary>
    private ConcurrentDictionary<ulong, DateTime> LastDownloads { get; } = new();

    /// <summary>
    ///     Ensures that all users on the specified guild were downloaded within the last hour.
    /// </summary>
    /// <param name="guild">The guild to check and potentially download users from.</param>
    /// <returns>A task representing the download state.</returns>
    public async Task EnsureUsersDownloadedAsync(IGuild guild)
    {
        await downloadUsersSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            var now = DateTime.UtcNow;

            // Download once per hour at most
            var added = LastDownloads.AddOrUpdate(
                guild.Id,
                now,
                (_, old) => now - old > TimeSpan.FromHours(1) ? now : old);

            // Means that this entry was just added - download the users
            if (added == now)
                await guild.DownloadUsersAsync().ConfigureAwait(false);
        }
        finally
        {
            downloadUsersSemaphore.Release();
        }
    }
}