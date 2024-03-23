namespace Mewdeko.Modules.Games.Common.Trivia
{
    /// <summary>
    /// Represents a pool of trivia questions.
    /// </summary>
    public class TriviaQuestionPool
    {
        private readonly IDataCache cache;

        private readonly MewdekoRandom rng = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="TriviaQuestionPool"/> class.
        /// </summary>
        /// <param name="cache">The data cache.</param>
        public TriviaQuestionPool(IDataCache cache) => this.cache = cache;

        private TriviaQuestion[] Pool => cache.LocalData.TriviaQuestions;

        /// <summary>
        /// Gets a random question from the pool, excluding those specified.
        /// </summary>
        /// <param name="exclude">The set of questions to exclude.</param>
        /// <returns>A random trivia question.</returns>
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
}