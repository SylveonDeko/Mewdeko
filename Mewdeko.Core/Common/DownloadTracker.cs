using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Mewdeko.Core.Services;

namespace Mewdeko.Core.Common
{
    public class DownloadTracker : INService
    {
        private readonly SemaphoreSlim downloadUsersSemaphore = new(1, 1);
        private ConcurrentDictionary<ulong, DateTime> LastDownloads { get; } = new();

        /// <summary>
        ///     Ensures all users on the specified guild were downloaded within the last hour.
        /// </summary>
        /// <param name="guild">Guild to check and potentially download users from</param>
        /// <returns>Task representing download state</returns>
        public async Task EnsureUsersDownloadedAsync(IGuild guild)
        {
            await downloadUsersSemaphore.WaitAsync();
            try
            {
                var now = DateTime.UtcNow;

                // download once per hour at most
                var added = LastDownloads.AddOrUpdate(
                    guild.Id,
                    now,
                    (key, old) => now - old > TimeSpan.FromHours(1) ? now : old);

                // means that this entry was just added - download the users
                if (added == now)
                    await guild.DownloadUsersAsync();
            }
            finally
            {
                downloadUsersSemaphore.Release();
            }
        }
    }
}