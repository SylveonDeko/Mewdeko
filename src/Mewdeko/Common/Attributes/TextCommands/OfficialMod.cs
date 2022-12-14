using System.Threading.Tasks;
using Discord.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace Mewdeko.Common.Attributes.TextCommands;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class OfficialServerModAttribute : PreconditionAttribute
{
    public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context,
        CommandInfo executingCommand, IServiceProvider services)
    {
        var creds = services.GetService<IBotCredentials>();

        return Task.FromResult(
            creds != null && (creds.IsOfficialMod(context.User) || context.Client.CurrentUser.Id == context.User.Id)
                ? PreconditionResult.FromSuccess()
                : PreconditionResult.FromError("Not Mod In Official Support"));
    }
}