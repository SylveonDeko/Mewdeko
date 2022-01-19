using Mewdeko.Common.Configs;
using Mewdeko.Common.PubSub;
using Mewdeko.Modules.Games.Common;
using Mewdeko.Services.Settings;

namespace Mewdeko.Modules.Games.Services;

public sealed class GamesConfigService : ConfigServiceBase<GamesConfig>
{
    private const string FILE_PATH = "data/games.yml";
    private static readonly TypedKey<GamesConfig> _changeKey = new("config.games.updated");

    public GamesConfigService(IConfigSeria serializer, IPubSub pubSub)
        : base(FILE_PATH, serializer, pubSub, _changeKey)
    {
        AddParsedProp("trivia.min_win_req", gs => gs.Trivia.MinimumWinReq, int.TryParse,
            ConfigPrinters.ToString, val => val > 0);
        AddParsedProp("trivia.currency_reward", gs => gs.Trivia.CurrencyReward, long.TryParse,
            ConfigPrinters.ToString, val => val >= 0);
    }

    public override string Name { get; } = "games";
}