using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Utility.Services;
using Serilog;

namespace Mewdeko.Modules.Utility;

public partial class Utility
{
    public class JoinLeaveStats : MewdekoSubmodule<JoinLeaveLoggerService>
    {
        [Cmd, Aliases, Ratelimit(10), RequireDragon]
        public async Task JoinStats()
        {
            try
            {
                var (stream, embed) = await Service.GenerateJoinGraphAsync(ctx.Guild.Id);
                await ctx.Channel.SendFileAsync(stream, "joingraph.png", embed: embed);
            }
            catch (Exception e)
            {
                Log.Error(e, "Error generating join stats:");
            }
        }

        [Cmd, Aliases, Ratelimit(10), RequireDragon]
        public async Task LeaveStats()
        {
            try
            {
                var (stream, embed) = await Service.GenerateLeaveGraphAsync(ctx.Guild.Id);
                await ctx.Channel.SendFileAsync(stream, "leavegraph.png", embed: embed);
            }
            catch (Exception e)
            {
                Log.Error(e, "Error generating leave stats:");
            }
        }

        [Cmd, Aliases, RequireDragon]
        public async Task JoinStatsColor(int r, int g, int b)
        {
            if ((r is < 0 or > 255) || (g is < 0 or > 255) || (b is < 0 or > 255))
            {
                await ErrorLocalizedAsync("color_invalid");
            }

            var color = (uint)System.Drawing.Color.FromArgb(r, g, b).ToArgb();
            await Service.SetJoinColor(color, Context.Guild.Id);
            await ConfirmLocalizedAsync("color_set");
        }

        [Cmd, Aliases, RequireDragon]
        public async Task JoinStatsColor()
        {
            var color = (uint)System.Drawing.Color.FromArgb(255, 215, 0).ToArgb();
            await Service.SetJoinColor(color, Context.Guild.Id);
            await ConfirmLocalizedAsync("color_set");
        }

        [Cmd, Aliases, RequireDragon]
        public async Task LeaveStatsColor(int r, int g, int b)
        {
            if ((r is < 0 or > 255) || (g is < 0 or > 255) || (b is < 0 or > 255))
            {
                await ErrorLocalizedAsync("color_invalid");
            }

            var color = (uint)System.Drawing.Color.FromArgb(r, g, b).ToArgb();
            await Service.SetLeaveColor(color, Context.Guild.Id);
            await ConfirmLocalizedAsync("color_set");
        }

        [Cmd, Aliases, RequireDragon]
        public async Task LeaveStatsColor()
        {
            var color = (uint)System.Drawing.Color.FromArgb(255, 215, 0).ToArgb();
            await Service.SetLeaveColor(color, Context.Guild.Id);
            await ConfirmLocalizedAsync("color_set");
        }
    }
}