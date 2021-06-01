using Mewdeko.Core.Common;
using Mewdeko.Core.Common.Configs;
using Mewdeko.Core.Services;

namespace Mewdeko.Modules.Xp.Services
{
    public sealed class XpConfigService : ConfigServiceBase<XpConfig>
    {
        public override string Name { get; } = "xp";
        private const string FilePath = "data/xp.yml";
        private static TypedKey<XpConfig> changeKey = new TypedKey<XpConfig>("config.xp.updated");

        public XpConfigService(IConfigSeria serializer, IPubSub pubSub) : base(FilePath, serializer, pubSub,
            changeKey)
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
    }
}