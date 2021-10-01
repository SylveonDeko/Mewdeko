using System.Collections.Generic;
using Mewdeko._Extensions;
using Mewdeko.Common;
using Mewdeko.Services;

namespace Mewdeko.Modules.Games.Common.Trivia
{
    public class TriviaQuestionPool
    {
        private readonly IDataCache _cache;

        private readonly MewdekoRandom _rng = new();
        private readonly int maxPokemonId;

        public TriviaQuestionPool(IDataCache cache)
        {
            _cache = cache;
            maxPokemonId = 721; //xd
        }

        private TriviaQuestion[] Pool => _cache.LocalData.TriviaQuestions;
        private IReadOnlyDictionary<int, string> Map => _cache.LocalData.PokemonMap;

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