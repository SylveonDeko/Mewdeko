using NadekoBot.Core.Modules.Music.Common;
using NLog;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace NadekoBot.Modules.Music.Common
{
    public sealed class SongBuffer : IDisposable
    {
        private readonly Process _p;
        private readonly PoopyBufferReborn _buffer;
        private Stream _outStream;

        private readonly Logger _log;

        public string SongUri { get; private set; }
        public TaskCompletionSource<bool> PrebufferingCompleted { get; }

        public SongBuffer(string songUri, bool isLocal)
        {
            _log = LogManager.GetCurrentClassLogger();
            this.SongUri = songUri;
            this._isLocal = isLocal;
            this.PrebufferingCompleted = new TaskCompletionSource<bool>();

            try
            {
                this._p = StartFFmpegProcess(SongUri);
                this._outStream = this._p.StandardOutput.BaseStream;
                this._buffer = new PoopyBufferReborn(this._outStream);
            }
            catch (System.ComponentModel.Win32Exception)
            {
                _log.Error(@"You have not properly installed or configured FFMPEG. 
Please install and configure FFMPEG to play music. 
Check the guides for your platform on how to setup ffmpeg correctly:
    Windows Guide: https://goo.gl/OjKk8F
    Linux Guide:  https://goo.gl/ShjCUo");
            }
            catch (OperationCanceledException) { }
            catch (InvalidOperationException) { } // when ffmpeg is disposed
            catch (Exception ex)
            {
                _log.Info(ex);
            }
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
                CreateNoWindow = true,
            });
        }

        private readonly bool _isLocal;

        public byte[] Read(int toRead)
        {
            return this._buffer.Read(toRead).ToArray();
        }

        public void Dispose()
        {
            try
            {
                this._p.StandardOutput.Dispose();
            }
            catch (Exception ex)
            {
                _log.Error(ex);
            }
            try
            {
                if (!this._p.HasExited)
                    this._p.Kill();
            }
            catch
            {
            }
            _buffer.Stop();
            _outStream.Dispose();
            this._p.Dispose();
            this._buffer.PrebufferingCompleted -= OnPrebufferingCompleted;
        }

        public void StartBuffering()
        {
            this._buffer.StartBuffering();
            this._buffer.PrebufferingCompleted += OnPrebufferingCompleted;
        }

        private void OnPrebufferingCompleted()
        {
            PrebufferingCompleted.SetResult(true);
        }
    }
}