using NadekoBot.Extensions;
using System;
using System.IO;
using System.Threading.Tasks;

namespace NadekoBot.Core.Modules.Music.Common
{
    public class PoopyBufferReborn
    {
        private readonly Stream _inStream;
        public event Action PrebufferingCompleted;
        private bool prebuffered = false;

        private readonly byte[] _buffer;

        public int ReadPosition { get; private set; }
        public int WritePosition { get; private set; }

        public int ContentLength => WritePosition >= ReadPosition
            ? WritePosition - ReadPosition
            : (_buffer.Length - ReadPosition) + WritePosition;

        public int FreeSpace => _buffer.Length - ContentLength;

        public bool Stopped { get; private set; } = false;

        public PoopyBufferReborn(Stream inStream, int bufferSize = 0)
        {
            if (bufferSize == 0)
                bufferSize = 10.MiB();
            _inStream = inStream;
            _buffer = new byte[bufferSize];

            ReadPosition = 0;
            WritePosition = 0;

            _inStream = inStream;
        }

        public void Stop() => Stopped = true;

        public void StartBuffering()
        {
            Task.Run(async () =>
            {
                var output = new byte[38400];
                int read = 0;
                while (!Stopped && (read = await _inStream.ReadAsync(output, 0, 38400).ConfigureAwait(false)) > 0)
                {
                    while (_buffer.Length - ContentLength <= read)
                    {
                        if(!prebuffered)
                        {
                            prebuffered = true;
                            PrebufferingCompleted();
                        }
                        await Task.Delay(100).ConfigureAwait(false);
                    }

                    Write(output, read);
                }
            });
        }

        private void Write(byte[] input, int writeCount)
        {
            if (WritePosition + writeCount < _buffer.Length)
            {
                Buffer.BlockCopy(input, 0, _buffer, WritePosition, writeCount);
                WritePosition += writeCount;
                return;
            }

            var wroteNormally = _buffer.Length - WritePosition;
            Buffer.BlockCopy(input, 0, _buffer, WritePosition, wroteNormally);
            var wroteFromStart = writeCount - wroteNormally;
            Buffer.BlockCopy(input, wroteNormally, _buffer, 0, wroteFromStart);
            WritePosition = wroteFromStart;
        }

        public ReadOnlySpan<byte> Read(int count)
        {
            var toRead = Math.Min(ContentLength, count);
            var wp = WritePosition;

            if (ContentLength == 0)
                return ReadOnlySpan<byte>.Empty;

            if (wp > ReadPosition || ReadPosition + toRead <= _buffer.Length)
            {
                var toReturn = ((Span<byte>)_buffer).Slice(ReadPosition, toRead);
                ReadPosition += toRead;
                return toReturn;
            }
            else
            {
                var toReturn = new byte[toRead];
                var toEnd = _buffer.Length - ReadPosition;
                Buffer.BlockCopy(_buffer, ReadPosition, toReturn, 0, toEnd);

                var fromStart = toRead - toEnd;
                Buffer.BlockCopy(_buffer, 0, toReturn, toEnd, fromStart);
                ReadPosition = fromStart;
                return toReturn;
            }
        }
    }
}