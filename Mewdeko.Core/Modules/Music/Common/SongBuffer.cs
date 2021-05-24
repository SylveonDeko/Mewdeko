using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Mewdeko.Core.Modules.Music.Common;
using NLog;

namespace Mewdeko.Modules.Music.Common
{
    public sealed class SongBuffer : IDisposable
    {
        private readonly PoopyBufferReborn _buffer;

        private readonly bool _isLocal;

        private readonly Logger _log;
        private readonly Stream _outStream;
        private readonly Process _p;

        public SongBuffer(string songUri, bool isLocal)
        {
            _log = LogManager.GetCurrentClassLogger();
            SongUri = songUri;
            _isLocal = isLocal;
            PrebufferingCompleted = new TaskCompletionSource<bool>();

            try
            {
                _p = StartFFmpegProcess(SongUri);
                _outStream = _p.StandardOutput.BaseStream;
                _buffer = new PoopyBufferReborn(_outStream);
            }
            catch (Win32Exception)
            {
                _log.Error(@"You have not properly installed or configured FFMPEG. 
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
            } // when ffmpeg is disposed
            catch (Exception ex)
            {
                _log.Info(ex);
            }
        }

        public string SongUri { get; }
        public TaskCompletionSource<bool> PrebufferingCompleted { get; }

        public void Dispose()
        {
            try
            {
                _p.StandardOutput.Dispose();
            }
            catch (Exception ex)
            {
                _log.Error(ex);
            }

            try
            {
                if (!_p.HasExited)
                    _p.Kill();
            }
            catch
            {
            }

            _buffer.Stop();
            _outStream.Dispose();
            _p.Dispose();
            _buffer.PrebufferingCompleted -= OnPrebufferingCompleted;
        }

        private Process StartFFmpegProcess(string songUri)
        {
            var args = $"-err_detect ignore_err -i {songUri} -f s16le -ar 48000 -vn -ac 2 pipe:1 -loglevel error";
            if (!_isLocal)
                args = "-reconnect 1 -reconnect_streamed 1 -reconnect_delay_max 5 " + args;

            return Process.Start(new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = false,
                CreateNoWindow = true
            });
        }

        public byte[] Read(int toRead)
        {
            return _buffer.Read(toRead).ToArray();
        }

        public void StartBuffering()
        {
            _buffer.StartBuffering();
            _buffer.PrebufferingCompleted += OnPrebufferingCompleted;
        }

        private void OnPrebufferingCompleted()
        {
            PrebufferingCompleted.SetResult(true);
        }
    }
}