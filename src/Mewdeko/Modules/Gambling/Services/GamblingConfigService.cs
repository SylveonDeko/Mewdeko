using Mewdeko.Common.Configs;
using Mewdeko.Common.PubSub;
using Mewdeko.Modules.Gambling.Common;
using Mewdeko.Services.Settings;

namespace Mewdeko.Modules.Gambling.Services;

public sealed class GamblingConfigService : ConfigServiceBase<GamblingConfig>
{
    private new const string FilePath = "data/gambling.yml";
    private static readonly TypedKey<GamblingConfig> ChangeKey = new("config.gambling.updated");

    public GamblingConfigService(IConfigSeria serializer, IPubSub pubSub)
        : base(FilePath, serializer, pubSub, ChangeKey)
    {
        AddParsedProp("currency.name", gs => gs.Currency.Name, ConfigParsers.String, ConfigPrinters.ToString);
        AddParsedProp("currency.sign", gs => gs.Currency.Sign, ConfigParsers.String, ConfigPrinters.ToString);

        AddParsedProp("minbet", gs => gs.MinBet, int.TryParse, ConfigPrinters.ToString, val => val >= 0);
        AddParsedProp("maxbet", gs => gs.MaxBet, int.TryParse, ConfigPrinters.ToString, val => val >= 0);

        AddParsedProp("gen.min", gs => gs.Generation.MinAmount, int.TryParse, ConfigPrinters.ToString,
            val => val >= 1);
        AddParsedProp("gen.max", gs => gs.Generation.MaxAmount, int.TryParse, ConfigPrinters.ToString,
            val => val >= 1);
        AddParsedProp("gen.cd", gs => gs.Generation.GenCooldown, int.TryParse, ConfigPrinters.ToString,
            val => val > 0);
        AddParsedProp("gen.chance", gs => gs.Generation.Chance, decimal.TryParse, ConfigPrinters.ToString,
            val => val is >= 0 and <= 1);
        AddParsedProp("gen.has_pw", gs => gs.Generation.HasPassword, bool.TryParse, ConfigPrinters.ToString);
        AddParsedProp("bf.multi", gs => gs.BetFlip.Multiplier, decimal.TryParse, ConfigPrinters.ToString,
            val => val >= 1);
        AddParsedProp("waifu.min_price", gs => gs.Waifu.MinPrice, int.TryParse, ConfigPrinters.ToString,
            val => val >= 0);
        AddParsedProp("waifu.multi.reset", gs => gs.Waifu.Multipliers.WaifuReset, int.TryParse,
            ConfigPrinters.ToString, val => val >= 0);
        AddParsedProp("waifu.multi.crush_claim", gs => gs.Waifu.Multipliers.CrushClaim, decimal.TryParse,
            ConfigPrinters.ToString, val => val >= 0);
        AddParsedProp("waifu.multi.normal_claim", gs => gs.Waifu.Multipliers.NormalClaim, decimal.TryParse,
            ConfigPrinters.ToString, val => val > 0);
        AddParsedProp("waifu.multi.divorce_value", gs => gs.Waifu.Multipliers.DivorceNewValue, decimal.TryParse,
            ConfigPrinters.ToString, val => val > 0);
        AddParsedProp("waifu.multi.all_gifts", gs => gs.Waifu.Multipliers.AllGiftPrices, decimal.TryParse,
            ConfigPrinters.ToString, val => val > 0);
        AddParsedProp("waifu.multi.gift_effect", gs => gs.Waifu.Multipliers.GiftEffect, decimal.TryParse,
            ConfigPrinters.ToString, val => val >= 0);
        AddParsedProp("decay.percent", gs => gs.Decay.Percent, decimal.TryParse, ConfigPrinters.ToString,
            val => val is >= 0 and <= 1);
        AddParsedProp("decay.maxdecay", gs => gs.Decay.MaxDecay, int.TryParse, ConfigPrinters.ToString,
            val => val >= 0);
        AddParsedProp("decay.threshold", gs => gs.Decay.MinThreshold, int.TryParse, ConfigPrinters.ToString,
            val => val >= 0);
    }

    public override string Name { get; } = "gambling";
}