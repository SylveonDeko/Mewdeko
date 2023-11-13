using Mewdeko.Common.Configs;
using Mewdeko.Common.PubSub;
using SkiaSharp;

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
        AddParsedProp("color.ok", bs => bs.Color.Ok, SKColor.TryParse, ConfigPrinters.Color);
        AddParsedProp("color.error", bs => bs.Color.Error, SKColor.TryParse, ConfigPrinters.Color);
        AddParsedProp("color.pending", bs => bs.Color.Pending, SKColor.TryParse, ConfigPrinters.Color);
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
        AddParsedProp("youtubesupport", bs => bs.YoutubeSupport, bool.TryParse, ConfigPrinters.ToString);
        AddParsedProp("chatgptkey", bs => bs.ChatGptKey, ConfigParsers.String, ConfigPrinters.ToString);
        AddParsedProp("chatgptchannel", bs => bs.ChatGptChannel, ulong.TryParse, ConfigPrinters.ToString);
        AddParsedProp("chatgptinitprompt", bs => bs.ChatGptInitPrompt, ConfigParsers.String, ConfigPrinters.ToString);
        AddParsedProp("chatgptwebhook", bs => bs.ChatGptWebhook, ConfigParsers.String, ConfigPrinters.ToString);
        AddParsedProp("chatgptmodel", bs => bs.ChatGptModel, ConfigParsers.String, ConfigPrinters.ToString);
        AddParsedProp("chatgptmaxtokens", bs => bs.ChatGptMaxTokens, int.TryParse, ConfigPrinters.ToString);
        AddParsedProp("checkForUpdates", bs => bs.CheckForUpdates, Enum.TryParse, ConfigPrinters.ToString);
        AddParsedProp("forwardMessages", bs => bs.ForwardMessages, bool.TryParse, ConfigPrinters.ToString);
        AddParsedProp("forwardToAllOwners", bs => bs.ForwardToAllOwners, bool.TryParse, ConfigPrinters.ToString);
        AddParsedProp("UpdateCheckType", bs => bs.CheckForUpdates, Enum.TryParse, ConfigPrinters.ToString);
        AddParsedProp("UpdateBranch", bs => bs.UpdateBranch, ConfigParsers.String, ConfigPrinters.ToString);
        AddParsedProp("CheckUpdateInterval", bs => bs.CheckUpdateInterval, int.TryParse, ConfigPrinters.ToString);
        AddParsedProp("QuarantineNotification", bs => bs.QuarantineNotification, bool.TryParse,
            ConfigPrinters.ToString);

        UpdateColors();
    }

    public override string Name { get; } = "bot";

    private void UpdateColors()
    {
        var ok = data.Color.Ok;
        var error = data.Color.Error;
        Mewdeko.OkColor = new Color(ok.Red, ok.Green, ok.Blue);
        Mewdeko.ErrorColor = new Color(error.Red, error.Green, error.Blue);
    }


    protected override void OnStateUpdate() => UpdateColors();
}