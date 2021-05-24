using System;
using Discord;
using Mewdeko.Core.Common;
using Mewdeko.Core.Common.Configs;
using SixLabors.ImageSharp.PixelFormats;

namespace Mewdeko.Core.Services
{
    /// <summary>
    ///     Settings service for bot-wide configuration.
    /// </summary>
    public sealed class BotSettingsService : SettingsServiceBase<BotSettings>
    {
        private const string FilePath = "data/bot.yml";
        private static readonly TypedKey<BotSettings> changeKey = new("config.bot.updated");

        public BotSettingsService(ISettingsSeria serializer, IPubSub pubSub)
            : base(FilePath, serializer, pubSub, changeKey)
        {
            AddParsedProp("color.ok", bs => bs.Color.Ok, Rgba32.TryParseHex, SettingPrinters.Color);
            AddParsedProp("color.error", bs => bs.Color.Error, Rgba32.TryParseHex, SettingPrinters.Color);
            AddParsedProp("color.pending", bs => bs.Color.Pending, Rgba32.TryParseHex, SettingPrinters.Color);
            AddParsedProp("help.text", bs => bs.HelpText, SettingParsers.String, SettingPrinters.ToString);
            AddParsedProp("help.dmtext", bs => bs.DmHelpText, SettingParsers.String, SettingPrinters.ToString);
            AddParsedProp("console.type", bs => bs.ConsoleOutputType, Enum.TryParse, SettingPrinters.ToString);
            AddParsedProp("locale", bs => bs.DefaultLocale, SettingParsers.Culture, SettingPrinters.Culture);
            AddParsedProp("prefix", bs => bs.Prefix, SettingParsers.String, SettingPrinters.ToString);

            UpdateColors();
        }

        private void UpdateColors()
        {
            var ok = _data.Color.Ok;
            var error = _data.Color.Error;
            // todo remove these static props once cleanup is done
            Mewdeko.OkColor = new Color(ok.R, ok.G, ok.B);
            Mewdeko.ErrorColor = new Color(error.R, error.G, error.B);
        }

        protected override void OnStateUpdate()
        {
            UpdateColors();
        }
    }
}