using System.Threading.Tasks;

namespace Mewdeko.Common.ModuleBehaviors;

/// <summary>
///     Last thing to be executed, won't stop further executions
/// </summary>
public interface ILateExecutor
{
    Task LateExecute(DiscordSocketClient client, IGuild guild, IUserMessage msg);
}