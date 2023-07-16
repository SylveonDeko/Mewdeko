using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Utility.Services;
using Serilog;

namespace Mewdeko.Modules.Utility;

public class JoinLeaveStats : MewdekoModuleBase<JoinLeaveLoggerService>
{
    [Cmd, Aliases, Ratelimit(10), RequireDragon]
    public async Task JoinStats()
    {
        try
        {
            var averageJoinsPerGuild = await Service.GenerateJoinLeaveGraphAsync(ctx.Guild.Id);
            await ctx.Channel.SendFileAsync(averageJoinsPerGuild, "joinleave.png", "Average joins per guild", embed: new EmbedBuilder
            {
                Description = $"Average joins per guild: {averageJoinsPerGuild}", Color = Mewdeko.OkColor
            }.Build());
        }
        catch (Exception e)
        {
            Log.Error(e, "Error generating join stats:");
        }
    }
}