using System.IO;
using Mewdeko.Modules.Games.Common.Hangman.Exceptions;
using Newtonsoft.Json;
using Serilog;

namespace Mewdeko.Modules.Games.Common.Hangman
{
    /// <summary>
    /// Represents a pool of terms for the Hangman game.
    /// </summary>
    public class TermPool
    {
        private const string TermsPath = "data/hangman.json";

        /// <summary>
        /// Initializes a new instance of the <see cref="TermPool"/> class.
        /// </summary>
        public TermPool()
        {
            try
            {
                // Deserialize terms from JSON file
                Data = JsonConvert.DeserializeObject<Dictionary<string, HangmanObject[]>>(File.ReadAllText(TermsPath));
                Data = Data.ToDictionary(
                    x => x.Key.ToLowerInvariant(),
                    x => x.Value);
            }
            catch (Exception ex)
            {
                // Log any errors during initialization
                Log.Warning(ex, "Error loading Hangman Term pool");
            }
        }

        /// <summary>
        /// Gets the dictionary containing the term data.
        /// </summary>
        public IReadOnlyDictionary<string, HangmanObject[]> Data { get; } = new Dictionary<string, HangmanObject[]>();

        /// <summary>
        /// Retrieves a HangmanObject for the specified type.
        /// </summary>
        /// <param name="type">The type of term to retrieve.</param>
        /// <returns>A HangmanObject representing the term.</returns>
        /// <exception cref="TermNotFoundException">Thrown when the requested term type is not found in the term pool.</exception>
        public HangmanObject GetTerm(string? type)
        {
            type = type?.Trim().ToLowerInvariant();
            var rng = new MewdekoRandom();

            if (type == "random")
                type = Data.Keys.ToArray()[rng.Next(0, Data.Keys.Count())];

            if (!Data.TryGetValue(type, out var termTypes) || termTypes.Length == 0)
                throw new TermNotFoundException();

            var obj = termTypes[rng.Next(0, termTypes.Length)];

            obj.Word = obj.Word.Trim().ToLowerInvariant();
            return obj;
        }
    }
}