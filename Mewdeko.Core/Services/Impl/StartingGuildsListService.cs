using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Discord.WebSocket;

namespace Mewdeko.Core.Services.Impl
{
    public class StartingGuildsService : IEnumerable<ulong>, INService
    {
        private readonly ImmutableList<ulong> _guilds;

        public StartingGuildsService(DiscordSocketClient client)
        {
            _guilds = client.Guilds.Select(x => x.Id).ToImmutableList();
        }

        public IEnumerator<ulong> GetEnumerator()
        {
            return _guilds.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _guilds.GetEnumerator();
        }
    }
}