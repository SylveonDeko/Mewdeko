using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Utility.Services;
using Serilog;
using Color = System.Drawing.Color;

namespace Mewdeko.Modules.Utility;

public partial class Utility
{
    /// <summary>
    ///     Commands for managing join and leave statistics.
    /// </summary>
    public class JoinLeaveStats : MewdekoSubmodule<JoinLeaveLoggerService>
    {
        /// <summary>
        ///     Generates and sends a graph displaying the join statistics of the server.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [Ratelimit(10)]
        [RequireDragon]
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

        /// <summary>
        ///     Generates and sends a graph displaying the leave statistics of the server.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [Ratelimit(10)]
        [RequireDragon]
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

        /// <summary>
        ///     Sets the color for the join statistics graph.
        /// </summary>
        /// <param name="r">Red component of the color.</param>
        /// <param name="g">Green component of the color.</param>
        /// <param name="b">Blue component of the color.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireDragon]
        public async Task JoinStatsColor(int r, int g, int b)
        {
            if (r is < 0 or > 255 || g is < 0 or > 255 || b is < 0 or > 255)
            {
                await ErrorLocalizedAsync("color_invalid");
            }

            var color = (uint)Color.FromArgb(r, g, b).ToArgb();
            await Service.SetJoinColorAsync(color, Context.Guild.Id);
            await ConfirmLocalizedAsync("color_set");
        }

        /// <summary>
        ///     Sets the default color for the join statistics graph to gold.
        ///     This method is a convenience command that applies a predefined color without the need for RGB input.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <remarks>
        ///     This command requires the user to be a beta user, aka dragon.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireDragon]
        public async Task JoinStatsColor()
        {
            var color = (uint)Color.FromArgb(255, 215, 0).ToArgb();
            await Service.SetJoinColorAsync(color, Context.Guild.Id);
            await ConfirmLocalizedAsync("color_set");
        }

        /// <summary>
        ///     Sets the default color for the leave statistics graph using specific RGB values.
        ///     Allows for precise control over the graph's appearance.
        /// </summary>
        /// <param name="r">Red component of the color, between 0 and 255.</param>
        /// <param name="g">Green component of the color, between 0 and 255.</param>
        /// <param name="b">Blue component of the color, between 0 and 255.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <remarks>
        ///     This command enables customization of the leave statistics graph's color to match server themes or preferences.
        ///     It requires the user to be a beta user, aka dragon.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireDragon]
        public async Task LeaveStatsColor(int r, int g, int b)
        {
            if (r is < 0 or > 255 || g is < 0 or > 255 || b is < 0 or > 255)
            {
                await ErrorLocalizedAsync("color_invalid");
            }

            var color = (uint)Color.FromArgb(r, g, b).ToArgb();
            await Service.SetLeaveColorAsync(color, Context.Guild.Id);
            await ConfirmLocalizedAsync("color_set");
        }

        /// <summary>
        ///     Sets the default color for the leave statistics graph to gold.
        ///     This method is a convenience command that applies a predefined color without the need for RGB input.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <remarks>
        ///     Similar to the JoinStatsColor method, this command is designed for ease of use, offering a quick way to set a
        ///     visually appealing color for the leave statistics graph.
        ///     Requires the user to be a beta user, aka dragon.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireDragon]
        public async Task LeaveStatsColor()
        {
            var color = (uint)Color.FromArgb(255, 215, 0).ToArgb();
            await Service.SetLeaveColorAsync(color, Context.Guild.Id);
            await ConfirmLocalizedAsync("color_set");
        }
    }
}