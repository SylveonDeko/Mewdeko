using Mewdeko.Common.Configs;
using Mewdeko.Common.PubSub;
using Mewdeko.Modules.Games.Common;
using Mewdeko.Services.Settings;

namespace Mewdeko.Modules.Games.Services;

public sealed class GamesConfigService : ConfigServiceBase<GamesConfig>
{
    private new const string FilePath = "data/games.yml";
    private static readonly TypedKey<GamesConfig> ChangeKey = new("config.games.updated");

    public GamesConfigService(IConfigSeria serializer, IPubSub pubSub)
        : base(FilePath, serializer, pubSub, ChangeKey)
    {
        AddParsedProp("trivia.min_win_req", gs => gs.Trivia.MinimumWinReq, int.TryParse,
            ConfigPrinters.ToString, val => val > 0);
        AddParsedProp("trivia.currency_reward", gs => gs.Trivia.CurrencyReward, long.TryParse,
            ConfigPrinters.ToString, val => val >= 0);
    }

    public override string Name { get; } = "games";
}