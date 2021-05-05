using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

namespace NadekoBot.Common.ModuleBehaviors
{
    /// <summary>
    /// Implemented by modules which block execution before anything is executed
    /// </summary>
    public interface IEarlyBehavior
    {
        int Priority { get; }
        ModuleBehaviorType BehaviorType { get; }

        Task<bool> RunBehavior(DiscordSocketClient client, IGuild guild, IUserMessage msg);
    }

    public enum ModuleBehaviorType
    {
        Blocker,
        Executor,
    }
}
