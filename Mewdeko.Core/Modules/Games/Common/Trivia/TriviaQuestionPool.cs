using Mewdeko.Common;
using Mewdeko.Core.Services;
using Mewdeko.Extensions;
using System.Collections.Generic;

namespace Mewdeko.Modules.Games.Common.Trivia
{
    public class TriviaQuestionPool
    {
        private readonly IDataCache _cache;
        private readonly int maxPokemonId;

        private readonly MewdekoRandom _rng = new MewdekoRandom();

        private TriviaQuestion[] Pool => _cache.LocalData.TriviaQuestions;
        private IReadOnlyDictionary<int, string> Map => _cache.LocalData.PokemonMap;

        public TriviaQuestionPool(IDataCache cache)
        {
            _cache = cache;
            maxPokemonId = 721; //xd
        }

        public TriviaQuestion GetRandomQuestion(HashSet<TriviaQuestion> exclude, bool isPokemon)
        {
            if (Pool.Length == 0)
                return null;

            if (isPokemon)
            {
                var num = _rng.Next(1, maxPokemonId + 1);
                return new TriviaQuestion("Who's That Pokémon?",
                    Map[num].ToTitleCase(),
                    "Pokemon",
                    $@"https://Mewdeko.bot/images/pokemon/shadows/{num}.png",
                    $@"https://Mewdeko.bot/images/pokemon/real/{num}.png");
            }
            TriviaQuestion randomQuestion;
            while (exclude.Contains(randomQuestion = Pool[_rng.Next(0, Pool.Length)])) ;

            return randomQuestion;
        }
    }
}
