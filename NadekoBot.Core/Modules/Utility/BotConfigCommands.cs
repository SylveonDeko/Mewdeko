using Discord;
using Discord.Commands;
using NadekoBot.Common;
using NadekoBot.Common.Attributes;
using NadekoBot.Extensions;
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AngleSharp.Common;
using NadekoBot.Core.Services;
using NadekoBot.Modules.Administration.Services;

namespace NadekoBot.Modules.Utility
{
    public partial class Utility
    {
        public class BotConfigCommands : NadekoSubmodule
        {
            private readonly BotSettingsService _bss;
            private readonly SelfService _selfService;

            public BotConfigCommands(BotSettingsService bss, SelfService selfService)
            {
                _bss = bss;
                _selfService = selfService;
            }
            
            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public async Task BotConfigEdit()
            {
                var names = Enum.GetNames(typeof(BotConfigEditType))
                    .ToList();
                var valuesSb = new StringBuilder();
                foreach(var name in names)
                {
                    var value = Bc.GetValue(name);
                    if(name != "CurrencySign")
                        value = value.TrimTo(30);
                    valuesSb.AppendLine(value.Replace("\n", ""));
                }

                var propKeys = _bss.GetSettableProps();
                names.AddRange(propKeys);
                
                foreach(var key in propKeys)
                {
                    var value = _bss.GetSetting(key);
                    valuesSb.AppendLine(value?.TrimTo(30).Replace("\n", "") ?? "-");
                }
                
                var embed = new EmbedBuilder()
                    .WithTitle("Bot Config")
                    .WithOkColor()
                    .AddField(fb => fb.WithName("Names").WithValue(string.Join("\n", names)).WithIsInline(true))
                    .AddField(fb => fb.WithName("Values").WithValue(valuesSb.ToString()).WithIsInline(true));
                
                await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
            }

            [NadekoCommand, Usage, Description, Aliases]
            [Priority(1)]
            [OwnerOnly]
            public async Task BotConfigEdit(BotConfigEditType type, [Leftover]string newValue = null)
            {
                if (string.IsNullOrWhiteSpace(newValue))
                    newValue = null;

                var success = Bc.Edit(type, newValue);

                if (!success)
                    await ReplyErrorLocalizedAsync("bot_config_edit_fail", Format.Bold(type.ToString()), Format.Bold(newValue ?? "NULL")).ConfigureAwait(false);
                else
                    await ReplyConfirmLocalizedAsync("bot_config_edit_success", Format.Bold(type.ToString()), Format.Bold(newValue ?? "NULL")).ConfigureAwait(false);
            }
            
            [NadekoCommand, Usage, Description, Aliases]
            [Priority(0)]
            [OwnerOnly]
            public async Task BotConfigEdit(string key, [Leftover]string newValue = null)
            {
                key = key.ToLowerInvariant();
                var props = _bss.GetSettableProps();
                if (!props.Contains(key))
                {
                    await ReplyErrorLocalizedAsync("setting_not_found");
                    return;
                }
                
                if (string.IsNullOrWhiteSpace(newValue))
                {
                    var val = _bss.GetSetting(key);
                    val = string.IsNullOrWhiteSpace(val)
                        ? "-"
                        : val;
                    
                    var eb = new EmbedBuilder()
                        .WithTitle($"⚙️ {key}")
                        .WithDescription(Format.Sanitize(val))
                        .WithOkColor();
                    
                    await Context.Channel.EmbedAsync(eb);
                    // print the value
                    return;
                }

                var success = _bss.SetSetting(key, newValue);

                if (!success)
                    await ReplyErrorLocalizedAsync("bot_config_edit_fail", Format.Bold(key.ToString()), Format.Bold(newValue ?? "NULL")).ConfigureAwait(false);
                else
                    await ReplyConfirmLocalizedAsync("bot_config_edit_success", Format.Bold(key.ToString()), Format.Bold(newValue ?? "NULL")).ConfigureAwait(false);
            }

            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public async Task BotConfigReload()
            {
                _bss.Reload();
                _selfService.ReloadBotConfig();
                await ReplyConfirmLocalizedAsync("bot_config_reloaded").ConfigureAwait(false);
            }
        }
    }
}
