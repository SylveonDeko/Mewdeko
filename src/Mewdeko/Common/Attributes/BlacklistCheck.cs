using Discord;
using Discord.Interactions;
using Mewdeko.Database.Models;
using Mewdeko.Modules.Permissions.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Mewdeko.Common.Attributes;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class BlacklistCheck : PreconditionAttribute
{
    public override Task<PreconditionResult> CheckRequirementsAsync(IInteractionContext context,
        ICommandInfo executingCommand, IServiceProvider services)
    {
        var blacklistService = services.GetService<BlacklistService>();
#pragma warning disable CS8602
        foreach (var bl in blacklistService._blacklist)
#pragma warning restore CS8602
        {
            if (context.Guild != null && bl.Type == BlacklistType.Server && bl.ItemId == context.Guild.Id)
                return Task.FromResult(PreconditionResult.FromError("***This guild is blacklisted from Mewdeko! You can visit the support server below to try and resolve this.***"));

            if (bl.Type == BlacklistType.User && bl.ItemId == context.User.Id)
                return Task.FromResult(PreconditionResult.FromError("***You are blacklisted from Mewdeko! You can visit the support server below to try and resolve this.***"));
        }
        return Task.FromResult(PreconditionResult.FromSuccess());
    }
}