namespace Mewdeko.Modules.Currency.Common;

/// <summary>
///     The side of a coin a player can call.
/// </summary>
public enum CoinSide
{
    /// <summary>Heads.</summary>
    Heads,

    /// <summary>Tails.</summary>
    Tails
}

/// <summary>
///     Whether the next number will be higher or lower.
/// </summary>
public enum HighLowGuess
{
    /// <summary>The next number will be higher.</summary>
    Higher,

    /// <summary>The next number will be lower.</summary>
    Lower
}

/// <summary>
///     A choice in rock paper scissors lizard spock.
/// </summary>
public enum RpsChoice
{
    /// <summary>Rock.</summary>
    Rock,

    /// <summary>Paper.</summary>
    Paper,

    /// <summary>Scissors.</summary>
    Scissors,

    /// <summary>Lizard.</summary>
    Lizard,

    /// <summary>Spock.</summary>
    Spock
}

/// <summary>
///     A craps bet type.
/// </summary>
public enum CrapsBet
{
    /// <summary>Pass line bet.</summary>
    Pass,

    /// <summary>Don't pass bet.</summary>
    DontPass,

    /// <summary>Field bet.</summary>
    Field,

    /// <summary>Any seven bet.</summary>
    Any
}

/// <summary>
///     A scratch card tier.
/// </summary>
public enum ScratchCardType
{
    /// <summary>Bronze card.</summary>
    Bronze,

    /// <summary>Silver card.</summary>
    Silver,

    /// <summary>Gold card.</summary>
    Gold,

    /// <summary>Diamond card.</summary>
    Diamond
}

/// <summary>
///     A baccarat bet type.
/// </summary>
public enum BaccaratBet
{
    /// <summary>Bet on the player hand.</summary>
    Player,

    /// <summary>Bet on the banker hand.</summary>
    Banker,

    /// <summary>Bet on a tie.</summary>
    Tie
}

/// <summary>
///     A minesweeper grid size.
/// </summary>
public enum MinesweeperSize
{
    /// <summary>Small grid.</summary>
    Small,

    /// <summary>Medium grid.</summary>
    Medium,

    /// <summary>Large grid.</summary>
    Large
}

/// <summary>
///     A bingo card size.
/// </summary>
public enum BingoCardType
{
    /// <summary>Small card.</summary>
    Small,

    /// <summary>Large card.</summary>
    Large
}

/// <summary>
///     A wheel of fortune configuration.
/// </summary>
public enum WheelType
{
    /// <summary>Classic wheel.</summary>
    Classic,

    /// <summary>Risky wheel.</summary>
    Risky,

    /// <summary>Balanced wheel.</summary>
    Balanced
}

/// <summary>
///     A memory game difficulty.
/// </summary>
public enum MemoryDifficulty
{
    /// <summary>Easy.</summary>
    Easy,

    /// <summary>Medium.</summary>
    Medium,

    /// <summary>Hard.</summary>
    Hard
}

/// <summary>
///     A trivia chain category.
/// </summary>
public enum TriviaCategory
{
    /// <summary>General knowledge.</summary>
    General,

    /// <summary>Science.</summary>
    Science,

    /// <summary>History.</summary>
    History,

    /// <summary>Sports.</summary>
    Sports,

    /// <summary>Entertainment.</summary>
    Entertainment
}