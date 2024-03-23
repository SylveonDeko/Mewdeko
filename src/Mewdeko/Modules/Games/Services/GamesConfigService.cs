using Mewdeko.Common.Configs;
using Mewdeko.Common.PubSub;
using Mewdeko.Modules.Games.Common;
using Mewdeko.Services.Settings;

namespace Mewdeko.Modules.Games.Services
{
    /// <summary>
    /// Service for managing configuration settings related to games.
    /// </summary>
    public sealed class GamesConfigService : ConfigServiceBase<GamesConfig>
    {
        /// <summary>
        /// Path to the file containing the games configuration.
        /// </summary>
        private new const string FilePath = "data/games.yml";

        /// <summary>
        /// Key used for pub-sub notifications of configuration changes.
        /// </summary>
        private static readonly TypedKey<GamesConfig> ChangeKey = new("config.games.updated");

        /// <summary>
        /// Initializes a new instance of the <see cref="GamesConfigService"/> class.
        /// </summary>
        /// <param name="serializer">The serializer used to serialize/deserialize the configuration.</param>
        /// <param name="pubSub">The pub-sub service used for notification of configuration changes.</param>
        public GamesConfigService(IConfigSeria serializer, IPubSub pubSub)
            : base(FilePath, serializer, pubSub, ChangeKey)
        {
            // Add configuration properties
            AddParsedProp("trivia.min_win_req", gs => gs.Trivia.MinimumWinReq, int.TryParse,
                ConfigPrinters.ToString, val => val > 0);
            AddParsedProp("trivia.currency_reward", gs => gs.Trivia.CurrencyReward, long.TryParse,
                ConfigPrinters.ToString, val => val >= 0);
        }

        /// <summary>
        /// Gets the name of the configuration service.
        /// </summary>
        public override string Name { get; } = "games";
    }
}