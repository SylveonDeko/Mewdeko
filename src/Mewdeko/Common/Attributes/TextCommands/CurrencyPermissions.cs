using Discord.Commands;
using Mewdeko.Modules.Administration.Services;
using Mewdeko.Modules.Currency.Services;
using Mewdeko.Modules.Currency.Services.Impl;
using Microsoft.Extensions.DependencyInjection;

namespace Mewdeko.Common.Attributes.TextCommands;

/// <summary>
/// Checks whether the user has the neccesary permissions to use whatever currency command this attribute is attached to
/// </summary>
public class CurrencyPermissionsAttribute : PreconditionAttribute
{

    /// <inheritdoc />
    public override async Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
    {
        var currencyService = services.GetRequiredService<ICurrencyService>();
        var credService = services.GetRequiredService<IBotCredentials>();
        var discordPermOverrideService = services.GetRequiredService<DiscordPermOverrideService>();
        var isGlobal = currencyService.GetType() == typeof(GlobalCurrencyService);

        switch (isGlobal)
        {
            case true when !credService.IsOwner(context.User):
                return PreconditionResult.FromError(
                    "Not owner\nYou can host your own version of Mewdeko by following the instructions at https://github.com/sylveondeko/Mewdeko\nOr if you don't have anywhere to host it you can subscribe to our ko-fi at https://ko-fi.com/mewdeko");
            case false:
                discordPermOverrideService.TryGetOverrides(context.Guild.Id, command.Name, out var guildPerm);
                if (!((IGuildUser)context.User).GuildPermissions.Has(guildPerm))
                    return PreconditionResult.FromError($"You need the `{guildPerm}` permission to use this command.");
                break;
        }

        return PreconditionResult.FromSuccess();
    }
}