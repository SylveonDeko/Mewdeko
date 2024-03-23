using Mewdeko.Common.Yml;

namespace Mewdeko.Modules.Games.Common
{
    /// <summary>
    /// Configuration settings for various games.
    /// </summary>
    public sealed class GamesConfig
    {
        /// <summary>
        /// Trivia related settings (.t command).
        /// </summary>
        [Comment("Trivia related settings (.t command)")]
        public TriviaConfig Trivia { get; set; } = new()
        {
            CurrencyReward = 0, MinimumWinReq = 1
        };

        /// <summary>
        /// List of responses for the .8ball command. A random one will be selected every time.
        /// </summary>
        [Comment("List of responses for the .8ball command. A random one will be selected every time")]
        public List<string> EightBallResponses { get; set; } = new()
        {
            "Most definitely yes.",
            "For sure.",
            "Totally!",
            "Of course!",
            // Add more responses here
            "Definitely no.",
            "NO - It may cause disease contraction!"
        };

        /// <summary>
        /// List of animals which will be used for the animal race game (.race).
        /// </summary>
        [Comment("List of animals which will be used for the animal race game (.race)")]
        public List<RaceAnimal> RaceAnimals { get; set; } = new()
        {
            new RaceAnimal
            {
                Icon = "🐼", Name = "Panda"
            },
            // Add more race animals here
            new RaceAnimal
            {
                Icon = "🦄", Name = "Unicorn"
            }
        };
    }

    /// <summary>
    /// Configuration settings for trivia games.
    /// </summary>
    public sealed class TriviaConfig
    {
        /// <summary>
        /// The amount of currency awarded to the winner of the trivia game.
        /// </summary>
        [Comment("The amount of currency awarded to the winner of the trivia game.")]
        public long CurrencyReward { get; set; }

        /// <summary>
        /// Users won't be able to start trivia games which have a smaller win requirement than the one specified by this setting.
        /// </summary>
        [Comment(
            @"Users won't be able to start trivia games which have a smaller win requirement than the one specified by this setting.")]
        public int MinimumWinReq { get; set; } = 1;
    }

    /// <summary>
    /// Represents a race animal with its icon and name.
    /// </summary>
    public sealed class RaceAnimal
    {
        /// <summary>
        /// The icon representing the race animal.
        /// </summary>
        public string Icon { get; set; }

        /// <summary>
        /// The name of the race animal.
        /// </summary>
        public string Name { get; set; }
    }
}