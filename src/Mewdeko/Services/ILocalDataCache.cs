using System.Collections.Generic;
using Mewdeko.Common.Pokemon;
using Mewdeko.Modules.Games.Common.Trivia;

namespace Mewdeko.Services
{
    public interface ILocalDataCache
    {
        IReadOnlyDictionary<string, SearchPokemon> Pokemons { get; }
        IReadOnlyDictionary<string, SearchPokemonAbility> PokemonAbilities { get; }
        IReadOnlyDictionary<int, string> PokemonMap { get; }
        TriviaQuestion[] TriviaQuestions { get; }
    }
}