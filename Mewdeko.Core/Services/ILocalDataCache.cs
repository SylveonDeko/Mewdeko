using Mewdeko.Core.Common.Pokemon;
using Mewdeko.Modules.Games.Common.Trivia;
using System.Collections.Generic;

namespace Mewdeko.Core.Services
{
    public interface ILocalDataCache
    {
        IReadOnlyDictionary<string, SearchPokemon> Pokemons { get; }
        IReadOnlyDictionary<string, SearchPokemonAbility> PokemonAbilities { get; }
        IReadOnlyDictionary<int, string> PokemonMap { get; }
        TriviaQuestion[] TriviaQuestions { get; }
    }
}
