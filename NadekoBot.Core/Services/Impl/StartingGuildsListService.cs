using Discord.WebSocket;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Collections;

namespace NadekoBot.Core.Services.Impl
{
    public class StartingGuildsService : IEnumerable<ulong>, INService
    {
        private readonly ImmutableList<ulong> _guilds;

        public StartingGuildsService(DiscordSocketClient client)
        {
            this._guilds = client.Guilds.Select(x => x.Id).ToImmutableList();
        }

        public IEnumerator<ulong> GetEnumerator() =>
            _guilds.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() =>
            _guilds.GetEnumerator();
    }
}
