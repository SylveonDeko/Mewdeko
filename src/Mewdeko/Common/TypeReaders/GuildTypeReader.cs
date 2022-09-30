using Discord.Commands;
using System.Threading.Tasks;

namespace Mewdeko.Common.TypeReaders;

public class GuildTypeReader : MewdekoTypeReader<IGuild>
{
    private readonly DiscordSocketClient client;

    public GuildTypeReader(DiscordSocketClient client, CommandService cmds) : base(client, cmds) => this.client = client;

    public override Task<TypeReaderResult> ReadAsync(ICommandContext context, string input, IServiceProvider _)
    {
        input = input.Trim().ToUpperInvariant();
        var guilds = client.Guilds;
        var guild = guilds.FirstOrDefault(g => g.Id.ToString().Trim().ToUpperInvariant() == input) ?? //by id
                    guilds.FirstOrDefault(g => g.Name.Trim().ToUpperInvariant() == input); //by name

        if (guild != null)
            return Task.FromResult(TypeReaderResult.FromSuccess(guild));

        return Task.FromResult(
            TypeReaderResult.FromError(CommandError.ParseFailed, "No guild by that name or Id found"));
    }
}