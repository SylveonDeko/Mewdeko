#nullable enable
using System;
using System.Collections.Generic;

namespace Mewdeko.Core.Modules.Music
{
    public interface IMusicQueue
    {
        IQueuedTrackInfo Enqueue(ITrackInfo trackInfo, string queuer, out int index);
        IQueuedTrackInfo EnqueueNext(ITrackInfo song, string queuer, out int index);
        
        void EnqueueMany(IEnumerable<ITrackInfo> tracks, string queuer);
        
        public IReadOnlyCollection<IQueuedTrackInfo> List();
        IQueuedTrackInfo? GetCurrent(out int index);
        void Advance();
        void Clear();
        bool IsLast();
        bool SetIndex(int index);
        bool TryRemoveAt(int index, out IQueuedTrackInfo? trackInfo, out bool isCurrent);
        int Index { get; }
        int Count { get; }
        void RemoveCurrent();
        IQueuedTrackInfo? MoveTrack(int from, int to);
        void Shuffle(Random rng);
    }
}