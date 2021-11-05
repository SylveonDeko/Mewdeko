#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Mewdeko._Extensions;
using Mewdeko.Common;
using Mewdeko.Services.Database.Models;
using Mewdeko.Voice;
using Serilog;

namespace Mewdeko.Modules.Music.Common.SongResolver.Impl
{
    public sealed class MusicPlayer : IMusicPlayer
    {
        private readonly IVoiceProxy _proxy;

        private readonly IMusicQueue _queue;
        private readonly Random _rng;
        private readonly ISongBuffer _songBuffer;
        private readonly Thread _thread;
        private readonly ITrackResolveProvider _trackResolveProvider;
        private readonly VoiceClient _vc;

        private readonly AdjustVolumeDelegate AdjustVolume;
        private int? _forceIndex;

        private bool _skipped;

        public MusicPlayer(
            IMusicQueue queue,
            ITrackResolveProvider trackResolveProvider,
            IVoiceProxy proxy,
            QualityPreset qualityPreset)
        {
            _queue = queue;
            _trackResolveProvider = trackResolveProvider;
            _proxy = proxy;
            _rng = new MewdekoRandom();

            _vc = GetVoiceClient(qualityPreset);
            if (_vc.BitDepth == 16)
                AdjustVolume = AdjustVolumeInt16;
            else
                AdjustVolume = AdjustVolumeFloat32;

            _songBuffer = new PoopyBufferImmortalized(_vc.InputLength);

            _thread = new Thread(async () => { await PlayLoop(); });
            _thread.Start();
        }

        public bool IsKilled { get; private set; }
        public bool IsStopped { get; private set; }
        public bool IsPaused { get; private set; }
        public PlayerRepeatType Repeat { get; private set; }

        public int CurrentIndex => _queue.Index;

        public float Volume { get; private set; } = 1.0f;

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
                        await TryEnqueueTrackAsync(query, queuer, false, platform).ConfigureAwait(false);
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

        public void SetRepeat(PlayerRepeatType type)
        {
            Repeat = type;
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
        {
            return _queue.List();
        }

        public IQueuedTrackInfo? GetCurrentTrack(out int index)
        {
            return _queue.GetCurrent(out index);
        }

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

            Volume = normalizedVolume;
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

        public bool TogglePause()
        {
            return IsPaused = !IsPaused;
        }

        public IQueuedTrackInfo? MoveTrack(int from, int to)
        {
            return _queue.MoveTrack(from, to);
        }

        public void Dispose()
        {
            IsKilled = true;
            OnCompleted = null;
            OnStarted = null;
            OnQueueStopped = null;
            _queue.Clear();
            _songBuffer.Dispose();
            _vc.Dispose();
        }

        private static VoiceClient GetVoiceClient(QualityPreset qualityPreset)
        {
            return qualityPreset switch
            {
                QualityPreset.Highest => new VoiceClient(),
                QualityPreset.High => new VoiceClient(
                    SampleRate._48k,
                    Bitrate._128k,
                    Channels.Two,
                    FrameDelay.Delay40
                ),
                QualityPreset.Medium => new VoiceClient(
                    SampleRate._48k,
                    Bitrate._96k,
                    Channels.Two,
                    FrameDelay.Delay40,
                    BitDepthEnum.UInt16
                ),
                QualityPreset.Low => new VoiceClient(
                    SampleRate._48k,
                    Bitrate._64k,
                    Channels.Two,
                    FrameDelay.Delay40,
                    BitDepthEnum.UInt16
                ),
                _ => throw new ArgumentOutOfRangeException(nameof(qualityPreset), qualityPreset, null)
            };
        }

        private async Task PlayLoop()
        {
            var sw = new Stopwatch();

            while (!IsKilled)
            {
                // wait until a song is available in the queue
                // or until the queue is resumed
                var track = _queue.GetCurrent(out var index);

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

                using var cancellationTokenSource = new CancellationTokenSource();
                var token = cancellationTokenSource.Token;
                try
                {
                    // light up green in vc
                    _ = _proxy.StartSpeakingAsync();

                    _ = OnStarted?.Invoke(this, track, index);

                    // make sure song buffer is ready to be (re)used
                    _songBuffer.Reset();

                    var streamUrl = await track.GetStreamUrl();
                    // start up the data source
                    using var source = FfmpegTrackDataSource.CreateAsync(
                        _vc.BitDepth,
                        streamUrl,
                        track.Platform == MusicPlatform.Local
                    );

                    // start moving data from the source into the buffer
                    // this method will return once the sufficient prebuffering is done
                    await _songBuffer.BufferAsync(source, token);

                    // start sending data
                    var ticksPerMs = 1000f / Stopwatch.Frequency;
                    sw.Start();
                    Thread.Sleep(2);

                    var delay = sw.ElapsedTicks * ticksPerMs > 3f
                        ? _vc.Delay - 16
                        : _vc.Delay - 3;

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
                            await Task.Delay(200);
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
                                if (errorCount > 0)
                                {
                                    _ = _proxy.StartSpeakingAsync();
                                    errorCount = 0;
                                }

                                // wait for slightly less than the latency
                                Thread.Sleep(delay);

                                // and then spin out the rest
                                while ((sw.ElapsedTicks - ticks) * ticksPerMs <= _vc.Delay - 0.1f)
                                    Thread.SpinWait(200);
                            }
                            else
                            {
                                // result is false is either when the gateway is being swapped 
                                // or if the bot is reconnecting, or just disconnected for whatever reason

                                // tolerate up to 15x200ms of failures (3 seconds)
                                if (++errorCount <= 15)
                                {
                                    await Task.Delay(200);
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
                catch (Win32Exception)
                {
                    IsStopped = true;
                    Log.Error("Please install ffmpeg and make sure it's added to your " +
                              "PATH environment variable before trying again");
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
                    cancellationTokenSource.Cancel();
                    // turn off green in vc
                    _ = OnCompleted?.Invoke(this, track);

                    HandleQueuePostTrack();
                    _skipped = false;

                    _ = _proxy.StopSpeakingAsync();
                    ;

                    await Task.Delay(100);
                }
            }
        }

        private bool? CopyChunkToOutput(ISongBuffer sb, VoiceClient vc)
        {
            var data = sb.Read(vc.InputLength, out var length);

            // if nothing is read from the buffer, song is finished
            if (data.Length == 0) return null;

            AdjustVolume(data, Volume);
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

            var (repeat, isStopped) = (Repeat, IsStopped);

            if (repeat == PlayerRepeatType.Track || isStopped)
                return;

            // if queue is being repeated, advance no matter what
            if (repeat == PlayerRepeatType.None)
            {
                // if this is the last song,
                // stop the queue
                if (_queue.IsLast())
                {
                    IsStopped = true;
                    OnQueueStopped?.Invoke(this);
                    return;
                }

                _queue.Advance();
                return;
            }

            _queue.Advance();
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AdjustVolumeInt16(Span<byte> audioSamples, float volume)
        {
            if (Math.Abs(volume - 1f) < 0.0001f) return;

            var samples = MemoryMarshal.Cast<byte, short>(audioSamples);

            for (var i = 0; i < samples.Length; i++)
            {
                ref var sample = ref samples[i];
                sample = (short)(sample * volume);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AdjustVolumeFloat32(Span<byte> audioSamples, float volume)
        {
            if (Math.Abs(volume - 1f) < 0.0001f) return;

            var samples = MemoryMarshal.Cast<byte, float>(audioSamples);

            for (var i = 0; i < samples.Length; i++)
            {
                ref var sample = ref samples[i];
                sample = sample * volume;
            }
        }

        public event Func<IMusicPlayer, IQueuedTrackInfo, Task>? OnCompleted;
        public event Func<IMusicPlayer, IQueuedTrackInfo, int, Task>? OnStarted;
        public event Func<IMusicPlayer, Task>? OnQueueStopped;

        private delegate void AdjustVolumeDelegate(Span<byte> data, float volume);
    }
}