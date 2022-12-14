using Mewdeko.Modules.Games.Common.Trivia;

namespace Mewdeko.Services;

public interface ILocalDataCache
{
    TriviaQuestion[] TriviaQuestions { get; }
}