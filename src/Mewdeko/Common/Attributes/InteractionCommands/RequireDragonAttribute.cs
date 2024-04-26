using Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;

namespace Mewdeko.Common.Attributes.InteractionCommands;

/// <summary>
/// Attribute to check if a user has the dragon status before executing a command or method.
/// </summary>
public class RequireDragonAttribute : PreconditionAttribute
{
    /// <summary>
    /// Checks the requirements before executing a command or method.
    /// </summary>
    /// <param name="context">The interaction context.</param>
    /// <param name="commandInfo">The command being executed.</param>
    /// <param name="services">The service provider.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the precondition result.</returns>
    public override async Task<PreconditionResult> CheckRequirementsAsync(IInteractionContext context,
        ICommandInfo commandInfo, IServiceProvider services)
    {
        // Get the database service.
        var db = services.GetRequiredService(typeof(DbService)) as DbService;

        // Get the database context.
        await using var ctx = db.GetDbContext();

        // Get or create the user in the database.
        var user = await ctx.GetOrCreateUser(context.User);

        // If the user has the dragon status, return success.
        // Otherwise, return an error with a message.
        return user.IsDragon
            ? PreconditionResult.FromSuccess()
            : PreconditionResult.FromError("Your meek human arms could never push the 10,000 pound rock blocking the " +
                                           "path out of the cave of stable features. You must call upon the dragon in " +
                                           "your soul to open a passage into the abyss of new features. (enable beta " +
                                           "mode by running `.dragon` to use this command)");
    }
}