using System;

namespace NadekoBot.Modules.Games.Common.Hangman
{
    [Flags]
    public enum TermTypes
    {
        Countries = 0,
        Movies = 1,
        Animals = 2,
        Things = 4,
        Random = 8,
    }
}
