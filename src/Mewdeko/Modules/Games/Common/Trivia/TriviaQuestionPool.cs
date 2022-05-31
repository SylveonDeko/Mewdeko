namespace Mewdeko.Modules.Games.Common.Trivia;

public class TriviaQuestionPool
{
    private readonly IDataCache _cache;

    private readonly MewdekoRandom _rng = new();

    public TriviaQuestionPool(IDataCache cache) => _cache = cache;

    private TriviaQuestion[] Pool => _cache.LocalData.TriviaQuestions;

    public TriviaQuestion GetRandomQuestion(HashSet<TriviaQuestion> exclude)
    {
        if (Pool.Length == 0)
            return null;

        TriviaQuestion randomQuestion;
        while (exclude.Contains(randomQuestion = Pool[_rng.Next(0, Pool.Length)]))
        {
        }

        return randomQuestion;
    }
}