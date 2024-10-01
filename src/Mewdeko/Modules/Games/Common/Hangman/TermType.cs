namespace Mewdeko.Modules.Games.Common.Hangman;

/// <summary>
///     Represents the types of terms available for the Hangman game.
/// </summary>
[Flags]
public enum TermTypes
{
    /// <summary>
    ///     Indicates countries as a type of term.
    /// </summary>
    Countries = 0,

    /// <summary>
    ///     Indicates movies as a type of term.
    /// </summary>
    Movies = 1,

    /// <summary>
    ///     Indicates animals as a type of term.
    /// </summary>
    Animals = 2,

    /// <summary>
    ///     Indicates things as a type of term.
    /// </summary>
    Things = 4,

    /// <summary>
    ///     Indicates that a random type of term will be selected.
    /// </summary>
    Random = 8
}