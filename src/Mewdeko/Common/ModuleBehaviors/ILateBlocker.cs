using System.Threading.Tasks;
using Discord.Commands;
using Discord.WebSocket;

namespace Mewdeko.Common.ModuleBehaviors;

public interface ILateBlocker
{
    public int Priority { get; }

    Task<bool> TryBlockLate(DiscordSocketClient client, ICommandContext context,
        string moduleName, CommandInfo command);
}