using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

namespace Mewdeko.Common.ModuleBehaviors
{
    public struct ModuleBehaviorResult
    {
        public bool Blocked { get; set; }
        public string NewInput { get; set; }

        public static ModuleBehaviorResult None()
        {
            return new ModuleBehaviorResult
            {
                Blocked = false,
                NewInput = null
            };
        }

        public static ModuleBehaviorResult FromBlocked(bool blocked)
        {
            return new ModuleBehaviorResult
            {
                Blocked = blocked,
                NewInput = null
            };
        }
    }

    public interface IModuleBehavior
    {
        /// <summary>
        ///     Negative priority means it will try to apply as early as possible
        ///     Positive priority menas it will try to apply as late as possible
        /// </summary>
        int Priority { get; }

        Task<ModuleBehaviorResult> ApplyBehavior(DiscordSocketClient client, IGuild guild, IUserMessage msg);
    }
}