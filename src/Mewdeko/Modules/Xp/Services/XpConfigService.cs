using Mewdeko.Common.Configs;
using Mewdeko.Common.PubSub;
using Mewdeko.Modules.Xp.Common;
using Mewdeko.Services.Settings;

namespace Mewdeko.Modules.Xp.Services;

/// <summary>
///     Provides services for managing XP configuration settings.
/// </summary>
public sealed class XpConfigService : ConfigServiceBase<XpConfig>
{
    private new const string FilePath = "data/xp.yml";
    private static readonly TypedKey<XpConfig> ChangeKey = new("config.xp.updated");

    /// <summary>
    ///     Initializes a new instance of the <see cref="XpConfigService" /> class.
    /// </summary>
    /// <param name="serializer">The configuration serializer.</param>
    /// <param name="pubSub">The pub/sub system for configuration changes.</param>
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


    /// <summary>
    ///     Gets the name of the configuration.
    /// </summary>
    public override string Name { get; } = "xp";
}