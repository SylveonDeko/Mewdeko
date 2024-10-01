using Mewdeko.Modules.Games.Common.Trivia;

namespace Mewdeko.Services;

/// <summary>
///     Represents a cache for local data.
/// </summary>
public interface ILocalDataCache
{
    /// <summary>
    ///     Gets the array of trivia questions.
    /// </summary>
    TriviaQuestion[] TriviaQuestions { get; }
}