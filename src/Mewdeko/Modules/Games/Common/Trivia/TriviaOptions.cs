using CommandLine;

namespace Mewdeko.Modules.Games.Common.Trivia;

/// <summary>
///     Represents options for configuring a trivia game.
/// </summary>
public class TriviaOptions : IMewdekoCommandOptions
{
    /// <summary>
    ///     Gets or sets a value indicating whether the trivia game is based on Pokémon.
    /// </summary>
    [Option('p', "pokemon", Required = false, Default = false,
        HelpText = "Whether it's 'Who's that pokemon?' trivia.")]
    public bool IsPokemon { get; set; } = false;

    /// <summary>
    ///     Gets or sets a value indicating whether hints are disabled in the trivia game.
    /// </summary>
    [Option("nohint", Required = false, Default = false, HelpText = "Don't show any hints.")]
    public bool NoHint { get; set; } = false;

    /// <summary>
    ///     Gets or sets the winning requirement for the trivia game.
    /// </summary>
    [Option('w', "win-req", Required = false, Default = 10,
        HelpText = "Winning requirement. Set 0 for an infinite game. Default 10.")]
    public int WinRequirement { get; set; } = 10;

    /// <summary>
    ///     Gets or sets the duration of a trivia question before it ends.
    /// </summary>
    [Option('q', "question-timer", Required = false, Default = 30,
        HelpText = "How long until the question ends. Default 30.")]
    public int QuestionTimer { get; set; } = 30;

    /// <summary>
    ///     Gets or sets the number of inactive questions before the game automatically stops.
    /// </summary>
    [Option('t', "timeout", Required = false, Default = 10,
        HelpText = "Number of questions of inactivity in order stop. Set 0 for never. Default 10.")]
    public int Timeout { get; set; } = 10;

    /// <summary>
    ///     Normalizes the trivia game options.
    /// </summary>
    public void NormalizeOptions()
    {
        if (WinRequirement < 0)
            WinRequirement = 10;
        if (QuestionTimer is < 10 or > 300)
            QuestionTimer = 30;
        if (Timeout is < 0 or > 20)
            Timeout = 10;
    }
}