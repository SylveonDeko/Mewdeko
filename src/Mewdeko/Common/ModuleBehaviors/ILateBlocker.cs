using System.Threading.Tasks;
using Discord.Commands;
using Discord.Interactions;

namespace Mewdeko.Common.ModuleBehaviors;

public interface ILateBlocker
{
    public int Priority { get; }

    Task<bool> TryBlockLate(DiscordSocketClient client, ICommandContext context,
        string moduleName, CommandInfo command);

    Task<bool> TryBlockLate(DiscordSocketClient client, IInteractionContext context,
        ICommandInfo command);
}