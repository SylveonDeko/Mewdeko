// using Discord;
// using Discord.Audio;
// using System;
// using System.Threading;
// using System.Threading.Tasks;
// using System.Linq;
// using System.Runtime.InteropServices;
// using Mewdeko._Extensions;
// using Mewdeko.Common.Collections;
// using Mewdeko.Modules.Music.Services;
// using Mewdeko.Services;
// using Mewdeko.Services.Database.Models;
// using Discord.WebSocket;
// using Serilog;
//
// namespace Mewdeko.Modules.Music.Common
// {
//     public enum StreamState
//     {
//         Resolving,
//         Queued,
//         Playing,
//         Completed
//     }
//     public class MusicPlayer
//     {
//         private readonly Thread _player;
//         public IVoiceChannel VoiceChannel { get; private set; }
//
//         public ITextChannel OriginalTextChannel { get; set; }
//         
//         private MusicQueue Queue { get; } = new MusicQueue();
//
//         public bool Exited { get; set; } = false;
//         public bool Stopped { get; private set; } = false;
//         public float Volume { get; private set; } = 1.0f;
//         public bool Paused => PauseTaskSource != null;
//         private TaskCompletionSource<bool> PauseTaskSource { get; set; } = null;
//
//         public string PrettyVolume => $"🔉 {(int)(Volume * 100)}%";
//         public string PrettyCurrentTime
//         {
//             get
//             {
//                 var time = CurrentTime.ToString(@"mm\:ss");
//                 var hrs = (int)CurrentTime.TotalHours;
//
//                 if (hrs > 0)
//                     return hrs + ":" + time;
//                 else
//                     return time;
//             }
//         }
//         public string PrettyFullTime => PrettyCurrentTime + " / " + (Queue.Current.Song?.PrettyTotalTime ?? "?");
//         private CancellationTokenSource SongCancelSource { get; set; }
//         public ITextChannel OutputTextChannel { get; set; }
//         public (int Index, SongInfo Current) Current
//         {
//             get
//             {
//                 if (Stopped)
//                     return (0, null);
//                 return Queue.Current;
//             }
//         }
//
//         public bool RepeatCurrentSong { get; private set; }
//         public bool Shuffle { get; private set; }
//         public bool Autoplay { get; private set; }
//         public bool RepeatPlaylist { get; private set; } = false;
//         public uint MaxQueueSize
//         {
//             get => Queue.MaxQueueSize;
//             set { lock (locker) Queue.MaxQueueSize = value; }
//         }
//         private bool _fairPlay;
//         public bool FairPlay
//         {
//             get => _fairPlay;
//             set
//             {
//                 if (value)
//                 {
//                     var (Index, Song) = Queue.Current;
//                     if (Song != null)
//                         RecentlyPlayedUsers.Add(Song.QueuerName);
//                 }
//                 else
//                 {
//                     RecentlyPlayedUsers.Clear();
//                 }
//
//                 _fairPlay = value;
//             }
//         }
//         public bool AutoDelete { get; set; }
//         public uint MaxPlaytimeSeconds { get; set; }
//
//
//         const int _frameBytes = 3840;
//         const float _miliseconds = 20.0f;
//         public TimeSpan CurrentTime => TimeSpan.FromSeconds(_bytesSent / (float)_frameBytes / (1000 / _miliseconds));
//
//         private int _bytesSent = 0;
//
//         private IAudioClient _audioClient;
//         private readonly object locker = new object();
//         private MusicService _musicService;
//
//         #region events
//         public event Action<MusicPlayer, (int Index, SongInfo Song)> OnStarted;
//         public event Action<MusicPlayer, SongInfo> OnCompleted;
//         public event Action<MusicPlayer, bool> OnPauseChanged;
//         #endregion
//
//         private bool manualSkip = false;
//         private bool manualIndex = false;
//         private bool newVoiceChannel = false;
//         private readonly IGoogleApiService _google;
//
//         private bool cancel = false;
//
//         private ConcurrentHashSet<string> RecentlyPlayedUsers { get; } = new ConcurrentHashSet<string>();
//         public TimeSpan TotalPlaytime
//         {
//             get
//             {
//                 var songs = Queue.ToArray().Songs;
//                 return songs.Any(s => s.TotalTime == TimeSpan.MaxValue)
//                     ? TimeSpan.MaxValue
//                     : new TimeSpan(songs.Sum(s => s.TotalTime.Ticks));
//             }
//         }
//
//         public MusicPlayer(MusicService musicService, MusicSettings ms, IGoogleApiService google,
//             IVoiceChannel vch, ITextChannel original, float volume)
//         {
//             this.Volume = volume;
//             this.VoiceChannel = vch;
//             this.OriginalTextChannel = original;
//             this.SongCancelSource = new CancellationTokenSource();
//             if (ms.MusicChannelId is ulong cid)
//             {
//                 this.OutputTextChannel = ((SocketGuild)original.Guild).GetTextChannel(cid) ?? original;
//             }
//             else
//             {
//                 this.OutputTextChannel = original;
//             }
//             this._musicService = musicService;
//             this.AutoDelete = ms.SongAutoDelete;
//             this._google = google;
//
//             _player = new Thread(new ThreadStart(PlayerLoop))
//             {
//                 Priority = ThreadPriority.AboveNormal
//             };
//             _player.Start();
//         }
//
//         private async void PlayerLoop()
//         {
//             while (!Exited)
//             {
//                 _bytesSent = 0;
//                 cancel = false;
//                 CancellationToken cancelToken;
//                 (int Index, SongInfo Song) data;
//                 lock (locker)
//                 {
//                     data = Queue.Current;
//                     cancelToken = SongCancelSource.Token;
//                     manualSkip = false;
//                     manualIndex = false;
//                 }
//                 if (data.Song != null)
//                 {
//                     Log.Information("Starting");
//                     AudioOutStream pcm = null;
//                     SongBuffer b = null;
//                     try
//                     {
//                         var streamUrl = await data.Song.Uri().ConfigureAwait(false);
//                         b = new SongBuffer(streamUrl, data.Song.ProviderType == MusicType.Local);
//                         //Log.Information("Created buffer, buffering...");
//
//                         //var bufferTask = b.StartBuffering(cancelToken);
//                         //var timeout = Task.Delay(10000);
//                         //if (Task.WhenAny(bufferTask, timeout) == timeout)
//                         //{
//                         //    Log.Information("Buffering failed due to a timeout.");
//                         //    continue;
//                         //}
//                         //else if (!bufferTask.Result)
//                         //{
//                         //    Log.Information("Buffering failed due to a cancel or error.");
//                         //    continue;
//                         //}
//                         //Log.Information("Buffered. Getting audio client...");
//                         var ac = await GetAudioClient().ConfigureAwait(false);
//                         Log.Information("Got Audio client");
//                         if (ac == null)
//                         {
//                             Log.Information("Can't join");
//                             await Task.Delay(900, cancelToken).ConfigureAwait(false);
//                             // just wait some time, maybe bot doesn't even have perms to join that voice channel,
//                             // i don't want to spam connection attempts
//                             continue;
//                         }
//                         b.StartBuffering();
//                         await Task.WhenAny(Task.Delay(10000), b.PrebufferingCompleted.Task).ConfigureAwait(false);
//                         pcm = ac.CreatePCMStream(AudioApplication.Music, bufferMillis: 1, packetLoss: 5);
//                         Log.Information("Created pcm stream");
//                         OnStarted?.Invoke(this, data);
//
//                         while (MaxPlaytimeSeconds <= 0 || MaxPlaytimeSeconds >= CurrentTime.TotalSeconds)
//                         {
//                             var buffer = b.Read(3840);
//                             if (buffer.Length == 0)
//                                 break;
//                             AdjustVolume(buffer, Volume);
//                             await pcm.WriteAsync(buffer, 0, buffer.Length, cancelToken).ConfigureAwait(false);
//                             unchecked { _bytesSent += buffer.Length; }
//
//                             await (PauseTaskSource?.Task ?? Task.CompletedTask).ConfigureAwait(false);
//                         }
//                     }
//                     catch (OperationCanceledException)
//                     {
//                         Log.Information("Song Canceled");
//                         cancel = true;
//                     }
//                     catch (Exception ex)
//                     {
//                         Log.Warning(ex, "Error sending song data");
//                     }
//                     finally
//                     {
//                         if (pcm != null)
//                         {
//                             // flush is known to get stuck from time to time,
//                             // just skip flushing if it takes more than 1 second
//                             var flushCancel = new CancellationTokenSource();
//                             var flushToken = flushCancel.Token;
//                             var flushDelay = Task.Delay(1000, flushToken);
//                             await Task.WhenAny(flushDelay, pcm.FlushAsync(flushToken)).ConfigureAwait(false);
//                             flushCancel.Cancel();
//                             pcm.Dispose();
//                         }
//
//                         if (b != null)
//                             b.Dispose();
//
//                         OnCompleted?.Invoke(this, data.Song);
//
//                         if (_bytesSent == 0 && !cancel)
//                         {
//                             lock (locker)
//                                 Queue.RemoveSong(data.Song);
//                             Log.Information("Song removed because it can't play");
//                         }
//                     }
//                     try
//                     {
//                         //if repeating current song, just ignore other settings,
//                         // and play this song again (don't change the index)
//                         // ignore rcs if song is manually skipped
//
//                         int queueCount;
//                         bool stopped;
//                         int currentIndex;
//                         lock (locker)
//                         {
//                             queueCount = Queue.Count;
//                             stopped = Stopped;
//                             currentIndex = Queue.CurrentIndex;
//                         }
//
//                         if (AutoDelete && !RepeatCurrentSong && !RepeatPlaylist && data.Song != null)
//                         {
//                             Queue.RemoveSong(data.Song);
//                         }
//
//                         if (!manualIndex && (!RepeatCurrentSong || manualSkip))
//                         {
//                             if (Shuffle)
//                             {
//                                 Log.Information("Random song");
//                                 Queue.Random(); //if shuffle is set, set current song index to a random number
//                             }
//                             else
//                             {
//                                 //if last song, and autoplay is enabled, and if it's a youtube song
//                                 // do autplay magix
//                                 if (queueCount - 1 == data.Index && Autoplay && data.Song?.ProviderType == MusicType.YouTube)
//                                 {
//                                     try
//                                     {
//                                         Log.Information("Loading related song");
//                                         await _musicService.TryQueueRelatedSongAsync(data.Song, OutputTextChannel, VoiceChannel).ConfigureAwait(false);
//                                         if (!AutoDelete)
//                                             Queue.Next();
//                                     }
//                                     catch
//                                     {
//                                         Log.Information("Loading related song failed");
//                                     }
//                                 }
//                                 else if (FairPlay)
//                                 {
//                                     lock (locker)
//                                     {
//                                         Log.Information("Next fair song");
//                                         var queueList = Queue.ToList();
//                                         var q = queueList.Shuffle().ToArray();
//
//                                         bool found = false;
//                                         for (var i = 0; i < q.Length; i++) //first try to find a queuer who didn't have their song played recently
//                                         {
//                                             var item = q[i];
//                                             if (RecentlyPlayedUsers.Add(item.QueuerName)) // if it's found, set current song to that index
//                                             {
//                                                 Queue.CurrentIndex = queueList.IndexOf(q[i]);
//                                                 found = true;
//                                                 break;
//                                             }
//                                         }
//                                         if (!found) //if it's not
//                                         {
//                                             RecentlyPlayedUsers.Clear(); //clear all recently played users (that means everyone from the playlist has had their song played)
//                                             Queue.Random(); //go to a random song (to prevent looping on the first few songs)
//                                             var cur = Current;
//                                             if (cur.Current != null) // add newely scheduled song's queuer to the recently played list
//                                                 RecentlyPlayedUsers.Add(cur.Current.QueuerName);
//                                         }
//                                     }
//                                 }
//                                 else if (queueCount - 1 == data.Index && !RepeatPlaylist && !manualSkip)
//                                 {
//                                     Log.Information("Stopping because repeatplaylist is disabled");
//                                     lock (locker)
//                                     {
//                                         Stop();
//                                     }
//                                 }
//                                 else
//                                 {
//                                     Log.Information("Next song");
//                                     lock (locker)
//                                     {
//                                         if (!Stopped)
//                                             if (!AutoDelete)
//                                                 Queue.Next();
//                                     }
//                                 }
//                             }
//                         }
//                     }
//                     catch (Exception ex)
//                     {
//                         Log.Error(ex, "Error in queue");
//                     }
//                 }
//                 do
//                 {
//                     await Task.Delay(500).ConfigureAwait(false);
//                 }
//                 while ((Queue.Count == 0 || Stopped) && !Exited);
//             }
//         }
//
//         private async Task<IAudioClient> GetAudioClient(bool reconnect = false)
//         {
//             if (_audioClient == null ||
//                 _audioClient.ConnectionState != ConnectionState.Connected ||
//                 reconnect ||
//                 newVoiceChannel)
//                 try
//                 {
//                     try
//                     {
//                         var t = _audioClient?.StopAsync();
//                         if (t != null)
//                         {
//
//                             Log.Information("Stopping audio client");
//                             await t.ConfigureAwait(false);
//
//                             Log.Information("Disposing audio client");
//                             _audioClient.Dispose();
//                         }
//                     }
//                     catch
//                     {
//                     }
//                     newVoiceChannel = false;
//
//                     var curUser = await VoiceChannel.Guild.GetCurrentUserAsync().ConfigureAwait(false);
//                     if (curUser.VoiceChannel != null)
//                     {
//                         Log.Information("Connecting");
//                         var ac = await VoiceChannel.ConnectAsync().ConfigureAwait(false);
//                         Log.Information("Connected, stopping");
//                         await ac.StopAsync().ConfigureAwait(false);
//                         Log.Information("Disconnected");
//                         await Task.Delay(1000).ConfigureAwait(false);
//                     }
//                     Log.Information("Connecting");
//                     _audioClient = await VoiceChannel.ConnectAsync().ConfigureAwait(false);
//                 }
//                 catch (Exception ex)
//                 {
//                     Log.Warning("Error while getting audio client: {0}", ex.ToString());
//                     return null;
//                 }
//             return _audioClient;
//         }
//
//         public int Enqueue(SongInfo song, bool forcePlay = false)
//         {
//             lock (locker)
//             {
//                 if (Exited)
//                     return -1;
//                 Queue.Add(song);
//                 var result = Queue.Count - 1;
//
//                 if (forcePlay)
//                 {
//                     if (Stopped)
//                     {
//                         Stopped = false;
//                         SetIndex(result);
//                     }
//                     Unpause();
//                 }
//                 return result;
//             }
//         }
//
//         public int EnqueueNext(SongInfo song, bool forcePlay = false)
//         {
//             lock (locker)
//             {
//                 if (Exited)
//                     return -1;
//                 var toReturn = Queue.AddNext(song);
//                 if (forcePlay)
//                 {
//                     Unpause();
//                     if (Stopped)
//                     {
//                         SetIndex(toReturn);
//                     }
//                 }
//                 return toReturn;
//             }
//         }
//
//         public void SetIndex(int index)
//         {
//             if (index < 0)
//                 throw new ArgumentOutOfRangeException(nameof(index));
//             lock (locker)
//             {
//                 if (Exited)
//                     return;
//                 if (AutoDelete && index >= Queue.CurrentIndex && index > 0)
//                     index--;
//                 Queue.CurrentIndex = index;
//                 manualIndex = true;
//                 Stopped = false;
//                 CancelCurrentSong();
//             }
//         }
//
//         public void Next(int skipCount = 1)
//         {
//             lock (locker)
//             {
//                 if (Exited)
//                     return;
//                 manualSkip = true;
//                 // if player is stopped, and user uses .n, it should play current song.
//                 // It's a bit weird, but that's the least annoying solution
//                 if (!Stopped)
//                     if (!RepeatPlaylist && Queue.IsLast() && !Autoplay) // if it's the last song in the queue, and repeat playlist is disabled
//                     { //stop the queue
//                         Stop();
//                         return;
//                     }
//                     else
//                         Queue.Next(skipCount - 1);
//                 else
//                     Queue.CurrentIndex = 0;
//                 Stopped = false;
//                 CancelCurrentSong();
//                 Unpause();
//             }
//         }
//
//         public void Stop(bool clearQueue = false)
//         {
//             lock (locker)
//             {
//                 Stopped = true;
//                 Autoplay = false;
//                 //Queue.ResetCurrent();
//                 if (clearQueue)
//                     Queue.Clear();
//                 Unpause();
//                 CancelCurrentSong();
//             }
//         }
//
//         private void Unpause()
//         {
//             lock (locker)
//             {
//                 if (PauseTaskSource != null)
//                 {
//                     PauseTaskSource.TrySetResult(true);
//                     PauseTaskSource = null;
//                 }
//             }
//         }
//
//         public void TogglePause()
//         {
//             lock (locker)
//             {
//                 if (PauseTaskSource == null)
//                     PauseTaskSource = new TaskCompletionSource<bool>();
//                 else
//                 {
//                     Unpause();
//                 }
//             }
//             OnPauseChanged?.Invoke(this, PauseTaskSource != null);
//         }
//
//         public void SetVolume(int volume)
//         {
//             if (volume < 0 || volume > 100)
//                 throw new ArgumentOutOfRangeException(nameof(volume));
//             lock (locker)
//             {
//                 Volume = ((float)volume) / 100;
//             }
//         }
//
//         public SongInfo RemoveAt(int index)
//         {
//             lock (locker)
//             {
//                 var (Index, Song) = Queue.Current;
//                 var toReturn = Queue.RemoveAt(index);
//                 if (Index == index)
//                     Next();
//                 return toReturn;
//             }
//         }
//
//         private void CancelCurrentSong()
//         {
//             lock (locker)
//             {
//                 var cs = SongCancelSource;
//                 SongCancelSource = new CancellationTokenSource();
//                 cs.Cancel();
//             }
//         }
//
//         public void ClearQueue()
//         {
//             lock (locker)
//             {
//                 Queue.Clear();
//             }
//         }
//
//         public (int CurrentIndex, SongInfo[] Songs) QueueArray()
//         {
//             lock (locker)
//                 return Queue.ToArray();
//         }
//
//         //aidiakapi ftw
//         // public static unsafe byte[] AdjustVolume(byte[] audioSamples, float volume)
//         // {
//         //     if (Math.Abs(volume - 1f) < 0.0001f) return audioSamples;
//         //
//         //     // 16-bit precision for the multiplication
//         //     var volumeFixed = (int)Math.Round(volume * 65536d);
//         //
//         //     var count = audioSamples.Length / 2;
//         //
//         //     fixed (byte* srcBytes = audioSamples)
//         //     {
//         //         var src = (short*)srcBytes;
//         //
//         //         for (var i = count; i != 0; i--, src++)
//         //             *src = (short)(((*src) * volumeFixed) >> 16);
//         //     }
//         //
//         //     return audioSamples;
//         // }
//         
//         private static void AdjustVolume(byte[] audioSamples, float volume)
//         {
//             if (Math.Abs(volume - 1f) < 0.0001f) return;
//             
//             var samples = MemoryMarshal.Cast<byte, short>(audioSamples);
//
//             for (var i = 0; i < samples.Length; i++)
//             {
//                 ref var sample = ref samples[i];
//                 sample = (short) (sample * volume);
//             }
//         }
//
//         public bool ToggleRepeatSong()
//         {
//             lock (locker)
//             {
//                 return RepeatCurrentSong = !RepeatCurrentSong;
//             }
//         }
//
//         public async Task Destroy()
//         {
//             Log.Information("Destroying");
//             lock (locker)
//             {
//                 Stop();
//                 Exited = true;
//                 Unpause();
//
//                 OnCompleted = null;
//                 OnPauseChanged = null;
//                 OnStarted = null;
//             }
//             var ac = _audioClient;
//             if (ac != null)
//                 await ac.StopAsync().ConfigureAwait(false);
//         }
//
//         public bool ToggleShuffle()
//         {
//             lock (locker)
//             {
//                 return Shuffle = !Shuffle;
//             }
//         }
//
//         public bool ToggleAutoplay()
//         {
//             lock (locker)
//             {
//                 return Autoplay = !Autoplay;
//             }
//         }
//
//         public bool ToggleRepeatPlaylist()
//         {
//             lock (locker)
//             {
//                 return RepeatPlaylist = !RepeatPlaylist;
//             }
//         }
//
//         public async Task SetVoiceChannel(IVoiceChannel vch)
//         {
//             lock (locker)
//             {
//                 if (Exited)
//                     return;
//                 VoiceChannel = vch;
//             }
//             _audioClient = await vch.ConnectAsync().ConfigureAwait(false);
//         }
//
//         public async Task UpdateSongDurationsAsync()
//         {
//             var (_, songs) = Queue.ToArray();
//             var toUpdate = songs
//                 .Where(x => x.ProviderType == MusicType.YouTube
//                     && x.TotalTime == TimeSpan.Zero);
//
//             var vIds = toUpdate.Select(x => x.VideoId);
//             if (!vIds.Any())
//                 return;
//
//             var durations = await _google.GetVideoDurationsAsync(vIds).ConfigureAwait(false);
//
//             foreach (var x in toUpdate)
//             {
//                 if (durations.TryGetValue(x.VideoId, out var dur))
//                     x.TotalTime = dur;
//             }
//         }
//
//         public SongInfo MoveSong(int n1, int n2)
//             => Queue.MoveSong(n1, n2);
//
//         public void SetMusicChannelToOriginal()
//         {
//             this.OutputTextChannel = OriginalTextChannel;
//         }
//
//         //// this should be written better
//         //public TimeSpan TotalPlaytime =>
//         //    _playlist.Any(s => s.TotalTime == TimeSpan.MaxValue) ?
//         //    TimeSpan.MaxValue :
//         //    new TimeSpan(_playlist.Sum(s => s.TotalTime.Ticks));
//     }
// }

