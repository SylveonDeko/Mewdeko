using System;

namespace Mewdeko.Common.Collections
{
    public sealed class PoopyRingBuffer : IDisposable
    {
        // readpos == writepos means empty
        // writepos == readpos - 1 means full 

        private byte[] _buffer;

        public PoopyRingBuffer(int capacity = 81920 * 100)
        {
            Capacity = capacity + 1;
            _buffer = new byte[Capacity];
        }

        public int Capacity { get; }

        private int ReadPos { get; set; }
        private int WritePos { get; set; }

        public int Length => ReadPos <= WritePos
            ? WritePos - ReadPos
            : Capacity - (ReadPos - WritePos);

        public int RemainingCapacity => Capacity - Length - 1;

        public void Dispose()
        {
            _buffer = null;
        }

        public int Read(byte[] b, int offset, int toRead)
        {
            if (WritePos == ReadPos)
                return 0;

            if (toRead > Length)
                toRead = Length;

            if (WritePos > ReadPos)
            {
                Array.Copy(_buffer, ReadPos, b, offset, toRead);
                ReadPos += toRead;
            }
            else
            {
                var toEnd = Capacity - ReadPos;
                var firstRead = toRead > toEnd ? toEnd : toRead;
                Array.Copy(_buffer, ReadPos, b, offset, firstRead);
                ReadPos += firstRead;
                var secondRead = toRead - firstRead;
                if (secondRead > 0)
                {
                    Array.Copy(_buffer, 0, b, offset + firstRead, secondRead);
                    ReadPos = secondRead;
                }
            }

            return toRead;
        }

        public bool Write(byte[] b, int offset, int toWrite)
        {
            while (toWrite > RemainingCapacity)
                return false;

            if (toWrite == 0)
                return true;

            if (WritePos < ReadPos)
            {
                Array.Copy(b, offset, _buffer, WritePos, toWrite);
                WritePos += toWrite;
            }
            else
            {
                var toEnd = Capacity - WritePos;
                var firstWrite = toWrite > toEnd ? toEnd : toWrite;
                Array.Copy(b, offset, _buffer, WritePos, firstWrite);
                var secondWrite = toWrite - firstWrite;
                if (secondWrite > 0)
                {
                    Array.Copy(b, offset + firstWrite, _buffer, 0, secondWrite);
                    WritePos = secondWrite;
                }
                else
                {
                    WritePos += firstWrite;
                    if (WritePos == Capacity)
                        WritePos = 0;
                }
            }

            return true;
        }
    }
}