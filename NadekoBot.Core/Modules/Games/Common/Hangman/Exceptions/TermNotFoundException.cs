using System;

namespace NadekoBot.Modules.Games.Common.Hangman.Exceptions
{
    public class TermNotFoundException : Exception
    {
        public TermNotFoundException() : base("Term of that type couldn't be found")
        {
        }

        public TermNotFoundException(string message) : base(message)
        {
        }

        public TermNotFoundException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
