using NadekoBot.Extensions;
using NadekoBot.Modules.Music.Common.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NadekoBot.Common;

namespace NadekoBot.Modules.Music.Common
{
    public sealed class MusicQueue : IDisposable
    {
        private LinkedList<SongInfo> Songs { get; set; } = new LinkedList<SongInfo>();
        private int _currentIndex = 0;
        public int CurrentIndex
        {
            get
            {
                return _currentIndex;
            }
            set
            {
                lock (locker)
                {
                    if (Songs.Count == 0)
                        _currentIndex = 0;
                    else
                        _currentIndex = value %= Songs.Count;
                }
            }
        }
        public (int Index, SongInfo Song) Current
        {
            get
            {
                var cur = CurrentIndex;
                return (cur, Songs.ElementAtOrDefault(cur));
            }
        }

        private readonly object locker = new object();
        private TaskCompletionSource<bool> nextSource { get; } = new TaskCompletionSource<bool>();
        public int Count
        {
            get
            {
                lock (locker)
                {
                    return Songs.Count;
                }
            }
        }

        private uint _maxQueueSize;
        public uint MaxQueueSize
        {
            get => _maxQueueSize;
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(value));

                lock (locker)
                {
                    _maxQueueSize = value;
                }
            }
        }

        public void Add(SongInfo song)
        {
            song.ThrowIfNull(nameof(song));
            lock (locker)
            {
                if(MaxQueueSize != 0 && Songs.Count >= MaxQueueSize)
                    throw new QueueFullException();
                Songs.AddLast(song);
            }
        }

        public int AddNext(SongInfo song)
        {
            song.ThrowIfNull(nameof(song));
            lock (locker)
            {
                if (MaxQueueSize != 0 && Songs.Count >= MaxQueueSize)
                    throw new QueueFullException();
                var curSong = Current.Song;
                if (curSong == null)
                {
                    Songs.AddLast(song);
                    return Songs.Count;
                }

                var songlist = Songs.ToList();
                songlist.Insert(CurrentIndex + 1, song);
                Songs = new LinkedList<SongInfo>(songlist);
                return CurrentIndex + 1;
            }
        }

        public void Next(int skipCount = 1)
        {
            lock(locker)
                CurrentIndex += skipCount;
        }

        public void Dispose()
        {
            Clear();
        }

        public SongInfo RemoveAt(int index)
        {
            lock (locker)
            {
                if (index < 0 || index >= Songs.Count)
                    throw new ArgumentOutOfRangeException(nameof(index));

                var current = Songs.First.Value;
                for (int i = 0; i < Songs.Count; i++)
                {
                    if (i == index)
                    {
                        current = Songs.ElementAt(index);
                        Songs.Remove(current);
                        if (CurrentIndex != 0)
                        {
                            if (CurrentIndex >= index)
                            {
                                --CurrentIndex;
                            }
                        }
                        break;
                    }
                }
                return current;
            }
        }

        public void Clear()
        {
            lock (locker)
            {
                Songs.Clear();
                CurrentIndex = 0;
            }
        }

        public (int CurrentIndex, SongInfo[] Songs) ToArray()
        {
            lock (locker)
            {
                return (CurrentIndex, Songs.ToArray());
            }
        }

        public List<SongInfo> ToList()
        {
            lock (locker)
            {
                return Songs.ToList();
            }
        }

        public void ResetCurrent()
        {
            lock (locker)
            {
                CurrentIndex = 0;
            }
        }

        public void Random()
        {
            lock (locker)
            {
                CurrentIndex = new NadekoRandom().Next(Songs.Count);
            }
        }

        public SongInfo MoveSong(int n1, int n2)
        {
            lock (locker)
            {
                var currentSong = Current.Song;
                var playlist = Songs.ToList();
                if (n1 >= playlist.Count || n2 >= playlist.Count || n1 == n2)
                    return null;

                var s = playlist[n1];

                playlist.RemoveAt(n1);
                playlist.Insert(n2, s);

                Songs = new LinkedList<SongInfo>(playlist);


                if (currentSong != null)
                    CurrentIndex = playlist.IndexOf(currentSong);

                return s;
            }
        }

        public void RemoveSong(SongInfo song)
        {
            lock (locker)
            {
                Songs.Remove(song);
            }
        }

        public bool IsLast()
        {
            lock (locker)
                return CurrentIndex == Songs.Count - 1;
        }
    }
}
//O O [O] O O O O
//
// 3