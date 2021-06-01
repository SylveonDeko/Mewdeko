#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Mewdeko.Core.Modules.Music
{
    public sealed partial class MusicQueue
    {
        private sealed class QueuedTrackInfo : IQueuedTrackInfo
        {
            public ITrackInfo TrackInfo { get; }
            public string Queuer { get; }

            public string Title => TrackInfo.Title;
            public string Url => TrackInfo.Url;
            public string Thumbnail => TrackInfo.Thumbnail;
            public TimeSpan Duration => TrackInfo.Duration;
            public MusicPlatform Platform => TrackInfo.Platform;


            public QueuedTrackInfo(ITrackInfo trackInfo, string queuer)
            {
                TrackInfo = trackInfo;
                Queuer = queuer;
            }

            public ValueTask<string?> GetStreamUrl() => TrackInfo.GetStreamUrl();
        }
    }

    public sealed partial class MusicQueue : IMusicQueue
    {
        private LinkedList<QueuedTrackInfo> _tracks;

        public int Index
        {
            get
            {
                // just make sure the internal logic runs first
                // to make sure that some potential indermediate value is not returned
                lock (locker)
                {
                    return _index;
                }
            }
        }

        private int _index;

        public int Count
        {
            get
            {
                lock (locker)
                {
                    return _tracks.Count;
                }
            }
        }

        private readonly object locker = new object();

        public MusicQueue()
        {
            _index = 0;
            _tracks = new LinkedList<QueuedTrackInfo>();
        }

        public IQueuedTrackInfo Enqueue(ITrackInfo trackInfo, string queuer, out int index)
        {
            lock (locker)
            {
                var added = new QueuedTrackInfo(trackInfo, queuer);
                index = _tracks.Count;
                _tracks.AddLast(added);
                return added;
            }
        }

        public IQueuedTrackInfo EnqueueNext(ITrackInfo trackInfo, string queuer, out int index)
        {
            lock (locker)
            {
                if (_tracks.Count == 0)
                {
                    return Enqueue(trackInfo, queuer, out index);
                }

                LinkedListNode<QueuedTrackInfo> currentNode = _tracks.First;
                int i;
                for (i = 1; i <= _index; i++)
                {
                    currentNode = currentNode.Next!; // can't be null because index is always in range of the count
                }

                var added = new QueuedTrackInfo(trackInfo, queuer);
                index = i;

                _tracks.AddAfter(currentNode, added);

                return added;
            }
        }

        public void EnqueueMany(IEnumerable<ITrackInfo> tracks, string queuer)
        {
            lock (locker)
            {
                foreach (var track in tracks)
                {
                    var added = new QueuedTrackInfo(track, queuer);
                    _tracks.AddLast(added);
                }
            }
        }

        public IReadOnlyCollection<IQueuedTrackInfo> List()
        {
            lock (locker)
            {
                return _tracks.ToList();
            }
        }

        public IQueuedTrackInfo? GetCurrent(out int index)
        {
            lock (locker)
            {
                index = _index;
                return _tracks.ElementAtOrDefault(_index);
            }
        }

        public void Advance()
        {
            lock (locker)
            {
                if (++_index >= _tracks.Count)
                    _index = 0;
            }
        }

        public void Clear()
        {
            lock (locker)
            {
                _tracks.Clear();
            }
        }

        public bool SetIndex(int index)
        {
            lock (locker)
            {
                if (index < 0 || index >= _tracks.Count)
                    return false;

                _index = index;
                return true;
            }
        }

        private void RemoveAtInternal(int index, out IQueuedTrackInfo trackInfo)
        {
            var removedNode = _tracks.First;
            int i;
            for (i = 0; i < index; i++)
            {
                removedNode = removedNode.Next!;
            }

            trackInfo = removedNode.Value;
            _tracks.Remove(removedNode);

            // if it was the last song in the queue
            // wrap back to start
            if (i == Count)
                _index = 0;
            else if (i <= _index)
                --_index;
        }

        public void RemoveCurrent()
        {
            lock (locker)
            {
                if (_index < _tracks.Count)
                    RemoveAtInternal(_index, out _);
            }
        }

        public IQueuedTrackInfo? MoveTrack(int from, int to)
        {
            if (from < 0)
                throw new ArgumentOutOfRangeException(nameof(from));
            if (to < 0)
                throw new ArgumentOutOfRangeException(nameof(to));
            if (to == from)
                throw new ArgumentException($"{nameof(from)} and {nameof(to)} must be different");

            lock (locker)
            {
                if (from >= Count || to >= Count)
                    return null;

                // update current track index
                if (from == _index)
                {
                    // if the song being moved is the current track
                    // it means that it will for sure end up on the destination
                    _index = to;
                }
                else
                {
                    // moving a track from below the current track means 
                    // means it will drop down
                    if (from < _index)
                        _index--;

                    // moving a track to below the current track
                    // means it will rise up
                    if (to <= _index)
                        _index++;


                    // if both from and to are below _index - net change is + 1 - 1 = 0
                    // if from is below and to is above - net change is -1 (as the track is taken and put above)
                    // if from is above and to is below - net change is 1 (as the track is inserted under)
                    // if from is above and to is above - net change is 0
                }

                // get the node which needs to be moved
                var fromNode = _tracks.First;
                for (var i = 0; i < from; i++)
                    fromNode = fromNode.Next!;

                // remove it from the queue
                _tracks.Remove(fromNode);

                // if it needs to be added as a first node,
                // add it directly and return
                if (to == 0)
                {
                    _tracks.AddFirst(fromNode);
                    return fromNode.Value;
                }

                // else find the node at the index before the specified target
                var addAfterNode = _tracks.First;
                for (var i = 1; i < to; i++)
                    addAfterNode = addAfterNode.Next!;

                // and add after it
                _tracks.AddAfter(addAfterNode, fromNode);
                return fromNode.Value;
            }
        }

        public void Shuffle(Random rng)
        {
            lock (locker)
            {
                var list = _tracks.ToList();

                for (var i = 0; i < list.Count; i++)
                {
                    var struck = rng.Next(i, list.Count);
                    var temp = list[struck];
                    list[struck] = list[i];
                    list[i] = temp;

                    // could preserving the index during shuffling be done better?
                    if (i == _index)
                        _index = struck;
                    else if (struck == _index)
                        _index = i;
                }

                _tracks = new LinkedList<QueuedTrackInfo>(list);
            }
        }

        public bool TryRemoveAt(int index, out IQueuedTrackInfo? trackInfo, out bool isCurrent)
        {
            lock (locker)
            {
                isCurrent = false;
                trackInfo = null;

                if (index < 0 || index >= _tracks.Count)
                    return false;

                if (index == _index)
                {
                    isCurrent = true;
                }

                RemoveAtInternal(index, out trackInfo);

                return true;
            }
        }
    }
}