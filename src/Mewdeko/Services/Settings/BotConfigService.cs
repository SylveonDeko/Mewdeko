using Mewdeko.Common.Configs;
using Mewdeko.Common.PubSub;
using SixLabors.ImageSharp.PixelFormats;

namespace Mewdeko.Services.Settings;

/// <summary>
///     Settings service for bot-wide configuration.
/// </summary>
public sealed class BotConfigService : ConfigServiceBase<BotConfig>
{
    private const string FilePath = "data/bot.yml";
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
        AddParsedProp("administrationemote", bs => bs.AdministrationEmote, ConfigParsers.String, ConfigPrinters.ToString);
        AddParsedProp("afkemote", bs => bs.AfkEmote, ConfigParsers.String, ConfigPrinters.ToString);
        AddParsedProp("chattriggeremote", bs => bs.ChatTriggersEmote, ConfigParsers.String, ConfigPrinters.ToString);
        AddParsedProp("confessionsemote", bs => bs.ConfessionsEmote, ConfigParsers.String, ConfigPrinters.ToString);
        AddParsedProp("gamesemote", bs => bs.GamesEmote, ConfigParsers.String, ConfigPrinters.ToString);
        AddParsedProp("gamblingemote", bs => bs.GamblingEmote, ConfigParsers.String, ConfigPrinters.ToString);
        AddParsedProp("giveawaysemote", bs => bs.GiveawaysEmote, ConfigParsers.String, ConfigPrinters.ToString);
        AddParsedProp("helpemote", bs => bs.HelpEmote, ConfigParsers.String, ConfigPrinters.ToString);
        AddParsedProp("highlightsemote", bs => bs.HighlightsEmote, ConfigParsers.String, ConfigPrinters.ToString);
        AddParsedProp("multigreetsemote", bs => bs.MultiGreetsEmote, ConfigParsers.String, ConfigPrinters.ToString);
        AddParsedProp("musicemote", bs => bs.MusicEmote, ConfigParsers.String, ConfigPrinters.ToString);
        AddParsedProp("nsfwemote", bs => bs.NsfwEmote, ConfigParsers.String, ConfigPrinters.ToString);
        AddParsedProp("owneronlyemote", bs => bs.OwnerOnlyEmote, ConfigParsers.String, ConfigPrinters.ToString);
        AddParsedProp("permissionsemote", bs => bs.PermissionsEmote, ConfigParsers.String, ConfigPrinters.ToString);
        AddParsedProp("rolegreetsemote", bs => bs.RoleGreetsEmote, ConfigParsers.String, ConfigPrinters.ToString);
        AddParsedProp("searchesemote", bs => bs.SearchesEmote, ConfigParsers.String, ConfigPrinters.ToString);
        AddParsedProp("starboardemote", bs => bs.StarboardEmote, ConfigParsers.String, ConfigPrinters.ToString);
        AddParsedProp("servermanagementemote", bs => bs.ServerManagementEmote, ConfigParsers.String, ConfigPrinters.ToString);
        AddParsedProp("suggestionsemote", bs => bs.SuggestionsEmote, ConfigParsers.String, ConfigPrinters.ToString);
        AddParsedProp("userprofileemote", bs => bs.UserProfileEmote, ConfigParsers.String, ConfigPrinters.ToString);
        AddParsedProp("voteemote", bs => bs.VoteEmote, ConfigParsers.String, ConfigPrinters.ToString);
        AddParsedProp("xpemote", bs => bs.XpEmote, ConfigParsers.String, ConfigPrinters.ToString);


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