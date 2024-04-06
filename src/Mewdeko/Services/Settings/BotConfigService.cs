using Mewdeko.Common.Configs;
using Mewdeko.Common.PubSub;
using SkiaSharp;

namespace Mewdeko.Services.Settings
{
    /// <summary>
    /// Settings service for bot-wide configuration.
    /// </summary>
    /// <remarks>
    /// This service handles bot-wide configuration settings, such as colors, text formatting, and other global parameters.
    /// </remarks>
    public sealed class BotConfigService : ConfigServiceBase<BotConfig>
    {
        /// <summary>
        /// The file path for the bot configuration file.
        /// </summary>
        private new const string FilePath = "data/bot.yml";

        /// <summary>
        /// The typed key used for pub/sub updates related to bot configuration changes.
        /// </summary>
        private static readonly TypedKey<BotConfig> ChangeKey = new("config.bot.updated");

        /// <summary>
        /// Initializes a new instance of the <see cref="BotConfigService"/> class.
        /// </summary>
        /// <param name="serializer">The serializer used for configuration data.</param>
        /// <param name="pubSub">The pub/sub service used for configuration updates.</param>
        /// <remarks>
        /// This constructor initializes the service with the specified serializer, pub/sub service, and change key.
        /// </remarks>
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
            AddParsedProp("chatgptinitprompt", bs => bs.ChatGptInitPrompt, ConfigParsers.String,
                ConfigPrinters.ToString);
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

        /// <summary>
        /// The name of the configuration service.
        /// </summary>
        /// <value>The name of the configuration service.</value>
        /// <remarks>
        /// This property represents the name of the configuration service, which is "bot".
        /// </remarks>
        public override string Name { get; } = "bot";

        /// <summary>
        /// Updates the colors used in the bot based on the configured color values.
        /// </summary>
        /// <remarks>
        /// This method updates the colors used throughout the bot based on the configured color values.
        /// </remarks>
        private void UpdateColors()
        {
            var ok = data.Color.Ok;
            var error = data.Color.Error;
            Mewdeko.OkColor = new Color(ok.Red, ok.Green, ok.Blue);
            Mewdeko.ErrorColor = new Color(error.Red, error.Green, error.Blue);
        }

        /// <summary>
        /// Handles state updates by invoking the color update logic.
        /// </summary>
        /// <remarks>
        /// This method is called whenever the state of the configuration service is updated, triggering color updates.
        /// </remarks>
        protected override void OnStateUpdate() => UpdateColors();
    }
}