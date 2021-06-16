using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Mewdeko.Common.Attributes;
using Mewdeko.Core.Services;
using Mewdeko.Extensions;
using Mewdeko.Modules.Administration.Services;

namespace Mewdeko.Modules.Utility
{
    public partial class Utility
    {
        public class ConfigCommands : MewdekoSubmodule
        {
            private readonly BotConfigService _bss;
            private readonly SelfService _selfService;

            private readonly IEnumerable<IConfigService> _settingServices;

            public ConfigCommands(BotConfigService bss, SelfService selfService,
                IEnumerable<IConfigService> settingServices)
            {
                _settingServices = settingServices;
                _bss = bss;
                _selfService = selfService;
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [OwnerOnly]
            public Task BotConfigEdit()
            {
                return Config("bot");
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [Priority(0)]
            [OwnerOnly]
            public Task BotConfigEdit(string prop, [Leftover] string newValue = null)
            {
                return Config("bot", prop, newValue);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [OwnerOnly]
            public async Task ConfigReload(string name)
            {
                var setting = _settingServices.FirstOrDefault(x =>
                    x.Name.StartsWith(name, StringComparison.InvariantCultureIgnoreCase));

                if (setting is null)
                {
                    var configNames = _settingServices.Select(x => x.Name);
                    var embed = new EmbedBuilder()
                        .WithErrorColor()
                        .WithDescription(GetText("config_not_found", Format.Code(name)))
                        .AddField(GetText("config_list"), string.Join("\n", configNames));

                    await ctx.Channel.EmbedAsync(embed);
                    return;
                }

                setting.Reload();
                await ctx.OkAsync();
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [OwnerOnly]
            public async Task Config(string name = null, string prop = null, [Leftover] string value = null)
            {
                var configNames = _settingServices.Select(x => x.Name);

                // if name is not provided, print available configs
                name = name?.ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(name))
                {
                    var embed = new EmbedBuilder()
                        .WithOkColor()
                        .WithTitle(GetText("config_list"))
                        .WithDescription(string.Join("\n", configNames));

                    await ctx.Channel.EmbedAsync(embed);
                    return;
                }

                var setting = _settingServices.FirstOrDefault(x =>
                    x.Name.StartsWith(name, StringComparison.InvariantCultureIgnoreCase));

                // if config name is not found, print error and the list of configs
                if (setting is null)
                {
                    var embed = new EmbedBuilder()
                        .WithErrorColor()
                        .WithDescription(GetText("config_not_found", Format.Code(name)))
                        .AddField(GetText("config_list"), string.Join("\n", configNames));

                    await ctx.Channel.EmbedAsync(embed);
                    return;
                }

                name = setting.Name;

                // if prop is not sent, then print the list of all props and values in that config
                prop = prop?.ToLowerInvariant();
                var propNames = setting.GetSettableProps();
                if (string.IsNullOrWhiteSpace(prop))
                {
                    var propStrings = GetPropsAndValuesString(setting, propNames);
                    var embed = new EmbedBuilder()
                        .WithOkColor()
                        .WithTitle($"⚙️ {setting.Name}")
                        .WithDescription(propStrings);


                    await ctx.Channel.EmbedAsync(embed);
                    return;
                }
                // if the prop is invalid -> print error and list of 

                var exists = propNames.Any(x => x == prop);

                if (!exists)
                {
                    var propStrings = GetPropsAndValuesString(setting, propNames);
                    var propErrorEmbed = new EmbedBuilder()
                        .WithErrorColor()
                        .WithDescription(GetText("config_prop_not_found", Format.Code(prop), Format.Code(name)))
                        .AddField($"⚙️ {setting.Name}", propStrings);

                    await ctx.Channel.EmbedAsync(propErrorEmbed);
                    return;
                }

                // if prop is sent, but value is not, then we have to check
                // if prop is valid -> 
                if (string.IsNullOrWhiteSpace(value))
                {
                    value = setting.GetSetting(prop);
                    if (prop != "currency.sign") Format.Code(Format.Sanitize(value?.TrimTo(1000)), "json");

                    if (string.IsNullOrWhiteSpace(value))
                        value = "-";

                    var embed = new EmbedBuilder()
                        .WithOkColor()
                        .AddField("Config", Format.Code(setting.Name), true)
                        .AddField("Prop", Format.Code(prop), true)
                        .AddField("Value", value);

                    var comment = setting.GetComment(prop);
                    if (!string.IsNullOrWhiteSpace(comment))
                        embed.AddField("Comment", comment);

                    await ctx.Channel.EmbedAsync(embed);
                    return;
                }

                var success = setting.SetSetting(prop, value);

                if (!success)
                {
                    await ReplyErrorLocalizedAsync("config_edit_fail", Format.Code(prop), Format.Code(value));
                    return;
                }

                await ctx.OkAsync();
            }

            private string GetPropsAndValuesString(IConfigService config, IEnumerable<string> names)
            {
                var propValues = names.Select(pr =>
                {
                    var val = config.GetSetting(pr);
                    if (pr != "currency.sign")
                        val = val?.TrimTo(28);
                    return val?.Replace("\n", "") ?? "-";
                });

                var strings = names.Zip(propValues, (name, value) =>
                    $"{name,-25} = {value}\n");

                return Format.Code(string.Concat(strings), "hs");
            }
        }
    }
}