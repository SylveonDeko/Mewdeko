#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Ayu.Discord.Voice;
using Mewdeko.Common;
using Mewdeko.Extensions;
using Mewdeko.Modules.Music;
using Serilog;

namespace Mewdeko.Core.Modules.Music
{
    public sealed class MusicPlayer : IMusicPlayer
    {
        private static readonly VoiceClient _vc = new VoiceClient(frameDelay: FrameDelay.Delay20);

        public bool IsKilled { get; set; }
        public bool IsStopped { get; set; }
        public bool IsPaused { get; set; }
        public bool IsRepeatingCurrentSong { get; set; }
        public bool IsAutoDelete { get; set; }
        public bool IsRepeatingQueue { get; set; } = true;

        public int CurrentIndex => _queue.Index;
        
        public float Volume => _volume;
        private float _volume = 1.0f;

        private readonly IMusicQueue _queue;
        private readonly ITrackResolveProvider _trackResolveProvider;
        private readonly IVoiceProxy _proxy;
        private readonly ISongBuffer _songBuffer;

        private bool _skipped;
        private int? _forceIndex;
        private readonly Thread _thread;
        private readonly Random _rng;

        public MusicPlayer(IMusicQueue queue, ITrackResolveProvider trackResolveProvider, IVoiceProxy proxy)
        {
            _queue = queue;
            _trackResolveProvider = trackResolveProvider;
            _proxy = proxy;
            _songBuffer = new PoopyBufferImmortalized();
            _rng = new MewdekoRandom();

            _thread = new Thread(async () =>
            {
                await PlayLoop();
            });
            _thread.Start();
        }

        private async Task PlayLoop()
        {
            var sw = new Stopwatch();

            while (!IsKilled)
            {
                // wait until a song is available in the queue
                // or until the queue is resumed
                var track = _queue.GetCurrent(out int index);
                
                if (track is null || IsStopped)
                {
                    await Task.Delay(500);
                    continue;
                }

                if (_skipped)
                {
                    _skipped = false;
                    _queue.Advance();
                    continue;
                }

                var trackCancellationSource = new CancellationTokenSource();
                var cancellationToken = trackCancellationSource.Token;
                try
                {
                    // light up green in vc
                    _ = _proxy.StartSpeakingAsync();

                    _ = OnStarted?.Invoke(this, track, index);
                    
                    // make sure song buffer is ready to be (re)used
                    _songBuffer.Reset();

                    var streamUrl = await track.GetStreamUrl();
                    // start up the data source
                    var source = FfmpegTrackDataSource.CreateAsync(
                        streamUrl,
                        track.Platform == MusicPlatform.Local
                    );

                    // start moving data from the source into the buffer
                    // this method will return once the sufficient prebuffering is done
                    await _songBuffer.BufferAsync(source, cancellationToken);


                    // start sending data
                    var ticksPerMs = 1000f / Stopwatch.Frequency;
                    var errorCount = 0;
                    while (!IsStopped && !IsKilled)
                    {
                        // doing the skip this way instead of in the condition
                        // ensures that a song will for sure be skipped
                        if (_skipped)
                        {
                            _skipped = false;
                            break;
                        }

                        if (IsPaused)
                        {
                            await Task.Delay(200, cancellationToken);
                            continue;
                        }

                        sw.Restart();
                        var ticks = sw.ElapsedTicks;
                        try
                        {
                            var result = CopyChunkToOutput(_songBuffer, _vc);

                            // if song is finished
                            if (result is null)
                                break;
                            
                            if (result is true)
                            {
                                // wait for slightly less than the latency
                                Thread.Sleep(_vc.Delay - 2);

                                // and then spin out the rest
                                // subjectively this seemed to work better
                                // due to apparent imprecision of sleep
                                while ((sw.ElapsedTicks - ticks) * ticksPerMs < _vc.Delay)
                                    Thread.SpinWait(50);

                                if (errorCount > 0)
                                {
                                    _ = _proxy.StartSpeakingAsync();
                                    errorCount = 0;
                                }
                            }
                            else
                            {
                                // result is false is either when the gateway is being swapped 
                                // or if the bot is reconnecting, or just disconnected for whatever reason

                                // tolerate up to 15x200ms of failures (3 seconds)
                                if (++errorCount <= 15)
                                {
                                    await Task.Delay(200, cancellationToken);
                                    continue;
                                }
                                
                                Log.Warning("Can't send data to voice channel");

                                IsStopped = true;
                                // if errors are happening for more than 3 seconds
                                // Stop the player
                                break;
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Warning(ex, "Something went wrong sending voice data: {ErrorMessage}", ex.Message);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    Log.Information("Song skipped");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Unknown error in music loop: {ErrorMessage}", ex.Message);
                }
                finally
                {
                    // turn off green in vc
                    trackCancellationSource.Cancel();
                    HandleQueuePostTrack();
                    
                    _ = _proxy.StopSpeakingAsync();
                    _ = OnCompleted?.Invoke(this, track);
                    
                    _skipped = false;
                    await Task.Delay(100);
                }
            }
        }
        
        private bool? CopyChunkToOutput(ISongBuffer sb, VoiceClient vc)
        {
            var data = sb.Read(vc.FrameSize, out var length);

            // if nothing is read from the buffer, song is finished
            if (data.Length == 0)
            {
                return null;
            }

            AdjustVolume(data, _volume);
            return _proxy.SendPcmFrame(vc, data, length);
        }

        private void HandleQueuePostTrack()
        {
            if (_forceIndex is int forceIndex)
            {
                _queue.SetIndex(forceIndex);
                _forceIndex = null;
                return;
            }

            if (IsRepeatingCurrentSong || IsStopped)
                return;

            // autodelete is basically advance, so if it's enabled
            // don't advance
            if(IsAutoDelete)
                _queue.RemoveCurrent();
            
            // if queue is being repeated, advance no matter what
            if (!IsRepeatingQueue)
            {
                // if this is the last song,
                // stop the queue
                if (_queue.Index < _queue.Count - 1)
                {
                    IsStopped = true;
                    return;
                }
                
                if(!IsAutoDelete)
                    _queue.Advance();
                
                return;
            }
            
            if(!IsAutoDelete)
                _queue.Advance();
        }

        

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AdjustVolume(Span<byte> audioSamples, float volume)
        {
            if (Math.Abs(volume - 1f) < 0.0001f) return;
        
            var samples = MemoryMarshal.Cast<byte, short>(audioSamples);
        
            for (var i = 0; i < samples.Length; i++)
            {
                ref var sample = ref samples[i];
                sample = (short) (sample * volume);
            }
        }

        public async Task<(IQueuedTrackInfo? QueuedTrack, int Index)> TryEnqueueTrackAsync(
            string query, 
            string queuer,
            bool asNext,
            MusicPlatform? forcePlatform = null)
        {
            var song = await _trackResolveProvider.QuerySongAsync(query, forcePlatform);
            if (song is null)
                return default;

            int index;

            if (asNext)
                return (_queue.EnqueueNext(song, queuer, out index), index);

            return (_queue.Enqueue(song, queuer, out index), index);
        }
        
        public async Task EnqueueManyAsync(IEnumerable<(string Query, MusicPlatform Platform)> queries, string queuer)
        {
            var errorCount = 0;
            foreach (var chunk in queries.Chunk(5))
            {
                if (IsKilled)
                    break;
                
                var queueTasks = chunk.Select(async data =>
                {
                    var (query, platform) = data;
                    try
                    {
                        await TryEnqueueTrackAsync(query, queuer, false, forcePlatform: platform).ConfigureAwait(false);
                        errorCount = 0;
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Error resolving {MusicPlatform} Track {TrackQuery}", platform, query);
                        ++errorCount;
                    }
                });

                await Task.WhenAll(queueTasks);
                await Task.Delay(1000);
                
                // > 10 errors in a row = kill
                if (errorCount > 10)
                    break;
            }
        }
        
        public void EnqueueTrack(ITrackInfo track, string queuer)
        {
            _queue.Enqueue(track, queuer, out _);
        }

        public void EnqueueTracks(IEnumerable<ITrackInfo> tracks, string queuer)
        {
            _queue.EnqueueMany(tracks, queuer);
        }

        public void ShuffleQueue()
        {
            _queue.Shuffle(_rng);
        }

        public void Stop()
        {
            IsStopped = true;
        }

        public void Clear()
        {
            _queue.Clear();
            _skipped = true;
        }

        public IReadOnlyCollection<IQueuedTrackInfo> GetQueuedTracks()
            => _queue.List();

        public IQueuedTrackInfo? GetCurrentTrack(out int index)
            => _queue.GetCurrent(out index);

        public void Next()
        {
            _skipped = true;
            IsStopped = false;
            IsPaused = false;
        }

        public bool MoveTo(int index)
        {
            if (_queue.SetIndex(index))
            {
                _forceIndex = index;
                _skipped = true;
                IsStopped = false;
                IsPaused = false;
                return true;
            }

            return false;
        }

        public void SetVolume(int newVolume)
        {
            var normalizedVolume = newVolume / 100f;
            if (normalizedVolume < 0f || normalizedVolume > 1f)
                throw new ArgumentOutOfRangeException(nameof(newVolume), "Volume must be in range 0-100");

            _volume = normalizedVolume;
        }

        public void Kill()
        {
            IsKilled = true;
            IsStopped = true;
            IsPaused = false;
            _skipped = true;
        }

        public bool TryRemoveTrackAt(int index, out IQueuedTrackInfo? trackInfo)
        {
            if (!_queue.TryRemoveAt(index, out trackInfo, out var isCurrent))
                return false;

            if (isCurrent)
                _skipped = true;

            return true;
        }

        public bool ToggleRcs() => IsRepeatingCurrentSong = !IsRepeatingCurrentSong;
        public bool ToggleRpl() => IsRepeatingQueue = !IsRepeatingQueue;
        public bool ToggleAd() => IsAutoDelete = !IsAutoDelete;
        public bool TogglePause() => IsPaused = !IsPaused;
        public IQueuedTrackInfo? MoveTrack(int from, int to) => _queue.MoveTrack(from, to);

        public void Dispose()
        {
            IsKilled = true;
            OnCompleted = null;
            OnStarted = null;
            _queue.Clear();
            _songBuffer.Dispose();
            try { _thread.Abort(); } catch { }
        }

        public event Func<IMusicPlayer, IQueuedTrackInfo, Task>? OnCompleted;
        public event Func<IMusicPlayer, IQueuedTrackInfo, int, Task>? OnStarted;
    }
}