using Discord;
using Discord.WebSocket;
using NadekoBot.Common.ModuleBehaviors;
using NadekoBot.Core.Services;
using NadekoBot.Core.Services.Database.Models;
using System.Threading.Tasks;

namespace NadekoBot.Modules.Permissions.Services
{
    public class BlacklistService : IEarlyBehavior, INService
    {
        private readonly IBotConfigProvider _bc;

        public int Priority => -100;

        public ModuleBehaviorType BehaviorType => ModuleBehaviorType.Blocker;

        public BlacklistService(IBotConfigProvider bc)
        {
            _bc = bc;
            var blacklist = bc.BotConfig.Blacklist;
        }

        public async Task<bool> RunBehavior(DiscordSocketClient _, IGuild guild, IUserMessage usrMsg)
        {
            await Task.Yield();
            var blItems = _bc.BotConfig.Blacklist;
            foreach (var bl in blItems)
            {
                if (guild != null && bl.Type == BlacklistType.Server && bl.ItemId == guild.Id)
                    return true;

                if (bl.Type == BlacklistType.Channel && bl.ItemId == usrMsg.Channel.Id)
                    return true;

                if (bl.Type == BlacklistType.User && bl.ItemId == usrMsg.Author.Id)
                    return true;
            }

            return false;
        }
    }
}
