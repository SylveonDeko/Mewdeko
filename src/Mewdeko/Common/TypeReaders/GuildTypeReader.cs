using System;
using System.Linq;
using System.Threading.Tasks;
using Discord.Commands;
using Discord.WebSocket;
using Mewdeko.Common.TypeReaders;
using Discord;

namespace Mewdeko.Common.TypeReaders
{
    public class GuildTypeReader : MewdekoTypeReader<IGuild>
    {
        private readonly DiscordSocketClient _client;

        public GuildTypeReader(DiscordSocketClient client, CommandService cmds) : base(client, cmds)
        {
            _client = client;
        }

        public override Task<TypeReaderResult> ReadAsync(ICommandContext context, string input, IServiceProvider _)
        {
            input = input.Trim().ToUpperInvariant();
            var guilds = _client.Guilds;
            var guild = guilds.FirstOrDefault(g => g.Id.ToString().Trim().ToUpperInvariant() == input) ?? //by id
                        guilds.FirstOrDefault(g => g.Name.Trim().ToUpperInvariant() == input); //by name

            return Task.FromResult(guild != null ? TypeReaderResult.FromSuccess(guild) : TypeReaderResult.FromError(CommandError.ParseFailed, "No guild by that name or Id found"));
        }
    }
}