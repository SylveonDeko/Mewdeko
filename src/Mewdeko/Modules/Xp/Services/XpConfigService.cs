using Mewdeko.Common.Configs;
using Mewdeko.Common.PubSub;
using Mewdeko.Modules.Xp.Common;
using Mewdeko.Services.Settings;

namespace Mewdeko.Modules.Xp.Services;

public sealed class XpConfigService : ConfigServiceBase<XpConfig>
{
    private new const string FilePath = "data/xp.yml";
    private static readonly TypedKey<XpConfig> ChangeKey = new("config.xp.updated");

    public XpConfigService(IConfigSeria serializer, IPubSub pubSub) : base(FilePath, serializer, pubSub,
        ChangeKey)
    {
        AddParsedProp("txt.cooldown", conf => conf.MessageXpCooldown, int.TryParse,
            ConfigPrinters.ToString, x => x > 0);
        AddParsedProp("txt.per_msg", conf => conf.XpPerMessage, int.TryParse,
            ConfigPrinters.ToString, x => x >= 0);
        AddParsedProp("voice.per_minute", conf => conf.VoiceXpPerMinute, double.TryParse,
            ConfigPrinters.ToString, x => x >= 0);
        AddParsedProp("voice.max_minutes", conf => conf.VoiceMaxMinutes, int.TryParse,
            ConfigPrinters.ToString, x => x > 0);
    }

    public override string Name { get; } = "xp";
}