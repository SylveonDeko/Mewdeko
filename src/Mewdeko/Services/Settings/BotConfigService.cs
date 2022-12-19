using Mewdeko.Common.Configs;
using Mewdeko.Common.PubSub;
using SixLabors.ImageSharp.PixelFormats;

namespace Mewdeko.Services.Settings;

/// <summary>
///     Settings service for bot-wide configuration.
/// </summary>
public sealed class BotConfigService : ConfigServiceBase<BotConfig>
{
    private new const string FilePath = "data/bot.yml";
    private static readonly TypedKey<BotConfig> ChangeKey = new("config.bot.updated");

    public BotConfigService(IConfigSeria serializer, IPubSub pubSub)
        : base(FilePath, serializer, pubSub, ChangeKey)
    {
        AddParsedProp("color.ok", bs => bs.Color.Ok, Rgba32.TryParseHex, ConfigPrinters.Color);
        AddParsedProp("color.error", bs => bs.Color.Error, Rgba32.TryParseHex, ConfigPrinters.Color);
        AddParsedProp("color.pending", bs => bs.Color.Pending, Rgba32.TryParseHex, ConfigPrinters.Color);
        AddParsedProp("help.text", bs => bs.HelpText, ConfigParsers.String, ConfigPrinters.ToString);
        AddParsedProp("help.dmtext", bs => bs.DmHelpText, ConfigParsers.String, ConfigPrinters.ToString);
        AddParsedProp("console.type", bs => bs.ConsoleOutputType, Enum.TryParse, ConfigPrinters.ToString);
        AddParsedProp("locale", bs => bs.DefaultLocale, ConfigParsers.Culture, ConfigPrinters.Culture);
        AddParsedProp("prefix", bs => bs.Prefix, ConfigParsers.String, ConfigPrinters.ToString);
        AddParsedProp("commandlogchannel", bs => bs.CommandLogChannel, ulong.TryParse, ConfigPrinters.ToString);
        AddParsedProp("showinvitebutton", bs => bs.ShowInviteButton, bool.TryParse, ConfigPrinters.ToString);
        AddParsedProp("successemote", bs => bs.SuccessEmote, ConfigParsers.String, ConfigPrinters.ToString);
        AddParsedProp("loadingemote", bs => bs.LoadingEmote, ConfigParsers.String, ConfigPrinters.ToString);
        AddParsedProp("erroremote", bs => bs.ErrorEmote, ConfigParsers.String, ConfigPrinters.ToString);
        AddParsedProp("supportserver", bs => bs.SupportServer, ConfigParsers.String, ConfigPrinters.ToString);


        UpdateColors();
    }

    public override string Name { get; } = "bot";

    private void UpdateColors()
    {
        var ok = data.Color.Ok;
        var error = data.Color.Error;
        Mewdeko.OkColor = new Color(ok.R, ok.G, ok.B);
        Mewdeko.ErrorColor = new Color(error.R, error.G, error.B);
    }


    protected override void OnStateUpdate() => UpdateColors();
}