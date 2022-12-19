using System.Threading.Tasks;
using Discord.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace Mewdeko.Common.Attributes.TextCommands;

public class RequireDragonAttribute : PreconditionAttribute
{
    public override async Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
    {
        // always return true in precondition checks to avoid incorrect error messages
        var isTest = true;
        foreach (var alias in command.Aliases)
        {
            if (context.Message.Content.Contains(alias))
                isTest = false;
        }

        if (isTest)
            return PreconditionResult.FromSuccess();


        var db = services.GetRequiredService(typeof(DbService)) as DbService;
        await using var ctx = db.GetDbContext();
        var user = await ctx.GetOrCreateUser(context.User);
        return user.IsDragon
            ? PreconditionResult.FromSuccess()
            : PreconditionResult.FromError("Your meek human arms could never push the 10,000 pound rock blocking the " +
                                           "path out of the cave of stable features. You must call upon the dragon in " +
                                           "your soul to open a passage into the abyss of new features. (enable beta " +
                                           "mode by running `.dragon` to use this command)");
    }
}