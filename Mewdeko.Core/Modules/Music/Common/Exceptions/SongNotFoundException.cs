using System;

namespace Mewdeko.Modules.Music.Common.Exceptions
{
    public class SongNotFoundException : Exception
    {
        public SongNotFoundException(string message) : base(message)
        {
        }

        public SongNotFoundException() : base("Song != found.")
        {
        }

        public SongNotFoundException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}