using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Mewdeko.Core.Services.Database.Models;

#nullable enable
namespace Mewdeko.Core.Modules.Music
{
    public interface IMusicPlayer : IDisposable
    {
        float Volume { get; }
        bool IsPaused { get; }
        bool IsStopped { get; }
        bool IsKilled { get; }
        int CurrentIndex { get; }
        public PlayerRepeatType Repeat { get; }

        void Stop();
        void Clear();
        IReadOnlyCollection<IQueuedTrackInfo> GetQueuedTracks();
        IQueuedTrackInfo? GetCurrentTrack(out int index);
        void Next();
        bool MoveTo(int index);
        void SetVolume(int newVolume);

        void Kill();
        bool TryRemoveTrackAt(int index, out IQueuedTrackInfo? trackInfo);


        Task<(IQueuedTrackInfo? QueuedTrack, int Index)> TryEnqueueTrackAsync(string query, string queuer, bool asNext,
            MusicPlatform? forcePlatform = null);

        Task EnqueueManyAsync(IEnumerable<(string Query, MusicPlatform Platform)> queries, string queuer);
        bool TogglePause();
        IQueuedTrackInfo? MoveTrack(int from, int to);
        void EnqueueTrack(ITrackInfo track, string queuer);
        void EnqueueTracks(IEnumerable<ITrackInfo> tracks, string queuer);
        void SetRepeat(PlayerRepeatType type);
        void ShuffleQueue();
    }
}