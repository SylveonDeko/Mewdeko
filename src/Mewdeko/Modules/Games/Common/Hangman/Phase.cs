namespace Mewdeko.Modules.Games.Common.Hangman;

/// <summary>
///     Represents the phase of a hangman game.
/// </summary>
public enum Phase
{
    /// <summary>
    ///     Represents the active phase of the game where guesses can be made.
    /// </summary>
    Active,

    /// <summary>
    ///     Represents the ended phase of the game where no more guesses can be made.
    /// </summary>
    Ended
}