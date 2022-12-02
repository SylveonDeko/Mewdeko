namespace Mewdeko.Modules.Games.Common.Trivia;

public class TriviaQuestionPool
{
    private readonly IDataCache cache;

    private readonly MewdekoRandom rng = new();

    public TriviaQuestionPool(IDataCache cache) => this.cache = cache;

    private TriviaQuestion[] Pool => cache.LocalData.TriviaQuestions;

    public TriviaQuestion? GetRandomQuestion(HashSet<TriviaQuestion> exclude)
    {
        if (Pool.Length == 0)
            return null;

        TriviaQuestion randomQuestion;
        while (exclude.Contains(randomQuestion = Pool[rng.Next(0, Pool.Length)]))
        {
        }

        return randomQuestion;
    }
}