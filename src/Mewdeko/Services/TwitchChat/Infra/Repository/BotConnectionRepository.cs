using Mewdeko.Services.TwitchChat.Entities;

namespace Mewdeko.Services.TwitchChat.Infra.Repository
{
    public class BotConnectionRepository
    {
        /*
        public async Task<BotConnection?> GetById(string id, bool grabCommands = false)
        {
            return await GetBotConnection($"botconnection: {id}:*:*", grabCommands);
        }
        
        private async Task<BotConnection> GetBotConnection(string searchPattern, bool grabCommands)
        {
            // search db for key pattern

            //
            //
        }
        */
    }

    public interface IBotConnectionRepository
    {
        Task<BotConnection?> GetById(string id, bool grabCommands = false);
    }
}
