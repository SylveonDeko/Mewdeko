using System;
using System.Collections.Generic;
using System.Threading.Tasks;

#nullable enable
namespace Mewdeko.Core.Modules.Music
{
    public interface IMusicPlayer : IDisposable
    {
        float Volume { get; }
        bool IsPaused { get; }
        bool IsStopped { get; }
        bool IsKilled { get; }
        bool IsRepeatingCurrentSong { get; }
        bool IsRepeatingQueue { get; }
        int CurrentIndex { get; }
        bool IsAutoDelete { get; set; }

        void Stop();
        void Clear();
        IReadOnlyCollection<IQueuedTrackInfo> GetQueuedTracks();
        IQueuedTrackInfo? GetCurrentTrack(out int index);
        void Next();
        bool MoveTo(int index);
        void SetVolume(int newVolume);

        void Kill();
        bool TryRemoveTrackAt(int index, out IQueuedTrackInfo? trackInfo);
        
        
        Task<(IQueuedTrackInfo? QueuedTrack, int Index)> TryEnqueueTrackAsync(string query, string queuer, bool asNext, MusicPlatform? forcePlatform = null);
        Task EnqueueManyAsync(IEnumerable<(string Query, MusicPlatform Platform)> queries, string queuer);
        bool ToggleRcs();
        bool ToggleRpl();
        bool ToggleAd();
        bool TogglePause();
        IQueuedTrackInfo? MoveTrack(int from, int to);
        void EnqueueTrack(ITrackInfo track, string queuer);
        void EnqueueTracks(IEnumerable<ITrackInfo> tracks, string queuer);
        void ShuffleQueue();
    }
}