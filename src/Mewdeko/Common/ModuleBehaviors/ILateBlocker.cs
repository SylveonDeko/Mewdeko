using Discord.Commands;
using Discord.Interactions;
using System.Threading.Tasks;

namespace Mewdeko.Common.ModuleBehaviors;

public interface ILateBlocker
{
    public int Priority { get; }

    Task<bool> TryBlockLate(DiscordSocketClient client, ICommandContext context,
        string moduleName, CommandInfo command);
    Task<bool> TryBlockLate(DiscordSocketClient client, IInteractionContext context,
        ICommandInfo command);
}