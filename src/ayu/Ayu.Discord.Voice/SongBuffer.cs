using Serilog;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Ayu.Discord.Voice
{
    public interface ISongBuffer : IDisposable
    {
        Span<byte> Read(int toRead, out int read);
        Task<bool> BufferAsync(ITrackDataSource source, CancellationToken cancellationToken);
        void Reset();
    }

    public interface ITrackDataSource
    {
        public int Read(byte[] output);
    }

    public sealed class FfmpegTrackDataSource : ITrackDataSource
    {
        private Process _p;

        private readonly string _streamUrl;
        private readonly bool _isLocal;

        private FfmpegTrackDataSource(string streamUrl, bool isLocal)
        {
            this._streamUrl = streamUrl;
            this._isLocal = isLocal;
        }

        public static FfmpegTrackDataSource CreateAsync(string streamUrl, bool isLocal)
        {
            try
            {
                var source = new FfmpegTrackDataSource(streamUrl, isLocal);
                source.StartFFmpegProcess();
                return source;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                Log.Error(@"You have not properly installed or configured FFMPEG. 
Please install and configure FFMPEG to play music. 
Check the guides for your platform on how to setup ffmpeg correctly:
    Windows Guide: https://goo.gl/OjKk8F
    Linux Guide:  https://goo.gl/ShjCUo");
            }
            catch (OperationCanceledException)
            {
            }
            catch (InvalidOperationException)
            {
            }
            catch (Exception ex)
            {
                Log.Information(ex, "Error starting ffmpeg: {ErrorMessage}", ex.Message);
            }

            return null;
        }

        private Process StartFFmpegProcess()
        {
            var args = $"-err_detect ignore_err -i {_streamUrl} -f s16le -ar 48000 -vn -ac 2 pipe:1 -loglevel error";
            if (!_isLocal)
                args = $"-reconnect 1 -reconnect_streamed 1 -reconnect_delay_max 5 {args}";

            return _p = Process.Start(new ProcessStartInfo
            {
                FileName = @"C:\ffmpeg\nightly\bin\ffmpeg.exe",
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = false,
                CreateNoWindow = true,
            });
        }

        public int Read(byte[] output)
            => _p.StandardOutput.BaseStream.Read(output);

        public void Dispose()
        {
            try { _p?.Kill(); } catch { }

            try { _p?.Dispose(); } catch { }
        }
    }
}