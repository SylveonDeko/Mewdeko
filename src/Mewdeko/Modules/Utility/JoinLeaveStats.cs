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
            var averageJoinsPerGuild = await Service.GenerateJoinGraphAsync(ctx.Guild.Id);
            await ctx.Channel.SendFileAsync(averageJoinsPerGuild, "join.png");
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
            var averageJoinsPerGuild = await Service.GenerateLeaveGraphAsync(ctx.Guild.Id);
            await ctx.Channel.SendFileAsync(averageJoinsPerGuild, "leave.png");
        }
        catch (Exception e)
        {
            Log.Error(e, "Error generating leave stats:");
        }
    }
}