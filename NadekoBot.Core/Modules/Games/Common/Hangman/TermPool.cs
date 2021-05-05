using NadekoBot.Common;
using NadekoBot.Modules.Games.Common.Hangman.Exceptions;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NLog;

namespace NadekoBot.Modules.Games.Common.Hangman
{
    public class TermPool
    {
        const string termsPath = "data/hangman.json";
        private readonly Logger _log;

        public IReadOnlyDictionary<string, HangmanObject[]> Data { get; } = new Dictionary<string, HangmanObject[]>();
        public TermPool()
        {
            _log = LogManager.GetCurrentClassLogger();
            try
            {
                Data = JsonConvert.DeserializeObject<Dictionary<string, HangmanObject[]>>(File.ReadAllText(termsPath));
                Data = Data.ToDictionary(
                    x => x.Key.ToLowerInvariant(),
                    x => x.Value);
            }
            catch (Exception ex)
            {
                _log.Warn(ex);
            }
        }

        public HangmanObject GetTerm(string type)
        {
            type = type?.Trim().ToLowerInvariant();
            var rng = new NadekoRandom();

            if (type == "random")
            {
                type = Data.Keys.ToArray()[rng.Next(0, Data.Keys.Count())];
            }
            if (!Data.TryGetValue(type, out var termTypes) || termTypes.Length == 0)
                throw new TermNotFoundException();

            var obj = termTypes[rng.Next(0, termTypes.Length)];

            obj.Word = obj.Word.Trim().ToLowerInvariant();
            return obj;
        }
    }
}
