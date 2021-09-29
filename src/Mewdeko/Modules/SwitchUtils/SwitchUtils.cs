using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Mewdeko.Common.Attributes;
using Mewdeko.Extensions;
using Mewdeko.Modules.SwitchUtils.Services;

namespace Mewdeko.Modules.SwitchUtils
{
    public class SwitchUtils : MewdekoModule<SwitchUtilsService>
    {
        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        public async Task serr(string err)
        {
            EmbedBuilder embed;
            string err_module;
            int desc;
            int module;
            int errcode;
            var switch_re = new Regex(@"2\d{3}\-\d{4}");
            var yesn = switch_re.Match(err);
            if (yesn.Success || err.StartsWith("0x"))
            {
                char[] ae = { };
                // Switch
                if (err.StartsWith("0x"))
                {
                    var ed = err.ToCharArray();
                    ae = ed[..2];
                    errcode = Convert.ToInt32(err, 16);
                    module = errcode & 0x1FF;
                    desc = (errcode >> 9) & 0x3FFF;
                }
                else
                {
                    module = Convert.ToInt32(err[..4]) - 2000;
                    desc = Convert.ToInt32(err[5..9]);
                    errcode = (desc << 9) + module;
                }

                var str_errcode = $"{module + 2000:04}-{desc:04}";
                // Searching for Modules in list
                if (_service.switch_modules.Select(x => x.Key).Contains(module))
                    err_module = _service.switch_modules[module];
                else
                    err_module = "Unknown";
                // Set initial value unconditionally
                var err_description = string.Empty;
                // Searching for error codes related to the Switch
                // (doesn't include special cases)
                if (_service.switch_known_errcodes.Select(x => x.Key).Contains(errcode))
                    err_description = _service.switch_known_errcodes[errcode];
                else if (_service.switch_support_page.Select(x => x.Key).Contains(errcode.ToString()))
                    err_description = _service.switch_support_page[errcode.ToString()];
                //else if (_service.switch_known_errcode_ranges.Select(x => x.Key).Contains(module))
                //{
                //    foreach (var errcode_range in _service.switch_known_errcode_ranges[module])
                //    {
                //        if (desc >= errcode_range[0] && desc <= errcode_range[1])
                //        {
                //            err_description = errcode_range[2];
                //        }
                //    }
                //}
                // Make a nice Embed out of it
                embed = new EmbedBuilder { Title = $"{str_errcode} / {errcode}", Description = err_description };
                embed.AddField("Module", $"{err_module} ({module})", true);
                embed.AddField("Description", desc, true);
                embed.WithOkColor();
                if (err_description.Contains("ban"))
                    embed.WithFooter("F to you | Console: Switch");
                else
                    embed.WithFooter("Console: Switch");
                await ctx.Channel.SendMessageAsync(embed: embed.Build());
            }
            else if (_service.switch_game_err.Select(x => x.Key).Contains(err))
            {
                // Special case handling because Nintendo feels like
                // its required to break their format lol
                var _tup_1 = _service.switch_game_err[err].Split(":");
                var game = _tup_1[0];
                var desc1 = _tup_1[1];
                embed = new EmbedBuilder { Title = err, Description = desc1 };
                embed.WithFooter("Console: Switch");
                embed.AddField("Game", game, true);
                await ctx.Channel.SendMessageAsync(embed: embed.Build());
            }
            else
            {
                await ctx.Channel.SendErrorAsync(
                    "Unknown Format - This is either no error code or you made some mistake!");
            }
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        public async Task err2hex(string err)
        {
            var switch_re = new Regex(@"2\d{3}\-\d{4}");
            if (switch_re.Match(err).Success)
            {
                var module = Convert.ToInt32(err[..4]) - 2000;
                var desc = Convert.ToInt32(err[5..9]);
                var errcode = (desc << 9) + module;
                await ctx.Channel.SendConfirmAsync($"0x{errcode:X}");
            }
            else
            {
                await ctx.Channel.SendErrorAsync("This doesn't follow the typical Nintendo Switch 2XXX-XXXX format!");
            }
        }
    }
}