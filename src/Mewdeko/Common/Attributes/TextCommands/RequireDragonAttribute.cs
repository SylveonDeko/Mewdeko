using Discord.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace Mewdeko.Common.Attributes.TextCommands;

public class RequireDragonAttribute : PreconditionAttribute
{
    public override async Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command,
        IServiceProvider services)
    {
        var db = services.GetRequiredService(typeof(DbService)) as DbService;
        var guildConfigService = services.GetRequiredService(typeof(GuildSettingsService)) as GuildSettingsService;
        await using var ctx = db.GetDbContext();
        var user = await ctx.GetOrCreateUser(context.User);
        return user.IsDragon == 1
            ? PreconditionResult.FromSuccess()
            : PreconditionResult.FromError("Your meek human arms could never push the 10,000 pound rock blocking the " +
                                           "path out of the cave of stable features. You must call upon the dragon in " +
                                           "your soul to open a passage into the abyss of new features. (enable beta " +
                                           $"mode by running `{await guildConfigService.GetPrefix(context.Guild?.Id)}dragon` to use this command)");
    }
}