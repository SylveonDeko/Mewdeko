using NadekoBot.Core.Common.Pokemon;
using NadekoBot.Extensions;
using NadekoBot.Modules.Games.Common.Trivia;
using Newtonsoft.Json;
using NLog;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NadekoBot.Core.Services.Impl
{
    public class RedisLocalDataCache : ILocalDataCache
    {
        private readonly ConnectionMultiplexer _con;
        private readonly IBotCredentials _creds;
        private readonly Logger _log;

        private IDatabase _db => _con.GetDatabase();

        private const string pokemonAbilitiesFile = "data/pokemon/pokemon_abilities.json";
        private const string pokemonListFile = "data/pokemon/pokemon_list.json";
        private const string pokemonMapPath = "data/pokemon/name-id_map.json";
        private const string questionsFile = "data/trivia_questions.json";

        public IReadOnlyDictionary<string, SearchPokemon> Pokemons
        {
            get
            {
                return Get<Dictionary<string, SearchPokemon>>("pokemon_list");
            }
            private set
            {
                Set("pokemon_list", value);
            }
        }

        public IReadOnlyDictionary<string, SearchPokemonAbility> PokemonAbilities
        {
            get
            {
                return Get<Dictionary<string, SearchPokemonAbility>>("pokemon_abilities");
            }
            private set
            {
                Set("pokemon_abilities", value);
            }
        }

        public TriviaQuestion[] TriviaQuestions
        {
            get
            {
                return Get<TriviaQuestion[]>("trivia_questions");
            }
            private set
            {
                Set("trivia_questions", value);
            }
        }

        public IReadOnlyDictionary<int, string> PokemonMap
        {
            get
            {
                return Get<Dictionary<int, string>>("pokemon_map");
            }
            private set
            {
                Set("pokemon_map", value);
            }
        }

        public RedisLocalDataCache(ConnectionMultiplexer con, IBotCredentials creds, int shardId)
        {
            _con = con;
            _creds = creds;
            _log = LogManager.GetCurrentClassLogger();

            if (shardId == 0)
            {
                if (!File.Exists(pokemonListFile))
                {
                    _log.Warn(pokemonListFile + " is missing. Pokemon abilities not loaded.");
                }
                else
                {
                    Pokemons = JsonConvert.DeserializeObject<Dictionary<string, SearchPokemon>>(File.ReadAllText(pokemonListFile));
                }

                if (!File.Exists(pokemonAbilitiesFile))
                {
                    _log.Warn(pokemonAbilitiesFile + " is missing. Pokemon abilities not loaded.");
                }
                else
                {
                    PokemonAbilities = JsonConvert.DeserializeObject<Dictionary<string, SearchPokemonAbility>>(File.ReadAllText(pokemonAbilitiesFile));
                }

                try
                {
                    TriviaQuestions = JsonConvert.DeserializeObject<TriviaQuestion[]>(File.ReadAllText(questionsFile));
                    PokemonMap = JsonConvert.DeserializeObject<PokemonNameId[]>(File.ReadAllText(pokemonMapPath))
                            .ToDictionary(x => x.Id, x => x.Name);
                }
                catch (Exception ex)
                {
                    _log.Error(ex);
                    throw;
                }
            }
        }

        private T Get<T>(string key) where T : class
        {
            return JsonConvert.DeserializeObject<T>(_db.StringGet($"{_creds.RedisKey()}_localdata_{key}"));
        }

        private void Set(string key, object obj)
        {
            _db.StringSet($"{_creds.RedisKey()}_localdata_{key}", JsonConvert.SerializeObject(obj));
        }
    }
}
