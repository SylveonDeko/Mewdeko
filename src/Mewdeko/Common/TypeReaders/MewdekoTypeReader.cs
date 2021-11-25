using Discord.Commands;
using Discord.WebSocket;

namespace Mewdeko.Common.TypeReaders
{
    public abstract class MewdekoTypeReader<T> : TypeReader
    {
        private readonly DiscordSocketClient _client;
        private readonly CommandService _cmds;

        protected MewdekoTypeReader(DiscordSocketClient client, CommandService cmds)
        {
            _client = client;
            _cmds = cmds;
        }
    }
}